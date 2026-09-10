using MacroDeckHost.Integrations.Discord.Rpc;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Logging;
using MacroDeckHost.Localization;
using Serilog;

namespace MacroDeckHost.Integrations.Discord.Actions;

internal sealed class SetUserVolumeActionDefinition
	: IActionDefinition, IDynamicOptionsActionDefinition, IIconProviderActionDefinition
{
	internal const string VolumeParameter = "volume";

	internal const double MaxVolume = 200d;

	private readonly Func<DiscordConnection?> _resolver;

	public SetUserVolumeActionDefinition(Func<DiscordConnection?> resolver)
	{
		_resolver = resolver;

		Parameters =
		[
			DiscordVoiceUserParameters.UserSelector(),
			DiscordActionParameters.VolumeModeSelector(),
			ActionParameter.Slider(VolumeParameter,
				min: 0,
				max: MaxVolume,
				label: AppStrings.Integrations.Discord.Actions.SetVoiceVolume.VolumeLabel(),
				description: AppStrings.Integrations.Discord.Actions.SetVoiceVolume.VolumeDescription(),
				step: 1,
				defaultValue: 100d)
		];
	}

	public string Id => "set-user-volume";

	public LocalizedText Name => AppStrings.Integrations.Discord.Actions.SetUserVolume.Name();

	public LocalizedText Description => AppStrings.Integrations.Discord.Actions.SetUserVolume.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; }

	public IActionExecutor CreateExecutor() => new Executor(_resolver);

	public Task<ActionIconSnapshot?> GetActionIconAsync(
		IReadOnlyDictionary<string, object?> parameters,
		CancellationToken cancellationToken)
		=> Task.FromResult(DiscordVoiceUserParameters.GetIcon(_resolver(), parameters));

	public Task<ActionIconContent?> GetActionIconContentAsync(
		IReadOnlyDictionary<string, object?> parameters,
		string version,
		CancellationToken cancellationToken)
		=> DiscordVoiceUserParameters.GetIconContentAsync(_resolver(), parameters, version, cancellationToken);

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(
		DynamicOptionsContext context,
		CancellationToken cancellationToken)
		=> string.Equals(context.ParameterName, DiscordVoiceUserParameters.UserParameter, StringComparison.Ordinal)
			? DiscordVoiceUserParameters.GetUserOptionsAsync(_resolver())
			: Task.FromResult(new DynamicOptionsResult
			{
				Options = [],
				AllowsCustomValue = true,
				CacheSeconds = 10
			});

	private sealed class Executor : IActionExecutor
	{
		private static readonly ILogger _logger =
			IntegrationLog.For<SetUserVolumeActionDefinition>(DiscordIntegration.IntegrationId);

		private readonly Func<DiscordConnection?> _resolver;

		public Executor(Func<DiscordConnection?> resolver)
		{
			_resolver = resolver;
		}

		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var connection = _resolver();
			if (connection is null)
			{
				_logger.Warning("Discord user volume action skipped: Discord is not set up");
				return ActionResult.Failed(ActionErrorCodes.NotConfigured,
					AppStrings.Integrations.Discord.Errors.NotSetUp());
			}

			var userId = DiscordVoiceUserParameters.ReadUserId(
				context.Parameters.GetValueOrDefault(DiscordVoiceUserParameters.UserParameter));
			if (userId is null)
			{
				_logger.Warning("Discord user volume action skipped: no user selected");
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					AppStrings.Integrations.Discord.Errors.NoUserSelected());
			}

			var value = DiscordActionParameters.ReadNumber(context.Parameters.GetValueOrDefault(VolumeParameter));
			var mode = DiscordActionParameters.ReadMode(
				context.Parameters.GetValueOrDefault(DiscordActionParameters.ModeParameter));

			var member = DiscordVoiceUserParameters.FindUser(connection.State, userId);
			if (member is null && !string.Equals(mode, DiscordActionParameters.ModeSet, StringComparison.Ordinal))
			{
				_logger.Warning("Discord user volume action skipped: {UserId} is not in the voice channel", userId);
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					AppStrings.Integrations.Discord.Errors.UnknownUser());
			}

			var current = member is null ? 100d : DiscordPerceptualVolume.ToDisplay(member.Volume);
			var target = mode switch
			{
				DiscordActionParameters.ModeIncrease => current + value,
				DiscordActionParameters.ModeDecrease => current - value,
				_ => value
			};

			var result = await connection
				.SetUserVoiceSettingsAsync(new DiscordUserVoiceSettingsPatch
				{
					UserId = userId,
					Volume = DiscordPerceptualVolume.ToRpc(Math.Clamp(target, 0d, MaxVolume))
				},
				context.CancellationToken)
				.ConfigureAwait(false);

			return DiscordVoiceActionResults.ToUserVoiceActionResult(result, "set-user-volume");
		}
	}
}

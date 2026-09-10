using MacroDeckHost.Integrations;
using MacroDeckHost.Integrations.Discord.Rpc;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Logging;
using MacroDeckHost.Localization;
using Serilog;

namespace MacroDeckHost.Integrations.Discord.Actions;

internal sealed class MuteUserActionDefinition
	: IActionDefinition, IDynamicOptionsActionDefinition, IIconProviderActionDefinition,
		IStateProviderActionDefinition
{
	private readonly Func<DiscordConnection?> _resolver;

	public MuteUserActionDefinition(Func<DiscordConnection?> resolver)
	{
		_resolver = resolver;

		Parameters =
		[
			DiscordVoiceUserParameters.UserSelector(),
			DiscordActionParameters.StateSelector(AppStrings.Integrations.Discord.Actions.VoiceToggle.TurnOnOption(),
				AppStrings.Integrations.Discord.Actions.VoiceToggle.TurnOffOption())
		];
	}

	public string Id => "mute-user";

	public LocalizedText Name => AppStrings.Integrations.Discord.Actions.MuteUser.Name();

	public LocalizedText Description => AppStrings.Integrations.Discord.Actions.MuteUser.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; }

	public IActionExecutor CreateExecutor() => new Executor(_resolver);

	// A member's mute can only change elsewhere, so the button learns about it from the poll -
	// the host minimum, since every read is answered from already-held state.
	public TimeSpan StatePollInterval => TimeSpan.FromSeconds(1);

	// The same read the executor toggles against, so a button following this instance shows the
	// member's live mute state and offers the shared muted/unmuted styling for it.
	public Task<ActionStateSnapshot?> GetActionStateAsync(
		IReadOnlyDictionary<string, object?> parameters,
		CancellationToken cancellationToken)
	{
		var member = DiscordVoiceUserParameters.FindConfiguredUser(_resolver(), parameters);
		return Task.FromResult<ActionStateSnapshot?>(ActionStates.Snapshot(ActionStates.Mute,
			member?.Muted));
	}

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
			IntegrationLog.For<MuteUserActionDefinition>(DiscordIntegration.IntegrationId);

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
				_logger.Warning("Discord mute-user action skipped: Discord is not set up");
				return ActionResult.Failed(ActionErrorCodes.NotConfigured,
					AppStrings.Integrations.Discord.Errors.NotSetUp());
			}

			var userId = DiscordVoiceUserParameters.ReadUserId(
				context.Parameters.GetValueOrDefault(DiscordVoiceUserParameters.UserParameter));
			if (userId is null)
			{
				_logger.Warning("Discord mute-user action skipped: no user selected");
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					AppStrings.Integrations.Discord.Errors.NoUserSelected());
			}

			var member = DiscordVoiceUserParameters.FindUser(connection.State, userId);
			var target = DiscordActionParameters.ResolveState(
				context.Parameters.GetValueOrDefault(DiscordActionParameters.StateParameter),
				member?.Muted ?? false);

			var result = await connection
				.SetUserVoiceSettingsAsync(new DiscordUserVoiceSettingsPatch { UserId = userId, Mute = target },
					context.CancellationToken)
				.ConfigureAwait(false);

			return DiscordVoiceActionResults.ToUserVoiceActionResult(result, "mute-user");
		}
	}
}

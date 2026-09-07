using MacroDeck.Sdk.Actions;
using MacroDeckHost.Localization;

namespace MacroDeckHost.Integrations.Discord.Actions;

internal static class DiscordVoiceUserParameters
{
	public const string UserParameter = "userId";

	public static ActionParameter UserSelector()
		=> ActionParameter.DynamicChoice(UserParameter,
			label: AppStrings.Integrations.Discord.Actions.VoiceUser.UserLabel(),
			description: AppStrings.Integrations.Discord.Actions.VoiceUser.UserDescription(),
			required: true);

	public static string? ReadUserId(object? raw) => DiscordActionParameters.ReadText(raw);

	public static DiscordVoiceUser? FindUser(DiscordState state, string userId)
		=> state.VoiceUsers.FirstOrDefault(u => string.Equals(u.UserId, userId, StringComparison.Ordinal));

	public static Task<DynamicOptionsResult> GetUserOptionsAsync(DiscordConnection? connection)
	{
		IReadOnlyList<ActionParameterOption> options = connection is null
			? []
			: [.. connection.State.VoiceUsers.Select(u => new ActionParameterOption
			{
				Value = u.UserId,
				Label = u.Name ?? u.UserId
			})];

		return Task.FromResult(new DynamicOptionsResult
		{
			Options = options,
			AllowsCustomValue = true,
			CacheSeconds = 10
		});
	}
}

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

	public static DiscordVoiceUser? FindConfiguredUser(
		DiscordConnection? connection,
		IReadOnlyDictionary<string, object?> parameters)
	{
		var userId = ReadUserId(parameters.GetValueOrDefault(UserParameter));
		return connection is null || userId is null ? null : FindUser(connection.State, userId);
	}

	public static ActionIconSnapshot? GetIcon(
		DiscordConnection? connection,
		IReadOnlyDictionary<string, object?> parameters)
	{
		var version = DiscordAvatarCache.VersionFor(FindConfiguredUser(connection, parameters));
		return version is null
			? null
			: new ActionIconSnapshot { Version = version, MediaType = DiscordAvatarCache.MediaType };
	}

	public static async Task<ActionIconContent?> GetIconContentAsync(
		DiscordConnection? connection,
		IReadOnlyDictionary<string, object?> parameters,
		string version,
		CancellationToken cancellationToken)
	{
		var member = FindConfiguredUser(connection, parameters);
		if (member?.AvatarHash is not { } hash ||
			!string.Equals(DiscordAvatarCache.Version(member.UserId, hash), version, StringComparison.Ordinal))
		{
			return null;
		}

		var bytes = await connection!
			.Avatars
			.GetAvatarAsync(member.UserId, hash, cancellationToken)
			.ConfigureAwait(false);

		return bytes is null ? null : new ActionIconContent(bytes, DiscordAvatarCache.MediaType);
	}

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

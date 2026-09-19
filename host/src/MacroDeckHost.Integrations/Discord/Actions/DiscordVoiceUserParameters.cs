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
		var userId = ReadUserId(parameters.GetValueOrDefault(UserParameter));
		if (connection is null || userId is null)
		{
			return null;
		}

		var version = DiscordAvatarCache.VersionFor(FindUser(connection.State, userId))
			?? (connection.State.KnownAvatars.TryGetValue(userId, out var hash)
				? DiscordAvatarCache.DimmedVersion(userId, hash)
				: null);

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
		var userId = ReadUserId(parameters.GetValueOrDefault(UserParameter));
		if (connection is null || userId is null)
		{
			return null;
		}

		var member = FindUser(connection.State, userId);
		if (member?.AvatarHash is { } hash &&
			string.Equals(DiscordAvatarCache.Version(userId, hash), version, StringComparison.Ordinal))
		{
			var bytes = await connection
				.Avatars
				.GetAvatarAsync(userId, hash, cancellationToken)
				.ConfigureAwait(false);

			return bytes is null ? null : new ActionIconContent(bytes, DiscordAvatarCache.MediaType);
		}

		if (connection.State.KnownAvatars.TryGetValue(userId, out var known) &&
			string.Equals(DiscordAvatarCache.DimmedVersion(userId, known), version, StringComparison.Ordinal))
		{
			var dimmed = await connection
				.Avatars
				.GetDimmedAvatarAsync(userId, known, cancellationToken)
				.ConfigureAwait(false);

			return dimmed is null ? null : new ActionIconContent(dimmed, DiscordAvatarCache.MediaType);
		}

		return null;
	}

	public static Task<DynamicOptionsResult> GetUserOptionsAsync(DiscordConnection? connection)
	{
		// Self is never offered: per-user volume does nothing for our own playback and self mute
		// already exists as a global action, so listing ourselves would only invite a dead control.
		IReadOnlyList<ActionParameterOption> options = connection is null
			? []
			: [.. connection.State.VoiceUsers
				.Where(u => !string.Equals(u.UserId, connection.State.UserId, StringComparison.Ordinal))
				.Select(u => new ActionParameterOption
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

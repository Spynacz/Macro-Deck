using System.Collections.Concurrent;

namespace MacroDeckHost.Integrations.Discord;

internal sealed class DiscordAvatarCache
{
	public const string MediaType = "image/png";

	private static readonly HttpClient _shared = new() { Timeout = TimeSpan.FromSeconds(10) };

	private readonly HttpClient _http;
	private readonly ConcurrentDictionary<string, byte[]> _bytes = new(StringComparer.Ordinal);

	public DiscordAvatarCache(HttpClient? http = null)
	{
		_http = http ?? _shared;
	}

	public static string Version(string userId, string avatarHash)
		=> $"discord-avatar:{userId}:{avatarHash}";

	public static string Url(string userId, string avatarHash)
		=> $"https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png?size=128";

	public static string? VersionFor(DiscordVoiceUser? member)
		=> member?.AvatarHash is { } hash ? Version(member.UserId, hash) : null;

	public async Task<byte[]?> GetAvatarAsync(
		string userId,
		string avatarHash,
		CancellationToken cancellationToken)
	{
		var version = Version(userId, avatarHash);
		if (_bytes.TryGetValue(version, out var cached))
		{
			return cached;
		}

		byte[] downloaded;
		try
		{
			downloaded = await _http
				.GetByteArrayAsync(Url(userId, avatarHash), cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
		{
			return null;
		}

		return downloaded.Length == 0 ? null : _bytes.GetOrAdd(version, downloaded);
	}
}

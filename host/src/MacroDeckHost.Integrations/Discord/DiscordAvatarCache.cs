using System.Collections.Concurrent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace MacroDeckHost.Integrations.Discord;

internal sealed class DiscordAvatarCache
{
	public const string MediaType = "image/png";

	private const float DimBrightness = 0.4f;

	private static readonly HttpClient _shared = new() { Timeout = TimeSpan.FromSeconds(10) };

	private readonly HttpClient _http;
	private readonly ConcurrentDictionary<string, byte[]> _bytes = new(StringComparer.Ordinal);

	public DiscordAvatarCache(HttpClient? http = null)
	{
		_http = http ?? _shared;
	}

	public static string Version(string userId, string avatarHash)
		=> $"discord-avatar:{userId}:{avatarHash}";

	public static string DimmedVersion(string userId, string avatarHash)
		=> $"{Version(userId, avatarHash)}:dimmed";

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

	public async Task<byte[]?> GetDimmedAvatarAsync(
		string userId,
		string avatarHash,
		CancellationToken cancellationToken)
	{
		var version = DimmedVersion(userId, avatarHash);
		if (_bytes.TryGetValue(version, out var cached))
		{
			return cached;
		}

		var plain = await GetAvatarAsync(userId, avatarHash, cancellationToken).ConfigureAwait(false);
		if (plain is null)
		{
			return null;
		}

		var dimmed = DimAvatar(plain);
		return dimmed is null ? null : _bytes.GetOrAdd(version, dimmed);
	}

	private static byte[]? DimAvatar(byte[] png)
	{
		try
		{
			using var image = Image.Load(png);
			image.Mutate(ctx => ctx.Brightness(DimBrightness));
			using var stream = new MemoryStream();
			image.SaveAsPng(stream);
			return stream.ToArray();
		}
		catch (Exception ex) when (ex is InvalidImageContentException or UnknownImageFormatException)
		{
			return null;
		}
	}
}

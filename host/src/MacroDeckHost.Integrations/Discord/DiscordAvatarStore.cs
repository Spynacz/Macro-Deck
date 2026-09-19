using System.Text.Json;

namespace MacroDeckHost.Integrations.Discord;

internal static class DiscordAvatarStore
{
	private const int MaxPersistedAvatars = 1000;

	public static string Serialize(IReadOnlyDictionary<string, string> avatars)
		=> JsonSerializer.Serialize(avatars);

	public static IReadOnlyDictionary<string, string> Parse(string? raw)
	{
		Dictionary<string, string>? parsed;
		try
		{
			parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(raw ?? string.Empty);
		}
		catch (JsonException)
		{
			return new Dictionary<string, string>();
		}

		if (parsed is null)
		{
			return new Dictionary<string, string>();
		}

		return parsed
			.Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(entry.Value))
			.Take(MaxPersistedAvatars)
			.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
	}
}

using System.Text.Json.Serialization;

namespace MacroDeckHost.Integrations.Discord.Rpc;

internal sealed record DiscordUserVoiceSettingsPatch
{
	[JsonPropertyName("user_id")]
	public required string UserId { get; init; }

	// Discord takes an integer percentage here, unlike the floating self volumes.
	[JsonPropertyName("volume")]
	public int? Volume { get; init; }

	[JsonPropertyName("mute")]
	public bool? Mute { get; init; }
}

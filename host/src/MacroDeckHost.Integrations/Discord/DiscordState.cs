using System.Collections.Immutable;

namespace MacroDeckHost.Integrations.Discord;

internal sealed record DiscordVoiceUser
{
	public required string UserId { get; init; }

	public string? Name { get; init; }

	public string? AvatarHash { get; init; }

	public double Volume { get; init; } = 100;

	public bool Muted { get; init; }
}

internal sealed record DiscordState
{
	public static DiscordState Disconnected { get; } = new();

	public bool IsConnected { get; init; }

	public string? UserId { get; init; }

	public string? UserName { get; init; }

	public bool SelfMuted { get; init; }

	public bool SelfDeafened { get; init; }

	public bool ServerMuted { get; init; }

	public bool ServerDeafened { get; init; }

	public bool EffectivelyMuted => SelfMuted || ServerMuted || SelfDeafened || ServerDeafened;

	public bool EffectivelySelfMuted => SelfMuted || SelfDeafened;

	public bool Deafened => SelfDeafened || ServerDeafened;

	public bool InVoiceChannel => VoiceChannelId is not null;

	public string? VoiceChannelId { get; init; }

	public string? VoiceChannelName { get; init; }

	public string? VoiceGuildId { get; init; }

	public string? VoiceGuildName { get; init; }

	public double InputVolume { get; init; }

	public double OutputVolume { get; init; }

	public string? VoiceMode { get; init; }

	public bool NoiseSuppression { get; init; }

	public bool EchoCancellation { get; init; }

	public bool AutomaticGainControl { get; init; }

	public string? VoiceConnectionState { get; init; }

	public int? AveragePing { get; init; }

	public bool SelfSpeaking { get; init; }

	public IReadOnlyList<DiscordVoiceUser> VoiceUsers { get; init; } = [];

	// Every avatar hash ever seen, upserted on sight and never cleared: a button bound to a user
	// keeps showing their dimmed face even when neither of them is in the channel any more.
	public ImmutableDictionary<string, string> KnownAvatars { get; init; } =
		ImmutableDictionary<string, string>.Empty;

	public DiscordState WithoutVoiceChannel()
	{
		return this with
		{
			VoiceChannelId = null,
			VoiceChannelName = null,
			VoiceGuildId = null,
			VoiceGuildName = null,
			ServerMuted = false,
			ServerDeafened = false,
			SelfSpeaking = false,
			VoiceConnectionState = null,
			AveragePing = null,
			VoiceUsers = []
		};
	}
}

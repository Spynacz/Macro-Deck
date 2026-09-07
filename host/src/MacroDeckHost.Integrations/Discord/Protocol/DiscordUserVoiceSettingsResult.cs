namespace MacroDeckHost.Integrations.Discord.Rpc;

internal enum DiscordUserVoiceSettingsOutcome
{
	Applied,
	NotConnected,
	Rejected,
	Failed
}

internal sealed record DiscordUserVoiceSettingsResult
{
	public required DiscordUserVoiceSettingsOutcome Outcome { get; init; }

	public string? Message { get; init; }

	public static DiscordUserVoiceSettingsResult Applied() => new() { Outcome = DiscordUserVoiceSettingsOutcome.Applied };

	public static DiscordUserVoiceSettingsResult NotConnected() =>
		new() { Outcome = DiscordUserVoiceSettingsOutcome.NotConnected };

	public static DiscordUserVoiceSettingsResult Rejected(string message) =>
		new() { Outcome = DiscordUserVoiceSettingsOutcome.Rejected, Message = message };

	public static DiscordUserVoiceSettingsResult Failed(string message) =>
		new() { Outcome = DiscordUserVoiceSettingsOutcome.Failed, Message = message };
}

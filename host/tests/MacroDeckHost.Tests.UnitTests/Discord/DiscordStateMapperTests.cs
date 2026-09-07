using System.Text.Json;
using MacroDeckHost.Integrations.Discord;

namespace MacroDeckHost.Tests.UnitTests.Discord;

[TestFixture]
internal sealed class DiscordStateMapperTests
{
	private static readonly DiscordState _connected = new() { IsConnected = true, UserId = "me" };
	private static readonly string[] _grantedScopes = ["rpc", "rpc.voice.read"];

	[Test]
	public void Voice_settings_are_read_in_full()
	{
		var state = DiscordStateMapper.ApplyVoiceSettings(_connected,
			Json("""
				 {
				 	"mute": true,
				 	"deaf": true,
				 	"noise_suppression": true,
				 	"echo_cancellation": false,
				 	"automatic_gain_control": true,
				 	"input": { "volume": 73.5 },
				 	"output": { "volume": 145 },
				 	"mode": { "type": "PUSH_TO_TALK" }
				 }
				 """));

		Assert.Multiple(() =>
		{
			Assert.That(state.SelfMuted, Is.True);
			Assert.That(state.SelfDeafened, Is.True);
			Assert.That(state.NoiseSuppression, Is.True);
			Assert.That(state.EchoCancellation, Is.False);
			Assert.That(state.AutomaticGainControl, Is.True);
			Assert.That(state.InputVolume, Is.EqualTo(73.5));
			Assert.That(state.OutputVolume, Is.EqualTo(145));
			Assert.That(state.VoiceMode, Is.EqualTo("PUSH_TO_TALK"));
		});
	}

	[Test]
	public void A_deaf_true_mute_false_payload_silences_the_microphone()
	{
		var state = DiscordStateMapper.ApplyVoiceSettings(_connected, Json("""{"mute":false,"deaf":true}"""));

		Assert.Multiple(() =>
		{
			Assert.That(state.SelfMuted, Is.False);
			Assert.That(state.SelfDeafened, Is.True);
			Assert.That(state.EffectivelyMuted, Is.True);
		});
	}

	[Test]
	public void A_partial_voice_settings_payload_leaves_the_other_values_alone()
	{
		var seeded = _connected with { SelfMuted = true, NoiseSuppression = true, InputVolume = 80 };

		var state = DiscordStateMapper.ApplyVoiceSettings(seeded, Json("""{"deaf":true}"""));

		Assert.Multiple(() =>
		{
			Assert.That(state.SelfDeafened, Is.True);
			Assert.That(state.SelfMuted, Is.True);
			Assert.That(state.NoiseSuppression, Is.True);
			Assert.That(state.InputVolume, Is.EqualTo(80));
		});
	}

	[Test]
	public void Our_own_voice_state_supplies_the_server_flags()
	{
		var state = DiscordStateMapper.ApplyVoiceState(_connected,
			Json("""
				 {
				 	"user": { "id": "me" },
				 	"voice_state": { "mute": true, "deaf": true, "self_mute": false, "self_deaf": false }
				 }
				 """));

		Assert.Multiple(() =>
		{
			Assert.That(state.ServerMuted, Is.True);
			Assert.That(state.ServerDeafened, Is.True);
			Assert.That(state.EffectivelyMuted, Is.True, "the combined value has to follow the server flag");
			Assert.That(state.Deafened, Is.True);
		});
	}

	[Test]
	public void Our_own_voice_state_does_not_touch_the_self_flags()
	{
		var seeded = _connected with { SelfMuted = true, SelfDeafened = true };

		var state = DiscordStateMapper.ApplyVoiceState(seeded,
			Json("""{"user":{"id":"me"},"voice_state":{"self_mute":false,"self_deaf":false}}"""));

		Assert.Multiple(() =>
		{
			Assert.That(state.SelfMuted, Is.True);
			Assert.That(state.SelfDeafened, Is.True);
		});
	}

	[Test]
	public void Someone_elses_voice_state_is_ignored()
	{
		var state = DiscordStateMapper.ApplyVoiceState(_connected,
			Json("""{"user":{"id":"someone-else"},"voice_state":{"mute":true,"deaf":true}}"""));

		Assert.Multiple(() =>
		{
			Assert.That(state.ServerMuted, Is.False);
			Assert.That(state.ServerDeafened, Is.False);
		});
	}

	[Test]
	public void A_selected_channel_brings_its_name_server_and_own_voice_state()
	{
		var state = DiscordStateMapper.ApplySelectedVoiceChannel(_connected,
			Json("""
				 {
				 	"id": "555",
				 	"name": "General",
				 	"guild_id": "999",
				 	"voice_states": [
				 		{ "user": { "id": "other" }, "voice_state": { "mute": false, "deaf": false } },
				 		{ "user": { "id": "me" }, "voice_state": { "mute": true, "deaf": false } }
				 	]
				 }
				 """),
			"My Server");

		Assert.Multiple(() =>
		{
			Assert.That(state.VoiceChannelId, Is.EqualTo("555"));
			Assert.That(state.VoiceChannelName, Is.EqualTo("General"));
			Assert.That(state.VoiceGuildId, Is.EqualTo("999"));
			Assert.That(state.VoiceGuildName, Is.EqualTo("My Server"));
			Assert.That(state.InVoiceChannel, Is.True);
			Assert.That(state.ServerMuted, Is.True);
			Assert.That(state.ServerDeafened, Is.False);
		});
	}

	[Test]
	public void A_new_channel_clears_the_server_flags_of_the_previous_one()
	{
		var seeded = _connected with { ServerMuted = true, ServerDeafened = true, VoiceChannelId = "old" };

		var state = DiscordStateMapper.ApplySelectedVoiceChannel(seeded,
			Json("""{"id":"new","name":"Other","guild_id":"999","voice_states":[]}"""));

		Assert.Multiple(() =>
		{
			Assert.That(state.VoiceChannelId, Is.EqualTo("new"));
			Assert.That(state.ServerMuted, Is.False);
			Assert.That(state.ServerDeafened, Is.False);
		});
	}

	[Test]
	public void A_null_selected_channel_means_not_in_a_voice_channel()
	{
		var seeded = _connected with
		{
			VoiceChannelId = "555",
			VoiceChannelName = "General",
			VoiceGuildName = "My Server",
			ServerMuted = true,
			SelfSpeaking = true,
			AveragePing = 42
		};

		var state = DiscordStateMapper.ApplySelectedVoiceChannel(seeded, Json("null"));

		Assert.Multiple(() =>
		{
			Assert.That(state.InVoiceChannel, Is.False);
			Assert.That(state.VoiceChannelName, Is.Null);
			Assert.That(state.VoiceGuildName, Is.Null);
			Assert.That(state.ServerMuted, Is.False);
			Assert.That(state.SelfSpeaking, Is.False);
			Assert.That(state.AveragePing, Is.Null);
		});
	}

	[Test]
	public void Leaving_a_channel_keeps_the_client_wide_settings()
	{
		var seeded = _connected with { SelfMuted = true, SelfDeafened = true, InputVolume = 55 };

		var state = seeded.WithoutVoiceChannel();

		Assert.Multiple(() =>
		{
			Assert.That(state.SelfMuted, Is.True);
			Assert.That(state.SelfDeafened, Is.True);
			Assert.That(state.InputVolume, Is.EqualTo(55));
		});
	}

	[Test]
	public void The_voice_connection_status_supplies_the_state_and_ping()
	{
		var state = DiscordStateMapper.ApplyVoiceConnectionStatus(_connected,
			Json("""{"state":"VOICE_CONNECTED","average_ping":37}"""));

		Assert.Multiple(() =>
		{
			Assert.That(state.VoiceConnectionState, Is.EqualTo("VOICE_CONNECTED"));
			Assert.That(state.AveragePing, Is.EqualTo(37));
		});
	}

	[Test]
	public void A_selected_channel_lists_its_members()
	{
		var state = DiscordStateMapper.ApplySelectedVoiceChannel(_connected,
			Json("""
				 {
				 	"id": "555",
				 	"name": "General",
				 	"guild_id": "999",
				 	"voice_states": [
				 		{ "user": { "id": "me", "username": "tester" }, "nick": "Tester", "volume": 100, "mute": false },
				 		{ "user": { "id": "u2", "username": "friend" }, "nick": "Friend", "volume": 80, "mute": false },
				 		{ "user": { "id": "u3", "username": "lurker" }, "volume": 110, "mute": true }
				 	]
				 }
				 """),
			guildName: "My Server");

		Assert.That(state.VoiceUsers.Select(u => u.UserId), Is.EqualTo(["me", "u2", "u3"]));
		Assert.Multiple(() =>
		{
			var me = state.VoiceUsers.Single(u => u.UserId == "me");
			Assert.That(me.Name, Is.EqualTo("Tester"), "the nick wins over the username");

			var friend = state.VoiceUsers.Single(u => u.UserId == "u2");
			Assert.That(friend.Name, Is.EqualTo("Friend"));
			Assert.That(friend.Volume, Is.EqualTo(80));
			Assert.That(friend.Muted, Is.False);

			var lurker = state.VoiceUsers.Single(u => u.UserId == "u3");
			Assert.That(lurker.Name, Is.EqualTo("lurker"), "the username stands in for a missing nick");
			Assert.That(lurker.Volume, Is.EqualTo(110));
			Assert.That(lurker.Muted, Is.True);
		});
	}

	[Test]
	public void A_channel_without_voice_states_lists_no_members()
	{
		var seeded = _connected with
		{
			VoiceUsers = [new DiscordVoiceUser { UserId = "u2", Volume = 80 }]
		};

		var state = DiscordStateMapper.ApplySelectedVoiceChannel(seeded,
			Json("""{"id":"new","name":"Other","guild_id":"999"}"""));

		Assert.That(state.VoiceUsers, Is.Empty, "members of the previous channel must not linger");
	}

	[Test]
	public void A_member_update_changes_only_that_member()
	{
		var seeded = _connected with
		{
			VoiceUsers =
			[
				new DiscordVoiceUser { UserId = "u2", Name = "Friend", Volume = 80 },
				new DiscordVoiceUser { UserId = "u3", Name = "Lurker", Volume = 110, Muted = true }
			]
		};

		var state = DiscordStateMapper.ApplyVoiceState(seeded,
			Json("""{"user":{"id":"u2","username":"friend"},"volume":90,"mute":true}"""));

		Assert.Multiple(() =>
		{
			var friend = state.VoiceUsers.Single(u => u.UserId == "u2");
			Assert.That(friend.Volume, Is.EqualTo(90));
			Assert.That(friend.Muted, Is.True);
			Assert.That(friend.Name, Is.EqualTo("friend"), "a fresh display name wins over the stored one");

			var lurker = state.VoiceUsers.Single(u => u.UserId == "u3");
			Assert.That(lurker.Volume, Is.EqualTo(110));
			Assert.That(lurker.Muted, Is.True);
		});

		var nameless = DiscordStateMapper.ApplyVoiceState(seeded, Json("""{"volume":50}"""));

		Assert.That(nameless, Is.EqualTo(seeded), "an entry without a user changes nothing");
	}

	[Test]
	public void A_first_sighting_of_a_member_adds_them()
	{
		var state = DiscordStateMapper.ApplyVoiceState(_connected,
			Json("""{"user":{"id":"u2","username":"friend"},"nick":"Friend","volume":80,"mute":false}"""));

		var friend = state.VoiceUsers.SingleOrDefault(u => u.UserId == "u2");

		Assert.Multiple(() =>
		{
			Assert.That(friend, Is.Not.Null);
			Assert.That(friend!.Name, Is.EqualTo("Friend"));
			Assert.That(friend.Volume, Is.EqualTo(80));
		});
	}

	[Test]
	public void A_member_delete_removes_the_member()
	{
		var seeded = _connected with
		{
			VoiceUsers =
			[
				new DiscordVoiceUser { UserId = "u2", Name = "Friend", Volume = 80 },
				new DiscordVoiceUser { UserId = "u3", Name = "Lurker", Volume = 110 }
			]
		};

		var state = DiscordStateMapper.ApplyVoiceStateDelete(seeded,
			Json("""{"user":{"id":"u2","username":"friend"},"nick":"Friend","volume":80,"mute":false}"""));

		Assert.That(state.VoiceUsers.Select(u => u.UserId), Is.EqualTo(["u3"]));
	}

	[Test]
	public void A_user_voice_settings_response_updates_the_member()
	{
		var seeded = _connected with
		{
			VoiceUsers = [new DiscordVoiceUser { UserId = "u2", Name = "Friend", Volume = 80 }]
		};

		var state = DiscordStateMapper.ApplyUserVoiceSettings(seeded,
			Json("""{"user_id":"u2","volume":90,"mute":true}"""));

		var friend = state.VoiceUsers.Single(u => u.UserId == "u2");

		Assert.Multiple(() =>
		{
			Assert.That(friend.Volume, Is.EqualTo(90));
			Assert.That(friend.Muted, Is.True);
			Assert.That(friend.Name, Is.EqualTo("Friend"));
		});
	}

	[Test]
	public void Leaving_a_channel_forgets_the_members()
	{
		var seeded = _connected with
		{
			VoiceUsers = [new DiscordVoiceUser { UserId = "u2", Volume = 80 }]
		};

		var state = seeded.WithoutVoiceChannel();

		Assert.That(state.VoiceUsers, Is.Empty);
	}

	[Test]
	public void The_user_is_read_from_a_ready_or_authenticate_payload()
	{
		var (id, name) = DiscordStateMapper.ReadUser(
			Json("""{"user":{"id":"7","username":"legacy","global_name":"Display Name"}}"""));

		Assert.Multiple(() =>
		{
			Assert.That(id, Is.EqualTo("7"));
			Assert.That(name, Is.EqualTo("Display Name"), "the display name is preferred over the handle");
		});
	}

	[Test]
	public void The_username_is_used_when_no_display_name_is_set()
	{
		var (_, name) = DiscordStateMapper.ReadUser(Json("""{"user":{"id":"7","username":"legacy"}}"""));

		Assert.That(name, Is.EqualTo("legacy"));
	}

	[Test]
	public void Granted_scopes_are_read_from_the_authenticate_payload()
	{
		var scopes = DiscordStateMapper.ReadScopes(Json("""{"scopes":["rpc","rpc.voice.read"]}"""));

		Assert.That(scopes, Is.EqualTo(_grantedScopes));
	}

	[Test]
	public void An_unexpected_payload_shape_is_survivable()
	{
		Assert.Multiple(() =>
		{
			Assert.That(DiscordStateMapper.ApplyVoiceSettings(_connected, Json("[]")), Is.EqualTo(_connected));
			Assert.That(DiscordStateMapper.ApplyVoiceState(_connected, Json("42")), Is.EqualTo(_connected));
			Assert.That(DiscordStateMapper.ApplyVoiceStateDelete(_connected, Json("42")), Is.EqualTo(_connected));
			Assert.That(DiscordStateMapper.ApplyUserVoiceSettings(_connected, Json("42")), Is.EqualTo(_connected));
			Assert.That(DiscordStateMapper.ReadScopes(Json("\"nope\"")), Is.Empty);
			Assert.That(DiscordStateMapper.ReadUser(Json("null")).Id, Is.Null);
		});
	}

	private static JsonElement Json(string json)
	{
		using var document = JsonDocument.Parse(json);
		return document.RootElement.Clone();
	}
}

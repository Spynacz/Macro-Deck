using MacroDeckHost.Integrations.Discord;

namespace MacroDeckHost.Tests.UnitTests.Discord;

[TestFixture]
internal sealed class DiscordAvatarStoreTests
{
	[Test]
	public void Known_avatars_survive_a_serialize_parse_round_trip()
	{
		var avatars = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["u2"] = "a1b2c3",
			["u3"] = "d4e5f6",
		};

		var parsed = DiscordAvatarStore.Parse(DiscordAvatarStore.Serialize(avatars));

		Assert.That(parsed, Is.EquivalentTo(avatars));
	}

	[TestCase(null)]
	[TestCase("")]
	[TestCase("not-json{{{")]
	[TestCase("[]")]
	[TestCase("""{"u2":42}""")]
	public void An_unreadable_store_reads_as_empty(string? raw)
	{
		Assert.That(DiscordAvatarStore.Parse(raw), Is.Empty);
	}

	[Test]
	public void Blank_entries_are_skipped()
	{
		var parsed = DiscordAvatarStore.Parse("""{"u2":"a1b2c3","":"x"," ":"y","u3":""}""");

		Assert.That(parsed, Is.EquivalentTo(new Dictionary<string, string> { ["u2"] = "a1b2c3" }));
	}

	[Test]
	public void An_oversized_store_is_capped()
	{
		var bloated = Enumerable.Range(0, 1200).ToDictionary(i => $"u{i}", i => $"hash{i}");

		var parsed = DiscordAvatarStore.Parse(DiscordAvatarStore.Serialize(bloated));

		Assert.That(parsed, Has.Count.EqualTo(1000));
	}
}

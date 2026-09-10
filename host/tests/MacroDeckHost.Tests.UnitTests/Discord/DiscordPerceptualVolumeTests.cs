using MacroDeckHost.Integrations.Discord;

namespace MacroDeckHost.Tests.UnitTests.Discord;

[TestFixture]
internal sealed class DiscordPerceptualVolumeTests
{
	[Test]
	public void The_endpoints_meet_where_the_scales_do()
	{
		Assert.Multiple(() =>
		{
			Assert.That(DiscordPerceptualVolume.ToRpc(0), Is.EqualTo(0));
			Assert.That(DiscordPerceptualVolume.ToRpc(100), Is.EqualTo(100));
			Assert.That(DiscordPerceptualVolume.ToRpc(200), Is.EqualTo(200));
			Assert.That(DiscordPerceptualVolume.ToDisplay(0), Is.EqualTo(0));
			Assert.That(DiscordPerceptualVolume.ToDisplay(100), Is.EqualTo(100));
			Assert.That(DiscordPerceptualVolume.ToDisplay(200), Is.EqualTo(200));
		});
	}

	[Test]
	public void A_mid_slider_position_maps_to_a_small_amplitude_and_back()
	{
		Assert.Multiple(() =>
		{
			Assert.That(DiscordPerceptualVolume.ToRpc(25), Is.EqualTo(2));
			Assert.That(DiscordPerceptualVolume.ToDisplay(2), Is.EqualTo(24.74).Within(0.01));
			Assert.That(DiscordPerceptualVolume.ToRpc(50), Is.EqualTo(14));
			Assert.That(DiscordPerceptualVolume.ToDisplay(14), Is.EqualTo(49.56).Within(0.01));
			Assert.That(DiscordPerceptualVolume.ToRpc(90), Is.EqualTo(74));
			Assert.That(DiscordPerceptualVolume.ToDisplay(74), Is.EqualTo(89.81).Within(0.01));
		});
	}

	[Test]
	public void Both_directions_clamp_instead_of_escaping_the_scale()
	{
		Assert.Multiple(() =>
		{
			Assert.That(DiscordPerceptualVolume.ToRpc(250), Is.EqualTo(200));
			Assert.That(DiscordPerceptualVolume.ToRpc(-5), Is.EqualTo(0));
			Assert.That(DiscordPerceptualVolume.ToDisplay(-3), Is.EqualTo(0));
			Assert.That(DiscordPerceptualVolume.ToDisplay(500), Is.EqualTo(200));
		});
	}

	[Test]
	public void Both_directions_rise_monotonically()
	{
		var rpc = 0;
		var display = 0d;

		for (var perceptual = 0d; perceptual <= 200d; perceptual += 5d)
		{
			var converted = DiscordPerceptualVolume.ToRpc(perceptual);
			Assert.That(converted, Is.GreaterThanOrEqualTo(rpc), $"ToRpc({perceptual})");
			rpc = converted;
		}

		for (var amplitude = 0d; amplitude <= 200d; amplitude += 5d)
		{
			var converted = DiscordPerceptualVolume.ToDisplay(amplitude);
			Assert.That(converted, Is.GreaterThanOrEqualTo(display), $"ToDisplay({amplitude})");
			display = converted;
		}
	}

	[Test]
	public void A_display_value_survives_the_round_trip_through_rpc()
	{
		foreach (var amplitude in new[] { 0d, 1d, 5d, 10d, 25d, 50d, 80d, 100d, 120d, 150d, 200d })
		{
			var display = DiscordPerceptualVolume.ToDisplay(amplitude);

			Assert.That(DiscordPerceptualVolume.ToDisplay(DiscordPerceptualVolume.ToRpc(display)),
				Is.EqualTo(display),
				$"amplitude {amplitude}");
		}
	}
}

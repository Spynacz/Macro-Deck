namespace MacroDeckHost.Integrations.Discord;

internal static class DiscordPerceptualVolume
{
	// Discord shows RPC amplitudes through a compressive curve, so a raw slider never matches the
	// client. At or below unity gain that curve is a power fitted to measured client readings, above
	// it a log boost that still meets 200, and the two agree at 100 from either side.
	private const double PowerExponent = 0.357;
	private const double BoostRangeDb = 6d;

	public static int ToRpc(double perceptual)
	{
		if (perceptual <= 0d)
		{
			return 0;
		}

		var amplitude = perceptual > 100d
			? 100d * Math.Pow(10d, (perceptual - 100d) / 100d * BoostRangeDb / 20d)
			: 100d * Math.Pow(perceptual / 100d, 1d / PowerExponent);
		return (int)Math.Clamp(Math.Round(amplitude, MidpointRounding.AwayFromZero), 0d, 200d);
	}

	public static double ToDisplay(double amplitude)
	{
		if (amplitude <= 0d)
		{
			return 0d;
		}

		if (amplitude <= 100d)
		{
			return Math.Clamp(100d * Math.Pow(amplitude / 100d, PowerExponent), 0d, 200d);
		}

		var db = 20d * Math.Log10(amplitude / 100d);
		return Math.Clamp(100d * (db / BoostRangeDb + 1d), 0d, 200d);
	}
}

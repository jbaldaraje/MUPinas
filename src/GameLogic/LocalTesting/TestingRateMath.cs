// <copyright file="TestingRateMath.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.LocalTesting;

/// <summary>Bounded calculations for reversible local test presets.</summary>
public static class TestingRateMath
{
    /// <summary>Multiplies a probability without producing more than one guaranteed occurrence.</summary>
    /// <param name="baseline">The original probability, between zero and one.</param>
    /// <param name="factor">The requested multiplier.</param>
    /// <returns>The probability capped at one.</returns>
    public static double DropProbability(double baseline, double factor)
    {
        if (!double.IsFinite(baseline) || baseline is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(baseline));
        }

        if (!double.IsFinite(factor) || factor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor));
        }

        return Math.Min(1, baseline * factor);
    }

    /// <summary>Bounds experience to the integer range used by combat and the protocol.</summary>
    /// <param name="experience">The computed non-negative experience.</param>
    /// <returns>The safe experience amount.</returns>
    public static int Experience(double experience)
        => double.IsNaN(experience) || experience <= 0 ? 0 : (int)Math.Min(int.MaxValue, experience);
}

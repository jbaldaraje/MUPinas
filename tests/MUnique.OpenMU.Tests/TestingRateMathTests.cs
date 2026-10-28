// <copyright file="TestingRateMathTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.LocalTesting;

/// <summary>Verifies probability caps and experience conversion at high testing rates.</summary>
[TestFixture]
public class TestingRateMathTests
{
    /// <summary>The drop factor applies to probabilities rather than item count.</summary>
    /// <param name="baseline">The starting probability.</param>
    /// <param name="expected">The bounded probability.</param>
    [TestCase(0.0001, 0.1)]
    [TestCase(0.001, 1)]
    [TestCase(0.3, 1)]
    [TestCase(0, 0)]
    public void ProbabilityIsBounded(double baseline, double expected)
        => Assert.That(TestingRateMath.DropProbability(baseline, 1000), Is.EqualTo(expected).Within(0.000001));

    /// <summary>Experience cannot wrap to a negative signed integer.</summary>
    /// <param name="value">The pre-conversion experience.</param>
    /// <param name="expected">The bounded integer.</param>
    [TestCase(1e15, int.MaxValue)]
    [TestCase(10000, 10000)]
    [TestCase(-1, 0)]
    [TestCase(double.NaN, 0)]
    [TestCase(double.PositiveInfinity, int.MaxValue)]
    public void ExperienceDoesNotOverflow(double value, int expected)
        => Assert.That(TestingRateMath.Experience(value), Is.EqualTo(expected));

    /// <summary>Invalid rates are rejected rather than silently altering game balance.</summary>
    [Test]
    public void InvalidProbabilitiesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TestingRateMath.DropProbability(-0.1, 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => TestingRateMath.DropProbability(0.1, double.NaN));
    }
}

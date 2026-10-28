// <copyright file="TestingMapPlan.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.LocalTesting;

using MUnique.OpenMU.Pathfinding;

/// <summary>A side-effect-free plan of generated monsters on one configured map.</summary>
public sealed class TestingMapPlan
{
    /// <summary>Gets or sets the client map number.</summary>
    public short Map { get; set; }

    /// <summary>Gets or sets the requested population.</summary>
    public int Requested { get; set; }

    /// <summary>Gets or sets a machine-readable exclusion or shortfall code.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets the validated spawn points, grouped in insertion order.</summary>
    public List<Point> Positions { get; } = [];
}

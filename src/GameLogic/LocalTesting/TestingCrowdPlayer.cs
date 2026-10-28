// <copyright file="TestingCrowdPlayer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.LocalTesting;

using MUnique.OpenMU.GameLogic.Offline;
using MUnique.OpenMU.Pathfinding;

/// <summary>Reuses the existing offline-player entity and optional MU Helper combat without world-wide bot navigation.</summary>
internal sealed class TestingCrowdPlayer : OfflinePlayer
{
    private readonly string _behavior;
    private readonly WalkingStep[] _step = new WalkingStep[1];

    /// <summary>Initializes a new instance of the <see cref="TestingCrowdPlayer"/> class.</summary>
    /// <param name="context">The running game server.</param>
    /// <param name="behavior">The validated crowd behavior.</param>
    public TestingCrowdPlayer(IGameContext context, string behavior)
        : base(context)
    {
        this._behavior = behavior;
    }

    /// <summary>Walks one validated step with a buffer owned by this character's walker.</summary>
    /// <param name="target">The validated adjacent target.</param>
    public ValueTask WalkOneStepAsync(Point target)
    {
        this._step[0] = new WalkingStep(this.Position, target, this.Position.GetDirectionTo(target));
        return this.WalkToAsync(target, this._step);
    }

    /// <summary>Starts combat only after the offline-player world entry has finished.</summary>
    public void StartBehavior()
    {
        if (this._behavior == "combat")
        {
            base.StartIntelligence();
        }
    }

    /// <inheritdoc />
    protected override void StartIntelligence()
    {
        // Crowd construction activates behavior after the entity is fully registered.
    }
}

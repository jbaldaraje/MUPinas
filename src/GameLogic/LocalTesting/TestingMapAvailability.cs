// <copyright file="TestingMapAvailability.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.LocalTesting;

/// <summary>Checks hosted configuration in the installed revision, which predates per-map IsActive flags.</summary>
internal static class TestingMapAvailability
{
    /// <summary>Checks that an ordinary map belongs to both the game and the current server.</summary>
    /// <param name="context">The running game context.</param>
    /// <param name="number">The client map number.</param>
    /// <returns>Whether the map is hosted by this server.</returns>
    public static bool IsHosted(IGameContext context, short number)
        => context.Configuration.Maps.Any(m => m.Number == number && m.Discriminator == 0)
            && (context is not IGameServerContext server || server.ServerConfiguration.Maps.Any(m => m.Number == number && m.Discriminator == 0));
}

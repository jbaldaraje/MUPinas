// <copyright file="DynamicStatLimitTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.LocalTesting;
using MUnique.OpenMU.GameLogic.PlayerActions.Character;

/// <summary>Verifies limits against actual stat-allocation actions and the privileged settings entry point.</summary>
[TestFixture]
public class DynamicStatLimitTests
{
    /// <summary>Lowering a cap below an existing stat must neither wrap the allocation amount nor deduct points.</summary>
    [Test]
    public async Task LoweredCapPreservesExistingStatAndPointsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.SelectedCharacter!.LevelUpPoints = 100;
        player.Attributes![Stats.BaseStrength] = 40000;
        var stat = player.SelectedCharacter.CharacterClass!.StatAttributes.Single(s => s.Attribute == Stats.BaseStrength);
        stat.Attribute = new AttributeDefinition { Id = Stats.BaseStrength.Id, MaximumValue = 100 };
        await new IncreaseStatsAction().IncreaseStatsAsync(player, Stats.BaseStrength, 10).ConfigureAwait(false);
        Assert.That(player.Attributes[Stats.BaseStrength], Is.EqualTo(40000));
        Assert.That(player.SelectedCharacter.LevelUpPoints, Is.EqualTo(100));
    }

    /// <summary>Server-side authorization cannot be bypassed by invoking the feature directly.</summary>
    [Test]
    public async Task OrdinaryPlayerCannotSaveTestingSettingsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var feature = new LocalTestingFeaturePlugIn();
        Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await feature.SaveAsync(player, settings => settings.MaximumAttackSpeed = 800).ConfigureAwait(false));
        Assert.That(feature.Configuration, Is.Null);
    }

    /// <summary>Unchecking the feature restores its captured baseline even though periodic callbacks stop.</summary>
    [Test]
    public async Task DeactivatingPluginRestoresBaselineAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var context = (GameContext)player.GameContext;
        var game = context.Configuration;
        game.ExperienceRate = 9999;
        var speed = new AttributeDefinition { Id = Stats.AttackSpeed.Id, MaximumValue = 200 };
        Mock.Get(game).Setup(g => g.Attributes).Returns(new List<AttributeDefinition> { speed });
        Mock.Get(game).Setup(g => g.DropItemGroups).Returns(new List<DropItemGroup>());
        context.PlugInManager.RegisterPlugIn<IFeaturePlugIn, LocalTestingFeaturePlugIn>();
        context.PlugInManager.ActivatePlugIn<LocalTestingFeaturePlugIn>();
        var feature = new LocalTestingFeaturePlugIn();
        context.FeaturePlugIns.AddPlugIn(feature, true);
        var settings = new LocalTestingConfiguration();
        feature.Configuration = settings;
        await feature.ExecuteTaskAsync(context).ConfigureAwait(false);
        TestingPreset.Capture(game, settings);
        Assert.That(settings.CapBaselines[speed.Id], Is.EqualTo(200));
        settings.Enabled = true;
        TestingPreset.Apply(game, settings);
        Assert.That(game.ExperienceRate, Is.EqualTo(10000));
        context.PlugInManager.DeactivatePlugIn<LocalTestingFeaturePlugIn>();
        Assert.That(game.ExperienceRate, Is.EqualTo(9999));
        Assert.That(speed.MaximumValue, Is.EqualTo(200));
        await feature.ExecuteTaskAsync(context).ConfigureAwait(false);
        Assert.That(speed.MaximumValue, Is.EqualTo(200), "A callback already queued before deactivation must not reapply the preset.");
        await context.DisposeAsync().ConfigureAwait(false);
    }
}

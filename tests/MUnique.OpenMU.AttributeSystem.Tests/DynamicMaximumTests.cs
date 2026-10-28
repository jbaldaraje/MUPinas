// <copyright file="DynamicMaximumTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.AttributeSystem.Tests;

/// <summary>Verifies editable limits without requiring a stat mutation or reconnect.</summary>
[TestFixture]
public class DynamicMaximumTests
{
    /// <summary>Applying a cap must not change the getter used to persist previously earned points.</summary>
    [Test]
    public void StoredStatsRemainRawWhileInterfaceReadsAreCapped()
    {
        var definition = new AttributeDefinition { MaximumValue = 65535 };
        var stored = new StatAttribute(definition, 128226);
        IAttribute effective = stored;
        Assert.That(effective.Value, Is.EqualTo(65535));
        Assert.That(stored.Value, Is.EqualTo(128226));
        definition.MaximumValue = 32000;
        Assert.That(effective.Value, Is.EqualTo(32000));
        Assert.That(stored.Value, Is.EqualTo(128226));
        definition.MaximumValue = null;
        Assert.That(effective.Value, Is.EqualTo(128226));
    }

    /// <summary>Increasing and decreasing a cap must not latch the previous capped cached value.</summary>
    [Test]
    public void ChangesTakeEffectAgainstCachedRawValue()
    {
        var definition = new AttributeDefinition { MaximumValue = 200 };
        var attribute = new ComposableAttribute(definition);
        attribute.AddElement(new ConstantElement(1200));
        Assert.That(attribute.Value, Is.EqualTo(200));
        definition.MaximumValue = 800;
        Assert.That(attribute.Value, Is.EqualTo(800));
        definition.MaximumValue = 100;
        Assert.That(attribute.Value, Is.EqualTo(100));
        definition.MaximumValue = null;
        Assert.That(attribute.Value, Is.EqualTo(1200));
    }
}

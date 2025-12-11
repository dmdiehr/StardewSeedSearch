using StardewSeedSearch.Core;
using Xunit;

namespace StardewSeedSearch.Tests;

public class GameSeedFactoryTests
{
    [Fact]
    public void SameInputs_ProduceSameSeed()
    {
        int s1 = GameSeedFactory.ForDailyFeature(123456789, 10);
        int s2 = GameSeedFactory.ForDailyFeature(123456789, 10);

        Assert.Equal(s1, s2);
    }

    [Fact]
    public void DifferentInputs_ProduceDifferentSeeds()
    {
        int s1 = GameSeedFactory.ForDailyFeature(123456789, 10);
        int s2 = GameSeedFactory.ForDailyFeature(123456789, 11);

        Assert.NotEqual(s1, s2);
    }
}

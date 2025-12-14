using System.Collections.Generic;

namespace StardewSeedSearch.Core;

public sealed class RandomBundleData
{
    public string AreaName { get; set; } = "";
    public string Keys { get; set; } = "";
    public List<BundleSetData> BundleSets { get; set; } = new();
    public List<BundleData> Bundles { get; set; } = new();
}

public sealed class BundleSetData
{
    public string Id { get; set; } = "";
    public List<BundleData> Bundles { get; set; } = new();
}

public sealed class BundleData
{
    public string Name { get; set; } = "";
    public int Index { get; set; }
    public string Sprite { get; set; } = "";
    public string Color { get; set; } = "";
    public string Items { get; set; } = "";
    public int Pick { get; set; }
    public int RequiredItems { get; set; }
    public string Reward { get; set; } = "";
}

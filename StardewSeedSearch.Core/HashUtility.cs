using System;
using System.Text;
using System.IO.Hashing;

namespace StardewSeedSearch.Core;

public static class HashUtility
{
    public static int GetDeterministicHashCode(params int[] values)
    {
        var data = new byte[values.Length * 4];
        Buffer.BlockCopy(values, 0, data, 0, data.Length);

        uint hash = XxHash32.HashToUInt32(data);
        return unchecked((int)hash);
    }

    public static int GetDeterministicHashCode(string value)
    {
        var data = Encoding.UTF8.GetBytes(value);
        uint hash = XxHash32.HashToUInt32(data);
        return unchecked((int)hash);
    }
}

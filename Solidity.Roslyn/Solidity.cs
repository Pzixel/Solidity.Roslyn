using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;

namespace Solidity.Roslyn
{
    internal static class Solidity
    {
        public static ImmutableDictionary<string, Type> SolidityTypesToCsTypes { get; } = new Dictionary<string, Type>
        {
            ["bool"] = typeof(bool),
            ["int8"] = typeof(sbyte),
            ["uint8"] = typeof(byte),
            ["int16"] = typeof(short),
            ["uint16"] = typeof(ushort),
            ["int32"] = typeof(int),
            ["uint32"] = typeof(uint),
            ["int64"] = typeof(long),
            ["uint64"] = typeof(ulong),
            ["int128"] = typeof(BigInteger),
            ["uint128"] = typeof(BigInteger),
            ["int256"] = typeof(BigInteger),
            ["uint256"] = typeof(BigInteger),
            ["address"] = typeof(string),
            ["byte"] = typeof(byte[]),
            ["bytes1"] = typeof(byte[]),
            ["bytes2"] = typeof(byte[]),
            ["bytes3"] = typeof(byte[]),
            ["bytes4"] = typeof(byte[]),
            ["bytes5"] = typeof(byte[]),
            ["bytes6"] = typeof(byte[]),
            ["bytes7"] = typeof(byte[]),
            ["bytes8"] = typeof(byte[]),
            ["bytes9"] = typeof(byte[]),
            ["bytes10"] = typeof(byte[]),
            ["bytes11"] = typeof(byte[]),
            ["bytes12"] = typeof(byte[]),
            ["bytes13"] = typeof(byte[]),
            ["bytes14"] = typeof(byte[]),
            ["bytes15"] = typeof(byte[]),
            ["bytes16"] = typeof(byte[]),
            ["bytes17"] = typeof(byte[]),
            ["bytes18"] = typeof(byte[]),
            ["bytes19"] = typeof(byte[]),
            ["bytes20"] = typeof(byte[]),
            ["bytes21"] = typeof(byte[]),
            ["bytes22"] = typeof(byte[]),
            ["bytes23"] = typeof(byte[]),
            ["bytes24"] = typeof(byte[]),
            ["bytes25"] = typeof(byte[]),
            ["bytes26"] = typeof(byte[]),
            ["bytes27"] = typeof(byte[]),
            ["bytes28"] = typeof(byte[]),
            ["bytes29"] = typeof(byte[]),
            ["bytes30"] = typeof(byte[]),
            ["bytes31"] = typeof(byte[]),
            ["bytes32"] = typeof(byte[]),
            ["bytes"] = typeof(byte[]),
            ["string"] = typeof(string)
        }.ToImmutableDictionary();
    }
}

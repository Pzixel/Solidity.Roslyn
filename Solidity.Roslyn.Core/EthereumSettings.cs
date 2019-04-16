using Nethereum.Hex.HexTypes;

namespace Solidity.Roslyn
{
    public static class EthereumSettings
    {
        public static HexBigInteger TxGas { get; set; } = new HexBigInteger(4700000);
        public static HexBigInteger DeploymentGas { get; set; } = new HexBigInteger(4700000);
    }
}

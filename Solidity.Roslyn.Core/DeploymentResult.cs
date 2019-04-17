using Nethereum.RPC.Eth.DTOs;

namespace Solidity.Roslyn.Core
{
    public struct DeploymentResult<T>
    {
        public T Value { get; }
        public TransactionReceipt Receipt { get; }

        public DeploymentResult(T value, TransactionReceipt receipt)
        {
            Value = value;
            Receipt = receipt;
        }
    }
}

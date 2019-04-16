using Nethereum.RPC.Eth.DTOs;

namespace Solidity.Roslyn
{
    public struct DeploymentResult<T> where T : ContractBase
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

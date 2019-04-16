using System;
using Nethereum.RPC.Eth.DTOs;

namespace Solidity.Roslyn.Core
{
    public class TransactionFailedException : Exception
    {
        public TransactionFailedException(TransactionReceipt transactionReceipt)
        {
            TransactionReceipt = transactionReceipt;
        }

        public TransactionFailedException(string message, TransactionReceipt transactionReceipt) : base(message)
        {
            TransactionReceipt = transactionReceipt;
        }

        public TransactionReceipt TransactionReceipt { get; }
    }
}

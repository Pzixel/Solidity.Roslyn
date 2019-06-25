using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Solidity.Roslyn.Core
{
    public static class XContractFunction
    {
        public static async Task<TransactionReceipt> SendDefaultTransactionAndWaitForReceiptAsync(this Function function,
                                                                                                  string accountAddress,
                                                                                                  HexBigInteger gas = null,
                                                                                                  params object[] functionInput)
        {
            var result = await function.SendTransactionAndWaitForReceiptAsync(
                             accountAddress,
                             gas ?? EthereumSettings.TxGas,
                             new HexBigInteger(0),
                             functionInput: functionInput);

            if (HasErrors(result) ?? false)
            {
                throw new TransactionFailedException(result);
            }

            return result;
        }

        private static bool? HasErrors(TransactionReceipt receipt)
        {
            if (receipt.Status?.HexValue == null)
                return new bool?();
            return receipt.Status.Value == 0L;
        }
    }
}

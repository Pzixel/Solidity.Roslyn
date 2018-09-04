using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Solidity.Roslyn
{
    public static class XContractFunction
    {
        public static async Task<TransactionReceipt> SendDefaultTransactionAndWaitForReceiptAsync(this Function function,
                                                                                                  string accountAddress,
                                                                                                  params object[] functionInput)
        {
            var result = await function.SendTransactionAndWaitForReceiptAsync(
                             accountAddress,
                             EthereumSettings.TxGas,
                             new HexBigInteger(0),
                             functionInput: functionInput);

            if (result.Status.Value != BigInteger.One)
            {
                throw new TransactionFailedException(result);
            }

            return result;
        }
    }
}

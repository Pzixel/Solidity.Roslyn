using Nethereum.RPC.TransactionManagers;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.Web3;
using Nethereum.Web3.Accounts.Managed;

namespace Solidity.Roslyn.Test.Integrational
{
    public static class XWeb3
    {
        public static Web3 GetInstance(string parityConnectionString)
        {
            return new Web3(parityConnectionString);
        }

        public static Web3 GetInstance(string parityConnectionString, string address, string password)
        {
            var account = new ManagedAccount(address, password);
            if (account.TransactionManager is TransactionManagerBase manager) // should always succeed
            {
                manager.TransactionReceiptService = new TransactionReceiptPollingService(manager, 1000);
            }

            return new Web3(account, parityConnectionString);
        }
    }
}

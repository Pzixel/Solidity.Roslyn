using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace Solidity.Roslyn
{
    [DebuggerDisplay("{" + nameof(Address) + "}")]
    public abstract class ContractBase : IEquatable<ContractBase>
    {
        private const string EmptyAddress = "0x0000000000000000000000000000000000000000";

        protected ContractBase(Web3 web3, string abi, string address)
        {
            if (string.IsNullOrEmpty(abi))
            {
                throw new ArgumentException($"Abi {abi} is empty");
            }

            if (IsEmptyAddress(address))
            {
                throw new ArgumentException($"Adress '{address}' is empty");
            }

            Web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            Contract = Web3.Eth.GetContract(abi, Address);
            Address = address;
        }

        protected Web3 Web3 { get; }

        public string Address { get; }

        protected Nethereum.Contracts.Contract Contract { get; }

        public bool Equals(ContractBase other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Web3.Equals(other.Web3) && string.Equals(Address, other.Address);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((ContractBase) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Web3.GetHashCode() * 397) ^ Address.GetHashCode();
            }
        }

        public static bool operator ==(ContractBase left, ContractBase right) => Equals(left, right);

        public static bool operator !=(ContractBase left, ContractBase right) => !Equals(left, right);

        public static bool IsEmptyAddress(string address) => string.IsNullOrEmpty(address) || address == EmptyAddress;

        protected static Task<TransactionReceipt> DeployAsync(Web3 web3, string abi, string bin, object[] arguments)
        {
            return web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                abi,
                bin,
                web3.TransactionManager.Account.Address,
                EthereumSettings.DeploymentGas,
                values: arguments);
        }
    }
}

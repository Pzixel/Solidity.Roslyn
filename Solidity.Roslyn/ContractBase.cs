using System;
using System.Diagnostics;

namespace Solidity.Roslyn
{
    [DebuggerDisplay("{" + nameof(Address) + "}")]
    public abstract class ContractBase : IEquatable<ContractBase>
    {
        private const string EmptyAddress = "0x0000000000000000000000000000000000000000";
        protected readonly IEthereumHandler EthereumHandler;

        protected ContractBase(IEthereumHandler ethereumHandler, string address)
        {
            if (IsEmptyAddress(address))
            {
                throw new ArgumentException($"Cannot create contract for empty address '{address}'");
            }

            Address = address;
            EthereumHandler = ethereumHandler ?? throw new ArgumentNullException(nameof(ethereumHandler));
        }

        public string Address { get; }
        
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

            return EthereumHandler.Equals(other.EthereumHandler) && string.Equals(Address, other.Address);
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

            return Equals((ContractBase)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EthereumHandler.GetHashCode() * 397) ^ Address.GetHashCode();
            }
        }

        public static bool operator ==(ContractBase left, ContractBase right) => Equals(left, right);

        public static bool operator !=(ContractBase left, ContractBase right) => !Equals(left, right);

        public static bool IsEmptyAddress(string address) => string.IsNullOrEmpty(address) || address == EmptyAddress;
    }
}

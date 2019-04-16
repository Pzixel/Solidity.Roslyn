using System.Threading.Tasks;

namespace Solidity.Roslyn
{
    public interface IEthereumHandler
    {
        Task<T> CallAsync<T>(string address, string abi, string functionName, params object[] args);
        Task<T> CallDeserializingToObjectAsync<T>(string address, string abi, string functionName, params object[] args);
        Task SendTransactionAsync<T>(string address, string abi, string functionName, params object[] args);
        Task DeployAsync<T>(string bin, string abi, params object[] args);
    }
}

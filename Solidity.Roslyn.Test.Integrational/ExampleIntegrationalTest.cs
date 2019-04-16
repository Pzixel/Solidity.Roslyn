using System.Threading.Tasks;
using Nethereum.Web3;
using Solidity.Roslyn.Core;
using Solidity.Roslyn.Example.Sample;
using Xunit;

namespace Solidity.Roslyn.Test.Integrational
{
    public class ExampleIntegrationalTest
    {
        private readonly Web3 Web3;

        public ExampleIntegrationalTest()
        {
            Web3 = XWeb3.GetInstance(EthereumSettings.ParityConnectionString,
                                     EthereumSettings.AccountAddress,
                                     EthereumSettings.AccountPassword);
        }

        [Fact]
        public async Task Should_Deploy()
        {
            const ulong xValue = 10;
            const ulong yValue = 20;

            var sample = await SampleContract.DeployAsync(Web3,
                                                          xValue,
                                                          yValue);

            ulong x = await sample.XAsync();
            ulong y = await sample.YAsync();

            Assert.Equal(xValue, x);
            Assert.Equal(yValue, y);
        }

        [Fact]
        public async Task Should_ThrowExceptions()
        {
            const ulong xValue = 10;
            const ulong yValue = 20;

            var sample = await SampleContract.DeployAsync(Web3, xValue, yValue);

            await sample.ThrowIfNotEqualAsync(5, 5);

            await Assert.ThrowsAsync<TransactionFailedException>(() => sample.ThrowIfNotEqualAsync(5, 30));
        }
    }
}

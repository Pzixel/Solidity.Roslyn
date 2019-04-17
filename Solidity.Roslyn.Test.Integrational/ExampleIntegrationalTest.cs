using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Solidity.Roslyn.Core;
using Solidity.Roslyn.Example.Sample;
using Xunit;

namespace Solidity.Roslyn.Test.Integrational
{
    public class ExampleIntegrationalTest
    {
        private const ulong X = 10;
        private const ulong Y = 20;
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
            var sample = await GetSampleContract();

            ulong x = await sample.XAsync();
            ulong y = await sample.YAsync();

            Assert.Equal(X, x);
            Assert.Equal(Y, y);
        }

        [Fact]
        public async Task Should_CallEmptyMethod()
        {
            var sample = await GetSampleContract();

            await sample.NoParamsAsync();
        }

        [Fact]
        public async Task Should_CallBase()
        {
            var sample = await GetSampleContract();

            bool isDeployed = await sample.IsDeployedAsync();
            Assert.True(isDeployed);
        }

        [Fact]
        public async Task Should_ThrowExceptions()
        {
            var sample = await GetSampleContract();

            await sample.ThrowIfNotEqualAsync(5, 5);
            await Assert.ThrowsAsync<TransactionFailedException>(() => sample.ThrowIfNotEqualAsync(5, 30));
        }

        [Fact]
        public async Task Should_CallIdentity()
        {
            var sample = await GetSampleContract();

            var value = new BigInteger(100500);
            var result = await sample.IdentityAsync(value);
            Assert.Equal(value, result);
        }

        [Fact]
        public async Task Should_TestTuplePartialNames()
        {
            var sample = await GetSampleContract();

            var result = await sample.TestTuplePartialNamesAsync(10, 20, 30);
            Assert.Equal(10, result.M);
            Assert.Equal(20, result.N);
            Assert.Equal(30, result.Property3);
        }

        [Fact]
        public async Task Should_ReceiveMultiple()
        {
            var sample = await GetSampleContract();

            var testStrings = Enumerable.Range(1, 10)
                                  .Select(i => $"{new string('1', 29)}{i}")
                                  .ToArray();
            var result = await sample.ReceiveMultipleAsync(
                             new ulong[] { 10, 20 },
                             testStrings
                                 .Select(Encoding.ASCII.GetBytes)
                                 .ToArray());

            Assert.Equal(testStrings, result.Select(Encoding.ASCII.GetString));
        }

        [Fact]
        public async Task Should_ReturnMultiple()
        {
            var sample = await GetSampleContract();

            var result = await sample.ReturnMultipleAsync();
            Assert.Single(result.Property1);
            Assert.Equal(3, result.Property2.Count);
        }

        [Fact]
        public async Task Should_Greet()
        {
            var sample = await GetSampleContract();

            var mostRecentBlockBeforeIndexingStarted = await Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var countBefore = await sample.GreetCountAsync();
            await sample.GreetAsync();
            var countAfter = await sample.GreetCountAsync();
            var greetEvent = sample.GetGreetingEvent();
            var eventLogs = await greetEvent.GetAllChanges(
                                greetEvent.CreateFilterInput(
                                    fromBlock: new BlockParameter(mostRecentBlockBeforeIndexingStarted)));

            Assert.Equal(0, countBefore);
            Assert.Equal(1, countAfter);
            Assert.Single(eventLogs);
            Assert.Equal(0, eventLogs[0].Event.GreetId);
            Assert.Equal("Hello", eventLogs[0].Event.Text);
        }


        [Fact]
        public async Task Should_CallSideEffectFunction()
        {
            var sample = await GetSampleContract();

            var value = await sample.SideEffectFunctionWithReturnValueAsync();
            Assert.Equal(1, value);
        }

        private Task<SampleContract> GetSampleContract() => SampleContract.DeployAsync(Web3, X, Y);
    }
}

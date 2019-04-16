using Solidity.Roslyn.Example.Sample;
using Xunit;

namespace Solidity.Roslyn.Test
{
    public class ContractTest
    {
        [Fact]
        public void AbiTest()
        {
            Assert.NotNull(Owned.Abi);
            Assert.NotNull(SampleContract.Abi);
        }
    }
}

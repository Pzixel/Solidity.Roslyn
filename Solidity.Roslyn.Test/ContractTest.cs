using Example;
using Solidity.Roslyn;
using Xunit;

[assembly: Solidity]

namespace Solidity.Roslyn.Test
{
    public class ContractTest
    {
        [Fact]
        public void Foo()
        {
            const string greeterAbi =
                "[{\"constant\":false,\"inputs\":[],\"name\":\"kill\",\"outputs\":[],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"greet\",\"outputs\":[{\"name\":\"\",\"type\":\"string\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"name\":\"_greeting\",\"type\":\"string\"}],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"constructor\"}]";
            const string mortalAbi =
                "[{\"constant\":false,\"inputs\":[],\"name\":\"kill\",\"outputs\":[],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"constructor\"}]";

            Assert.Equal(greeterAbi, Greeter.Abi);
            Assert.NotEmpty(Greeter.Bin);
            Assert.Equal(mortalAbi, Mortal.Abi);
            Assert.NotEmpty(Mortal.Bin);
        }
    }
}

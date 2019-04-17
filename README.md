# Solidity.Roslyn
[![CircleCI](https://circleci.com/gh/Pzixel/Solidity.Roslyn/tree/master.svg?style=svg)](https://circleci.com/gh/Pzixel/Solidity.Roslyn/tree/master)

This library allows you to generate C# wrapper over Solidity contracts, targeting netstandard library. 
It requires `solc` available in PATH when building.
You can install it for your OS ([see instruction](https://solidity.readthedocs.io/en/v0.4.25/installing-solidity.html)) or use prebuilt [docker image](https://hub.docker.com/r/pzixel/solidity-dotnet/).

![image](https://user-images.githubusercontent.com/11201122/56217716-604e7600-606c-11e9-960e-3ee7fa097f3d.png)


# Sample usage

1. Create a new netstandard project and add solidity file with code

```solidity
contract SampleContract {
    uint64 public x;
    uint64 public y;

    constructor(uint64 x_, uint64 y_) public {
        x = x_;
        y = y_;
    }
}
```

2. Reference `Solidity.Roslyn`, `Solidity.Roslyn.Core` and required build packages
```powershell
Install-Package Solidity.Roslyn
Install-Package Solidity.Roslyn.Core
Install-Package CodeGeneration.Roslyn.BuildTime -Version 0.4.88
```

3. Add codegen attribute in the project

```cs
using Solidity.Roslyn;

[assembly: Solidity]
```

4. Build the project and start calling the contract!:

```cs
const ulong X = 10;
const ulong Y = 20;
var sample = await SampleContract.DeployAsync(Web3, X, Y);
ulong x = await sample.XAsync();
ulong y = await sample.YAsync();

Assert.Equal(X, x);
Assert.Equal(Y, y);
```

For more details see `Example` project

Also you may find [Solidity code highlighter](https://github.com/Pzixel/Solidity) useful.

[This playground](https://github.com/orbita-center/parity-poa-playground/) was used to run integrational tests. If you have different connection settings put them in `EthereumSettings` class in `Solidity.Roslyn.Test.Integrational` project.

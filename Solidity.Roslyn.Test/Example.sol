pragma solidity ^0.4.24;

contract mortal {
    /* Define variable owner of the type address */
    address owner;

    /* This function is executed at initialization and sets the owner of the contract */
    function mortal(uint64 a, uint b) public {
        owner = msg.sender;
    }

    /* Function to recover the funds on the contract */
    function kill() public {
        if (msg.sender == owner) {
            selfdestruct(owner);
        }
    }

    function testTx(int a, uint b) public {
    }

    function testValue(int a) public returns (string) {
        return "";
    }

    function testTuple(uint16 a, int8 b, int64 d) public returns (int16 x, uint8 y, uint64) {
        return (1,2,3);
    }
}

contract greeter is mortal {
    /* Define variable greeting of the type string */
    string greeting;

    /* This runs when the contract is executed */
    function greeter(string _greeting) public {
        greeting = _greeting;
    }

    /* Main function */
    function greet() public constant returns (string) {
        return greeting;
    }
}

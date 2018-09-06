pragma solidity ^0.4.24;

contract baseContract {
    /* Define variable owner of the type address */
    address public owner;
    uint64 public a;
    uint64 public b;

    /* This function is executed at initialization and sets the owner of the contract */
    function mortal(uint64 a_, uint64 b_) public {
        owner = msg.sender;
        a = a_;
        b = b_;
    }

    /* Function to recover the funds on the contract */
    function noParams() public {
    }

    function throwIfNotEqual(int a, int b) public {
        require (a == b);
    }

    function testSimpleValue(int a) public returns (int) {
        return a;
    }

    function testTuplePartialNames(uint16 a, int8 b, int64 d) public returns (int16 x, uint8 y, uint64) {
        return (1,2,3);
    }

    function receiveMultiple(uint64[] xs, bytes32[] ys) public pure {
        require (xs.length > 0);
        require (ys.length > 0);
    }

    function returnMultiple() public pure returns (uint64[], bytes) {
        return (new uint64[](1), new bytes(3));
    }
}

contract derivedContract is baseContract {
    /* Define variable greeting of the type string */
    string public greeting;

    /* This runs when the contract is executed */
    function greeter(string _greeting) public {
        greeting = _greeting;
    }

    /* Main function */
    function greet() public view returns (string) {
        return greeting;
    }
}

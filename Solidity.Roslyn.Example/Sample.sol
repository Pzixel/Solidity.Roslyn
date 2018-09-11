pragma solidity ^0.4.24;

contract Owned {
    address public owner;

    constructor() public {
        owner = tx.origin;
    }

    modifier onlyOwner {
        require(msg.sender == owner);
        _;
    }

    function isDeployed() public pure returns (bool) {
        return true;
    }
}

contract SampleContract is Owned {
    address public owner;
    uint64 public x;
    uint64 public y;
    uint public greetCount;
    event Greet(uint indexed greetId, string text);

    constructor(uint64 x_, uint64 y_) public {
        owner = msg.sender;
        x = x_;
        y = y_;
    }

    function noParams() public pure {
    }

    function throwIfNotEqual(int a, int b) public pure {
        require (a == b);
    }

    function testSimpleValue(int a) public pure returns (int) {
        return a;
    }

    function testTuplePartialNames(uint8 a, uint16 b, uint32 d) public pure returns (int16 m, int32 n, int64) {
        return (int16(a), int32(b), int64(d));
    }

    function receiveMultiple(uint64[] xs, bytes32[] ys) public pure {
        require (xs.length > 0);
        require (ys.length > 0);
    }

    function returnMultiple() public pure returns (uint64[], bytes32[]) {
        return (new uint64[](1), new bytes32[](3));
    }

    function greet() public {
        emit Greet(greetCount, "Hello");
        greetCount++;
    }
}
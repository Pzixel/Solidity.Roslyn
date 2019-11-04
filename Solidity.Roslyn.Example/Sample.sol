pragma solidity ^0.5.2;

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

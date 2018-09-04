using System.Collections.Generic;

namespace Solidity.Roslyn
{
    public class SolcOutput
    {
        public string Version { get; set; }
        public Dictionary<string, Contract> Contracts { get; set; }
    }

    public class Contract
    {
        public string Abi { get; set; }
        public string Bin { get; set; }
    }

    public class Abi
    {
        public string Name { get; set; }
        public MemberType Type { get; set; }
        public Parameter[] Inputs { get; set; }
        public Parameter[] Outputs { get; set; }
    }

    public enum MemberType
    {
        Function,
        Constructor
    }

    public class Parameter
    {
        public string Name { get; set; }

        public string Type { get; set; }
    }
}

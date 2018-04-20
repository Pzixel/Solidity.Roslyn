using System;
using System.Diagnostics;
using CodeGeneration.Roslyn;

namespace Solidity.Roslyn
{
    [AttributeUsage(AttributeTargets.Assembly)]
    [CodeGenerationAttribute(typeof(SolidityGenerator))]
    [Conditional("CodeGeneration")]
    public class SolidityAttribute : Attribute
    {
    }
}

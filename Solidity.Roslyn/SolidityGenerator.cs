using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeGeneration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Solidity.Roslyn
{
    public class SolidityGenerator : ICodeGenerator
    {
        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        public SolidityGenerator(AttributeData attributeData)
        {
        }

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "solc",
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                try
                {
                    process.Start();
                }
                catch
                {
                    throw new InvalidOperationException("System doesn't have solc available in PATH.");
                }
            }

            var solidityFiles = Directory.EnumerateFiles(context.ProjectDirectory, "*.sol", SearchOption.AllDirectories);

            var jsons = solidityFiles.Select(file =>
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "solc",
                        Arguments = $"--combined-json abi,bin {file}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                return output;
            });

            var contracts = jsons.Select(JsonConvert.DeserializeObject<SolcOutput>).SelectMany(x => x.Contracts);


            var results = contracts.Select(x=>
            {
                int separatorIndex = x.Key.LastIndexOf(':');
                var contract = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(x.Key.Substring(separatorIndex + 1));
                var namespaceName = Path.GetFileNameWithoutExtension(x.Key.Remove(separatorIndex));

                var classDeclarationSyntax = NamespaceDeclaration(IdentifierName(namespaceName))
                    .AddMembers(
                        ClassDeclaration(contract)
                            .AddMembers(
                                FieldDeclaration(
                                        VariableDeclaration(
                                                PredefinedType(Token(SyntaxKind.StringKeyword)))
                                            .AddVariables(
                                                VariableDeclarator(Identifier("Abi"))
                                                    .WithInitializer(
                                                        EqualsValueClause(
                                                            LiteralExpression(
                                                                SyntaxKind.StringLiteralExpression,
                                                                Literal(x.Value.Abi))))))
                                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword),
                                        Token(SyntaxKind.ConstKeyword))),
                                FieldDeclaration(
                                        VariableDeclaration(
                                                PredefinedType(Token(SyntaxKind.StringKeyword)))
                                            .AddVariables(
                                                VariableDeclarator(Identifier("Bin"))
                                                    .WithInitializer(
                                                        EqualsValueClause(
                                                            LiteralExpression(
                                                                SyntaxKind.StringLiteralExpression,
                                                                Literal(x.Value.Bin))))))
                                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword),
                                        Token(SyntaxKind.ConstKeyword))))
                            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword))));
                return classDeclarationSyntax;
            });

            return Task.FromResult(List<MemberDeclarationSyntax>(results));
        }
    }

    class SolcOutput
    {
        public string Version { get; set; }
        public Dictionary<string, Contract> Contracts { get; set; }
    }

    class Contract
    {
        public string Abi { get; set; }
        public string Bin { get; set; }
    }
}

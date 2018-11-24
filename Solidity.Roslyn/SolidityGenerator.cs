using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context,
                                                                       IProgress<Diagnostic> progress,
                                                                       CancellationToken cancellationToken)
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

            string defaultNamespace = Path.GetFileName(context.ProjectDirectory);

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

            var results = contracts.Select(x =>
            {
                int separatorIndex = x.Key.LastIndexOf(':');
                string contract = Capitalize(x.Key.Substring(separatorIndex + 1));
                string namespaceName = Path.GetFileNameWithoutExtension(x.Key.Remove(separatorIndex));

                var abiIdentifier = Identifier("Abi");
                var binIdentifier = Identifier("Bin");

                var classIdentifier = Identifier(contract);

                var contractClassDeclaration = ClassDeclaration(classIdentifier)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .AddMembers(
                        FieldDeclaration(
                                VariableDeclaration(
                                        PredefinedType(Token(SyntaxKind.StringKeyword)))
                                    .AddVariables(
                                        VariableDeclarator(abiIdentifier)
                                            .WithInitializer(
                                                EqualsValueClause(
                                                    LiteralExpression(
                                                        SyntaxKind.StringLiteralExpression,
                                                        Literal(x.Value.Abi))))))
                            .AddModifiers(Token(SyntaxKind.PublicKeyword),
                                          Token(SyntaxKind.ConstKeyword)),
                        FieldDeclaration(
                                VariableDeclaration(
                                        PredefinedType(Token(SyntaxKind.StringKeyword)))
                                    .AddVariables(
                                        VariableDeclarator(binIdentifier)
                                            .WithInitializer(
                                                EqualsValueClause(
                                                    LiteralExpression(
                                                        SyntaxKind.StringLiteralExpression,
                                                        Literal(x.Value.Bin))))))
                            .AddModifiers(Token(SyntaxKind.PublicKeyword),
                                          Token(SyntaxKind.ConstKeyword)));

                var classDeclarationWithMethods = contractClassDeclaration;

                var namespaceDeclaration = NamespaceDeclaration(
                        QualifiedName(GetQualifiedName(defaultNamespace),
                                      IdentifierName(namespaceName)))
                    .AddUsings(UsingDirective(
                                   QualifiedName(
                                       QualifiedName(
                                           IdentifierName(nameof(System)),
                                           IdentifierName(nameof(System.Collections))),
                                       IdentifierName(nameof(System.Collections.Generic)))),
                               UsingDirective(
                                   QualifiedName(
                                       IdentifierName(nameof(System)),
                                       IdentifierName(nameof(System.Numerics)))),
                               UsingDirective(
                                   QualifiedName(
                                       QualifiedName(
                                           IdentifierName(nameof(System)),
                                           IdentifierName(nameof(System.Threading))),
                                       IdentifierName(nameof(System.Threading.Tasks)))))
                    .AddMembers(classDeclarationWithMethods);

                return namespaceDeclaration;
            });

            return Task.FromResult(List<MemberDeclarationSyntax>(results));
        }

        private static NameSyntax GetQualifiedName(string dotSeparatedName)
        {
            var separated = dotSeparatedName.Split('.');
            return separated.Skip(1).Aggregate((NameSyntax) IdentifierName(separated[0]), (result, segment) => QualifiedName(result, IdentifierName(segment)));
        }

        private static string Capitalize(string value) => $"{char.ToUpper(value[0])}{value.Substring(1)}";
    }
}

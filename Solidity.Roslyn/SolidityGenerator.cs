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

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context,
                                                                       IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            var solidityFiles =
                Directory.EnumerateFiles(context.ProjectDirectory, "*.sol", SearchOption.AllDirectories);

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

                var classDeclaration = ClassDeclaration(contract)
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
                                       .WithBaseList(
                                           BaseList(
                                               SingletonSeparatedList<BaseTypeSyntax>(
                                                   SimpleBaseType(
                                                       IdentifierName(nameof(ContractBase))))));
                classDeclaration = classDeclaration.AddMembers(ConstructorDeclaration(
                                                                   classDeclaration.Identifier)
                                                               .WithModifiers(
                                                                   TokenList(
                                                                       Token(SyntaxKind.PublicKeyword)))
                                                               .WithParameterList(
                                                                   ParameterList(
                                                                       SeparatedList<ParameterSyntax>(
                                                                           new SyntaxNodeOrToken[]{
                                                                               Parameter(
                                                                                       Identifier("ethereumHandler"))
                                                                                   .WithType(
                                                                                       IdentifierName(nameof(IEthereumHandler))),
                                                                               Token(SyntaxKind.CommaToken),
                                                                               Parameter(
                                                                                       Identifier("address"))
                                                                                   .WithType(
                                                                                       PredefinedType(
                                                                                           Token(SyntaxKind.StringKeyword)))})))
                                                               .WithInitializer(
                                                                   ConstructorInitializer(
                                                                       SyntaxKind.BaseConstructorInitializer,
                                                                       ArgumentList(
                                                                           SeparatedList<ArgumentSyntax>(
                                                                               new SyntaxNodeOrToken[]{
                                                                                   Argument(
                                                                                       IdentifierName("ethereumHandler")),
                                                                                   Token(SyntaxKind.CommaToken),
                                                                                   Argument(
                                                                                       IdentifierName("address"))}))))
                                                               .WithBody(
                                                                   Block()));

                var abis = JsonConvert.DeserializeObject<Abi[]>(x.Value.Abi);

                var methods = abis.Select(abi =>
                {
                    var method = abi.Type == MemberType.Constructor
                                     ? MethodDeclaration(
                                         IdentifierName("Task"),
                                         Identifier("DeployAsync")).WithModifiers(TokenList(Token(SyntaxKind.StaticKeyword)))
                                     : MethodDeclaration(
                                         IdentifierName("Task"),
                                         Identifier(Capitalize(abi.Name) + "Async"));
                    var methodDeclarationSyntax = method
                                                  .AddModifiers(Token(SyntaxKind.PublicKeyword))
                                                  .WithExpressionBody(
                                                      ArrowExpressionClause(
                                                          LiteralExpression(
                                                              SyntaxKind.NullLiteralExpression)))
                                                  .WithSemicolonToken(
                                                      Token(SyntaxKind.SemicolonToken));
                    return methodDeclarationSyntax;
                });

                var classDeclarationWithMethods = classDeclaration.AddMembers(methods.Cast<MemberDeclarationSyntax>().ToArray());

                var namespaceDeclaration = NamespaceDeclaration(IdentifierName(namespaceName))
                                           .WithUsings(
                                               SingletonList(
                                                   UsingDirective(
                                                       QualifiedName(
                                                           QualifiedName(
                                                               IdentifierName("System"),
                                                               IdentifierName("Threading")),
                                                           IdentifierName("Tasks")))))
                    .AddMembers(classDeclarationWithMethods);

                return namespaceDeclaration;
            });

            return Task.FromResult(List<MemberDeclarationSyntax>(results));
        }

        private static string Capitalize(string value) => $"{char.ToUpper(value[0])}{value.Substring(1)}";
    }

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

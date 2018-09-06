using System;
using System.Collections.Generic;
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
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Generators.Core;
using Nethereum.Web3;
using Newtonsoft.Json;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Solidity.Roslyn
{
    public class SolidityGenerator : ICodeGenerator
    {
        [SuppressMessage("ReSharper",
            "UnusedParameter.Local")]
        public SolidityGenerator(AttributeData attributeData)
        {
        }

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context,
                                                                       IProgress<Diagnostic> progress,
                                                                       CancellationToken cancellationToken)
        {
            var solidityFiles =
                Directory.EnumerateFiles(context.ProjectDirectory,
                                         "*.sol",
                                         SearchOption.AllDirectories);

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

            var contracts = jsons.Select(JsonConvert.DeserializeObject<SolcOutput>)
                .SelectMany(x => x.Contracts);

            var typeConverter = new ABITypeToCSharpType();

            var results = contracts.Select(x =>
            {
                int separatorIndex = x.Key.LastIndexOf(':');
                string contract = Capitalize(x.Key.Substring(separatorIndex + 1));
                string namespaceName = Path.GetFileNameWithoutExtension(x.Key.Remove(separatorIndex));

                var abiIdentifier = Identifier("Abi");
                var binIdentifier = Identifier("Bin");
                var web3Identifier = Identifier("web3");

                var contractClassDeclaration = ClassDeclaration(contract)
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
                                          Token(SyntaxKind.ConstKeyword)))
                    .AddBaseListTypes(
                        SimpleBaseType(
                            IdentifierName(nameof(ContractBase))));

                contractClassDeclaration = contractClassDeclaration
                    .AddMembers(ConstructorDeclaration(
                                        contractClassDeclaration.Identifier)
                                    .AddModifiers(
                                        Token(SyntaxKind.PublicKeyword))
                                    .AddParameterListParameters(
                                        Parameter(
                                                web3Identifier)
                                            .WithType(
                                                IdentifierName(nameof(Web3))),
                                        Parameter(
                                                Identifier("address"))
                                            .WithType(
                                                PredefinedType(
                                                    Token(SyntaxKind.StringKeyword))))
                                    .WithInitializer(
                                        ConstructorInitializer(
                                            SyntaxKind.BaseConstructorInitializer,
                                            ArgumentList(
                                                SeparatedList(
                                                    new[]
                                                    {
                                                        Argument(
                                                            IdentifierName(web3Identifier)),
                                                        Argument(
                                                            IdentifierName(abiIdentifier)),
                                                        Argument(
                                                            IdentifierName("address"))
                                                    }))))
                                    .WithBody(
                                        Block()));

                var abis = JsonConvert.DeserializeObject<Abi[]>(x.Value.Abi);

                var outputTypes = new List<MemberDeclarationSyntax>();

                var methods = abis.Select(abi =>
                {
                    var inputParameters = abi.Inputs.Select((input, i) => new ParameterDescription(input.Name, typeConverter.Convert(input.Type), input.Type, $"parameter{i + 1}"))
                        .ToArray();
                    var outputParameters = (abi.Outputs ?? Array.Empty<Parameter>()).Select((output,
                                                                                             i) => new ParameterDescription(output.Name, typeConverter.Convert(output.Type, true), output.Type, $"Property{i + 1}"))
                        .ToArray();

                    var methodParameters = inputParameters.SelectMany(input => new[]
                        {
                            Parameter(
                                    Identifier(input.Name))
                                .WithType(
                                    IdentifierName(input.Type))
                        })
                        .ToArray();

                    var initializerParameters = inputParameters.SelectMany(input => new[]
                        {
                            IdentifierName(input.Name)
                        })
                        .ToArray();

                    var callParameters = inputParameters.SelectMany(input => new[]
                        {
                            Argument(IdentifierName(input.Name))
                        })
                        .ToArray();

                    if (abi.Type == MemberType.Constructor)
                    {
                        return GetConstructodDeclaration(web3Identifier,
                                                         methodParameters,
                                                         contractClassDeclaration,
                                                         abiIdentifier,
                                                         binIdentifier,
                                                         initializerParameters);
                    }

                    if (outputParameters.Length > 0)
                    {
                        SyntaxToken outputType;
                        string methodName;

                        if (outputParameters.Length == 1)
                        {
                            outputType = Identifier(outputParameters.Single()
                                                        .Type);
                            methodName = nameof(Function.CallAsync);
                        }
                        else
                        {
                            var outputTypeClass = ClassDeclaration(contract + Capitalize(abi.Name) + "Output")
                                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                                .WithAttributeLists(
                                    SingletonList(
                                        AttributeList(
                                            SingletonSeparatedList(
                                                Attribute(
                                                    IdentifierName(nameof(FunctionOutputAttribute)))))))
                                .AddMembers(outputParameters
                                                .Select((output,
                                                         i) => PropertyDeclaration(IdentifierName(output.Type),
                                                                                   output.Name)
                                                            .WithAttributeLists(
                                                                SingletonList(
                                                                    AttributeList(
                                                                        SingletonSeparatedList(
                                                                            Attribute(
                                                                                    IdentifierName(nameof(ParameterAttribute)))
                                                                                .AddArgumentListArguments(AttributeArgument(
                                                                                                              LiteralExpression(
                                                                                                                  SyntaxKind
                                                                                                                      .StringLiteralExpression,
                                                                                                                  Literal(
                                                                                                                      output
                                                                                                                          .OriginalType))),
                                                                                                          AttributeArgument(
                                                                                                              LiteralExpression(
                                                                                                                  SyntaxKind
                                                                                                                      .NumericLiteralExpression,
                                                                                                                  Literal(i + 1))))))))
                                                            .AddModifiers(Token(SyntaxKind.PublicKeyword))
                                                            .AddAccessorListAccessors(AccessorDeclaration(
                                                                                              SyntaxKind.GetAccessorDeclaration)
                                                                                          .WithSemicolonToken(
                                                                                              Token(SyntaxKind.SemicolonToken)),
                                                                                      AccessorDeclaration(
                                                                                              SyntaxKind.SetAccessorDeclaration)
                                                                                          .WithSemicolonToken(
                                                                                              Token(SyntaxKind.SemicolonToken))))
                                                .Cast<MemberDeclarationSyntax>()
                                                .ToArray());

                            outputTypes.Add(outputTypeClass);

                            outputType = outputTypeClass.Identifier;
                            methodName = nameof(Function.CallDeserializingToObjectAsync);
                        }

                        var methodDeclarationSyntax = MethodDeclaration(
                                GenericName(
                                        Identifier("Task"))
                                    .AddTypeArgumentListArguments(
                                        IdentifierName(outputType)),
                                Identifier(Capitalize(abi.Name) + "Async"))
                            .AddModifiers(Token(SyntaxKind.PublicKeyword))
                            .AddParameterListParameters(methodParameters)
                            .WithExpressionBody(
                                ArrowExpressionClause(
                                    InvocationExpression(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                InvocationExpression(
                                                        MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            IdentifierName("Contract"),
                                                            IdentifierName("GetFunction")))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            LiteralExpression(
                                                                SyntaxKind.StringLiteralExpression,
                                                                Literal(abi.Name)))),
                                                GenericName(
                                                        Identifier(methodName))
                                                    .AddTypeArgumentListArguments(
                                                        IdentifierName(outputType))))
                                        .AddArgumentListArguments(callParameters)))
                            .WithSemicolonToken(
                                Token(SyntaxKind.SemicolonToken));
                        return methodDeclarationSyntax;
                    }

                    var sendTxCallParameters = new[]
                        {
                            Argument(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("Web3"),
                                            IdentifierName("TransactionManager")),
                                        IdentifierName("Account")),
                                    IdentifierName("Address")))
                        }.Concat(callParameters)
                        .ToArray();

                    return MethodDeclaration(
                            IdentifierName("Task"),
                            Identifier(Capitalize(abi.Name) + "Async"))
                        .AddModifiers(Token(SyntaxKind.PublicKeyword))
                        .AddParameterListParameters(
                            methodParameters)
                        .WithExpressionBody(
                            ArrowExpressionClause(
                                InvocationExpression(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            InvocationExpression(
                                                    MemberAccessExpression(
                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                        IdentifierName("Contract"),
                                                        IdentifierName("GetFunction")))
                                                .AddArgumentListArguments(
                                                    Argument(
                                                        LiteralExpression(
                                                            SyntaxKind.StringLiteralExpression,
                                                            Literal(abi.Name)))),
                                            IdentifierName(nameof(XContractFunction.SendDefaultTransactionAndWaitForReceiptAsync))))
                                    .AddArgumentListArguments(
                                        sendTxCallParameters)))
                        .WithSemicolonToken(
                            Token(SyntaxKind.SemicolonToken));
                });

                var classDeclarationWithMethods = contractClassDeclaration.AddMembers(methods.Cast<MemberDeclarationSyntax>()
                                                                                          .ToArray());

                var namespaceDeclaration = NamespaceDeclaration(IdentifierName(namespaceName))
                    .AddUsings(UsingDirective(
                                   IdentifierName("System")),
                               UsingDirective(
                                   QualifiedName(
                                       QualifiedName(
                                           IdentifierName("System"),
                                           IdentifierName("Collections")),
                                       IdentifierName("Generic"))),
                               UsingDirective(
                                   QualifiedName(
                                       IdentifierName("System"),
                                       IdentifierName("Numerics"))),
                               UsingDirective(
                                   QualifiedName(
                                       QualifiedName(
                                           IdentifierName("System"),
                                           IdentifierName("Threading")),
                                       IdentifierName("Tasks"))),
                               UsingDirective(
                                   QualifiedName(
                                       QualifiedName(
                                           QualifiedName(
                                               IdentifierName("Nethereum"),
                                               IdentifierName("ABI")),
                                           IdentifierName("FunctionEncoding")),
                                       IdentifierName("Attributes"))),
                               UsingDirective(
                                   QualifiedName(
                                       IdentifierName("Nethereum"),
                                       IdentifierName("Web3"))))
                    .AddMembers(classDeclarationWithMethods)
                    .AddMembers(outputTypes.ToArray());

                return namespaceDeclaration;
            });

            return Task.FromResult(List<MemberDeclarationSyntax>(results));
        }

        private static MethodDeclarationSyntax GetConstructodDeclaration(SyntaxToken web3Identifier,
                                                                         ParameterSyntax[] methodParameters,
                                                                         ClassDeclarationSyntax contractClassDeclaration,
                                                                         SyntaxToken abiIdentifier,
                                                                         SyntaxToken binIdentifier,
                                                                         IdentifierNameSyntax[] initializerParameters)
        {
            var constructorParameters = new[]
                {
                    Parameter(
                            web3Identifier)
                        .WithType(
                            IdentifierName(nameof(Web3)))
                }.Concat(methodParameters)
                .ToArray();

            return MethodDeclaration(
                    GenericName(
                            Identifier("Task"))
                        .AddTypeArgumentListArguments(
                            IdentifierName(contractClassDeclaration.Identifier)),
                    Identifier("DeployAsync"))
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.StaticKeyword),
                    Token(SyntaxKind.AsyncKeyword))
                .AddParameterListParameters(
                    constructorParameters)
                .AddBodyStatements(
                    LocalDeclarationStatement(
                        VariableDeclaration(
                                IdentifierName("var"))
                            .AddVariables(
                                VariableDeclarator(
                                        Identifier("receipt"))
                                    .WithInitializer(
                                        EqualsValueClause(
                                            AwaitExpression(
                                                InvocationExpression(
                                                        MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            IdentifierName(nameof(ContractBase)),
                                                            IdentifierName("DeployAsync")))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            IdentifierName(web3Identifier)),
                                                        Argument(
                                                            IdentifierName(abiIdentifier)),
                                                        Argument(
                                                            IdentifierName(binIdentifier)),
                                                        Argument(
                                                            ArrayCreationExpression(
                                                                    ArrayType(
                                                                            PredefinedType(
                                                                                Token(SyntaxKind.ObjectKeyword)))
                                                                        .AddRankSpecifiers(
                                                                            ArrayRankSpecifier()))
                                                                .WithInitializer(
                                                                    InitializerExpression(
                                                                        SyntaxKind.ArrayInitializerExpression,
                                                                        SeparatedList<ExpressionSyntax>(
                                                                            initializerParameters)))))))))),
                    ReturnStatement(
                        ObjectCreationExpression(
                                IdentifierName(contractClassDeclaration.Identifier))
                            .AddArgumentListArguments(Argument(
                                                          IdentifierName(web3Identifier)),
                                                      Argument(
                                                          MemberAccessExpression(
                                                              SyntaxKind.SimpleMemberAccessExpression,
                                                              IdentifierName("receipt"),
                                                              IdentifierName("ContractAddress"))))));
        }

        private static string Capitalize(string value) => $"{char.ToUpper(value[0])}{value.Substring(1)}";

        public struct ParameterDescription
        {
            public string Name { get; }
            public string Type { get; }
            public string OriginalType { get; }

            public ParameterDescription(string name, string type, string originalType, string missingReplacement) : this()
            {
                Name = !string.IsNullOrEmpty(name)
                           ? name
                           : missingReplacement;
                Type = type;
                OriginalType = originalType;
            }
        }
    }
}

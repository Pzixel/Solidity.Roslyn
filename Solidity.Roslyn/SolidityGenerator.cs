using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Web3;
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
            var solidityFiles = Directory.EnumerateFiles(context.ProjectDirectory,
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

            var contracts = jsons.Select(JsonConvert.DeserializeObject<SolcOutput>).SelectMany(x => x.Contracts);

            var typeConverter = new ABITypeToCSharpType();

            var results = contracts.Select(x =>
            {
                int separatorIndex = x.Key.LastIndexOf(':');
                string contract = Capitalize(x.Key.Substring(separatorIndex + 1));
                string namespaceName = Path.GetFileNameWithoutExtension(x.Key.Remove(separatorIndex));

                var abiIdentifier = Identifier("Abi");
                var binIdentifier = Identifier("Bin");
                var web3Identifier = Identifier("web3");
                var contractProperty = Identifier("Contract");
                var addressIdentifier = Identifier("address");

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
                                          Token(SyntaxKind.ConstKeyword)),
                        ConstructorDeclaration(classIdentifier)
                            .AddModifiers(
                                Token(SyntaxKind.PublicKeyword))
                            .AddParameterListParameters(
                                Parameter(
                                        web3Identifier)
                                    .WithType(
                                        IdentifierName(nameof(Web3))),
                                Parameter(
                                        addressIdentifier)
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
                                                    IdentifierName(addressIdentifier))
                                            }))))
                            .WithBody(
                                Block()))
                    .AddBaseListTypes(
                        SimpleBaseType(
                            IdentifierName(nameof(ContractBase))));

                var abis = JsonConvert.DeserializeObject<Abi[]>(x.Value.Abi);

                var outputTypes = new List<MemberDeclarationSyntax>();

                var methods = abis.Select(abi =>
                {
                    var inputParameters = abi.Inputs.Select((input,
                                                             i) => new ParameterDescription(input.Name,
                                                                                            typeConverter.Convert(input.Type),
                                                                                            input.Type,
                                                                                            $"parameter{i + 1}",
                                                                                            input.Indexed))
                        .ToArray();
                    var outputParameters = (abi.Outputs ?? Array.Empty<Parameter>()).Select((output,
                                                                                             i) => new ParameterDescription(output.Name,
                                                                                                                            typeConverter.Convert(output.Type,
                                                                                                                                                  true),
                                                                                                                            output.Type,
                                                                                                                            $"Property{i + 1}",
                                                                                                                            output.Indexed))
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

                    switch (abi.Type)
                    {
                        case MemberType.Constructor:
                            return GetConstructorDeclaration(web3Identifier,
                                                             methodParameters,
                                                             contractClassDeclaration,
                                                             abiIdentifier,
                                                             binIdentifier,
                                                             initializerParameters);
                        case MemberType.Function:
                            if (outputParameters.Length > 0)
                            {
                                return GetCallMethodDeclarationSyntax(outputParameters,
                                                                      contract,
                                                                      abi,
                                                                      outputTypes,
                                                                      methodParameters,
                                                                      callParameters,
                                                                      contractProperty);
                            }

                            return GetSendTxMethodDeclarationSyntax(callParameters,
                                                                    abi,
                                                                    methodParameters,
                                                                    contractProperty);
                        case MemberType.Event:
                            return GetEventMethodDeclarationSyntax(inputParameters,
                                                                   contract,
                                                                   abi,
                                                                   outputTypes,
                                                                   contractProperty);
                        default:
                            throw new InvalidEnumArgumentException(nameof(abi.Type),
                                                                   (int) abi.Type,
                                                                   typeof(MemberType));
                    }
                });

                var classDeclarationWithMethods = contractClassDeclaration.AddMembers(methods.Cast<MemberDeclarationSyntax>()
                                                                                          .ToArray());

                var namespaceDeclaration = NamespaceDeclaration(IdentifierName(namespaceName))
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
                                       IdentifierName(nameof(System.Threading.Tasks)))),
                               UsingDirective(
                                   QualifiedName(
                                       QualifiedName(
                                           QualifiedName(
                                               IdentifierName(nameof(Nethereum)),
                                               IdentifierName(nameof(Nethereum.ABI))),
                                           IdentifierName(nameof(Nethereum.ABI.FunctionEncoding))),
                                       IdentifierName(nameof(Nethereum.ABI.FunctionEncoding.Attributes)))),
                               UsingDirective(
                                   QualifiedName(
                                       IdentifierName(nameof(Nethereum)),
                                       IdentifierName(nameof(Nethereum.Contracts)))),
                               UsingDirective(
                                   QualifiedName(
                                       QualifiedName(
                                           QualifiedName(
                                               IdentifierName(nameof(Nethereum)),
                                               IdentifierName(nameof(Nethereum.RPC))),
                                           IdentifierName(nameof(Nethereum.RPC.Eth))),
                                       IdentifierName(nameof(Nethereum.RPC.Eth.DTOs)))),
                               UsingDirective(
                                   QualifiedName(
                                       IdentifierName(nameof(Nethereum)),
                                       IdentifierName(nameof(Nethereum.Web3)))))
                    .AddMembers(classDeclarationWithMethods)
                    .AddMembers(outputTypes.ToArray());

                return namespaceDeclaration;
            });

            return Task.FromResult(List<MemberDeclarationSyntax>(results));
        }

        private static MethodDeclarationSyntax GetEventMethodDeclarationSyntax(ParameterDescription[] inputParameters,
                                                                               string contract,
                                                                               Abi abi,
                                                                               List<MemberDeclarationSyntax> outputTypes,
                                                                               SyntaxToken contractProperty)
        {
            var members = inputParameters
                .Select((output,
                         i) => PropertyDeclaration(IdentifierName(output.Type),
                                                   Capitalize(output.Name))
                            .WithAttributeLists(
                                SingletonList(
                                    AttributeList(
                                        SingletonSeparatedList(
                                            Attribute(
                                                    IdentifierName(nameof(ParameterAttribute)))
                                                .AddArgumentListArguments(
                                                    AttributeArgument(
                                                        LiteralExpression(
                                                            SyntaxKind
                                                                .StringLiteralExpression,
                                                            Literal(
                                                                output
                                                                    .OriginalType))),
                                                    AttributeArgument(
                                                        LiteralExpression(
                                                            SyntaxKind
                                                                .StringLiteralExpression,
                                                            Literal(
                                                                output
                                                                    .Name))),
                                                    AttributeArgument(
                                                        LiteralExpression(
                                                            SyntaxKind
                                                                .NumericLiteralExpression,
                                                            Literal(i + 1))),
                                                    AttributeArgument(
                                                        LiteralExpression(
                                                            output.Indexed ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression)))))))
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
                .ToArray();
            var eventDto = ClassDeclaration(default(SyntaxList<AttributeListSyntax>),
                                            TokenList(Token(SyntaxKind.PublicKeyword)),
                                            Identifier(contract + Capitalize(abi.Name) + "EventDTO"),
                                            default(TypeParameterListSyntax),
                                            default(BaseListSyntax),
                                            default(SyntaxList<TypeParameterConstraintClauseSyntax>),
                                            List(members));

            outputTypes.Add(eventDto);

            return MethodDeclaration(
                    GenericName(nameof(Event))
                        .AddTypeArgumentListArguments(IdentifierName(eventDto.Identifier)),
                    Identifier($"Get{Capitalize(abi.Name)}Event"))
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PublicKeyword)))
                .WithExpressionBody(
                    ArrowExpressionClause(
                        InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(contractProperty),
                                    GenericName(nameof(Nethereum.Contracts.Contract.GetEvent))
                                        .AddTypeArgumentListArguments(IdentifierName(eventDto.Identifier))))
                            .WithArgumentList(
                                ArgumentList(
                                    SingletonSeparatedList(
                                        Argument(
                                            LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                Literal(abi.Name))))))))
                .WithSemicolonToken(
                    Token(SyntaxKind.SemicolonToken));
        }

        private static MethodDeclarationSyntax GetSendTxMethodDeclarationSyntax(ArgumentSyntax[] callParameters,
                                                                                Abi abi,
                                                                                ParameterSyntax[] methodParameters,
                                                                                SyntaxToken contractProperty)
        {
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
                                    IdentifierName(nameof(Web3.TransactionManager))),
                                IdentifierName(nameof(ITransactionManager.Account))),
                            IdentifierName(nameof(IAccount.Address))))
                }.Concat(callParameters)
                .ToArray();

            return MethodDeclaration(
                    GenericName(
                            Identifier(nameof(Task)))
                        .AddTypeArgumentListArguments(
                            IdentifierName(nameof(TransactionReceipt))),
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
                                                IdentifierName(contractProperty),
                                                IdentifierName(nameof(Nethereum.Contracts.Contract.GetFunction))))
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
        }

        private static MethodDeclarationSyntax GetCallMethodDeclarationSyntax(ParameterDescription[] outputParameters,
                                                                              string contract,
                                                                              Abi abi,
                                                                              List<MemberDeclarationSyntax> outputTypes,
                                                                              ParameterSyntax[] methodParameters,
                                                                              ArgumentSyntax[] callParameters,
                                                                              SyntaxToken contractProperty)
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
                            Identifier(nameof(Task)))
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
                                                IdentifierName(contractProperty),
                                                IdentifierName(nameof(Nethereum.Contracts.Contract.GetFunction))))
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

        private static MethodDeclarationSyntax GetConstructorDeclaration(SyntaxToken web3Identifier,
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

            var receiptSyntaxToken = Identifier("receipt");
            var deployedContractSyntaxToken = Identifier("deployedContract");
            var deploymentResultType = GenericName(
                    nameof(DeploymentResult<ContractBase>))
                .AddTypeArgumentListArguments(IdentifierName(contractClassDeclaration.Identifier));
            return MethodDeclaration(
                    GenericName(
                            Identifier(nameof(Task)))
                        .AddTypeArgumentListArguments(
                            deploymentResultType),
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
                                        receiptSyntaxToken)
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
                    LocalDeclarationStatement(
                        VariableDeclaration(
                                IdentifierName("var"))
                            .AddVariables(
                                VariableDeclarator(
                                        deployedContractSyntaxToken)
                                    .WithInitializer(
                                        EqualsValueClause(
                                            ObjectCreationExpression(
                                                    IdentifierName(contractClassDeclaration.Identifier))
                                                .AddArgumentListArguments(Argument(
                                                                              IdentifierName(web3Identifier)),
                                                                          Argument(
                                                                              MemberAccessExpression(
                                                                                  SyntaxKind.SimpleMemberAccessExpression,
                                                                                  IdentifierName(receiptSyntaxToken),
                                                                                  IdentifierName(nameof(TransactionReceipt.ContractAddress))))))))),
                    ReturnStatement(
                        ObjectCreationExpression(deploymentResultType)
                            .AddArgumentListArguments(
                                Argument(IdentifierName(deployedContractSyntaxToken)),
                                Argument(IdentifierName(receiptSyntaxToken)))));
        }

        private static string Capitalize(string value) => $"{char.ToUpper(value[0])}{value.Substring(1)}";

        private struct ParameterDescription
        {
            public string Name { get; }
            public string Type { get; }
            public string OriginalType { get; }
            public bool Indexed { get; }

            public ParameterDescription(string name,
                                        string type,
                                        string originalType,
                                        string missingReplacement,
                                        bool indexed)
            {
                Name = !string.IsNullOrEmpty(name)
                           ? Capitalize(name)
                           : missingReplacement;
                Type = type;
                OriginalType = originalType;
                Indexed = indexed;
            }
        }
    }
}

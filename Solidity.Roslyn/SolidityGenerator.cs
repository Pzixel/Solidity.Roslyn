using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        public SolidityGenerator(AttributeData _)
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

            var solidityFiles = Directory.EnumerateFiles(context.ProjectDirectory, "*.sol", SearchOption.AllDirectories).ToArray();

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

            var sourceFiles = solidityFiles.Select(File.ReadAllText);
            var inheritanceDictionary = sourceFiles.SelectMany(text => Regex.Matches(text, @"contract\s+(\w+)\s+is\s+(\w+)")
                                                              .Cast<Match>())
                                     .ToDictionary(m => Capitalize(m.Groups[1]
                                                         .Value),
                                                   m => m.Groups[2]
                                                         .Value);

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
                var abiParamIdentifier = Identifier("abi");
                var contractProperty = Identifier("Contract");
                var addressIdentifier = Identifier("address");

                var classIdentifier = Identifier(contract);

                string baseClass = inheritanceDictionary.TryGetValue(contract, out baseClass)
                                    ? Capitalize(baseClass)
                                    : "ContractBase";

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
                                        IdentifierName("Web3")),
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
                                Block()),
                        ConstructorDeclaration(classIdentifier)
                            .AddModifiers(
                                Token(SyntaxKind.ProtectedKeyword))
                            .AddParameterListParameters(
                                Parameter(
                                        web3Identifier)
                                    .WithType(
                                        IdentifierName("Web3")),
                                Parameter(
                                        abiParamIdentifier)
                                    .WithType(
                                        PredefinedType(
                                            Token(SyntaxKind.StringKeyword))),
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
                                                    IdentifierName(abiParamIdentifier)),
                                                Argument(
                                                    IdentifierName(addressIdentifier))
                                            }))))
                            .WithBody(
                                Block()))
                    .AddBaseListTypes(
                        SimpleBaseType(
                            IdentifierName(baseClass)))
                    .WithPragma();

                var abis = JsonConvert.DeserializeObject<Abi[]>(x.Value.Abi);

                var outputTypes = new List<MemberDeclarationSyntax>();

                bool hasCtor = false;
                var methods = abis.SelectMany(abi =>
                {
                    var inputParameters = abi.Inputs.Select((input,
                                                             i) => new ParameterDescription(Decapitalize(input.Name.Trim('_')),
                                                                                            typeConverter.Convert(input.Type),
                                                                                            input.Type,
                                                                                            $"parameter{i + 1}",
                                                                                            input.Indexed))
                        .ToArray();
                    var outputParameters = (abi.Outputs ?? Array.Empty<Parameter>()).Select((output,
                                                                                             i) => new ParameterDescription(Capitalize(output.Name),
                                                                                                                            typeConverter.Convert(output.Type,
                                                                                                                                                  outputArrayAsList: true),
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
                            hasCtor = true;
                            return GetDeployDeclarations(web3Identifier,
                                                             methodParameters,
                                                             contractClassDeclaration,
                                                             abiIdentifier,
                                                             binIdentifier,
                                                             initializerParameters);
                        case MemberType.Function:
                            if (outputParameters.Length > 0)
                            {
                                return new[]
                                {
                                    GetCallMethodDeclarationSyntax(outputParameters,
                                                                   contract,
                                                                   abi,
                                                                   outputTypes,
                                                                   methodParameters,
                                                                   callParameters,
                                                                   contractProperty)
                                };
                            }

                            return new[]
                            {
                                GetSendTxMethodDeclarationSyntax(callParameters,
                                                                 abi,
                                                                 methodParameters,
                                                                 contractProperty)
                            };
                        case MemberType.Event:
                            return new []
                            {
                                GetEventMethodDeclarationSyntax(inputParameters,
                                                                contract,
                                                                abi,
                                                                outputTypes,
                                                                contractProperty)
                            };
                        default:
                            throw new InvalidEnumArgumentException("Type",
                                                                   (int) abi.Type,
                                                                   typeof(MemberType));
                    }
                }).ToList();

                if (!hasCtor)
                {
                    methods.AddRange(GetDeployDeclarations(web3Identifier,
                                                     Array.Empty<ParameterSyntax>(),
                                                     contractClassDeclaration,
                                                     abiIdentifier,
                                                     binIdentifier,
                                                     Array.Empty<IdentifierNameSyntax>()));
                }

                var classDeclarationWithMethods = contractClassDeclaration.AddMembers(methods.Cast<MemberDeclarationSyntax>()
                                                                                          .ToArray());

                var namespaceDeclaration = NamespaceDeclaration(
                        QualifiedName(GetQualifiedName(defaultNamespace),
                                      IdentifierName(namespaceName)))
                    .AddUsings(GetUsingDirective("System", "Collections", "Generic"),
                               GetUsingDirective("System", "Numerics"),
                               GetUsingDirective("System", "Threading", "Tasks"),
                               GetUsingDirective("Nethereum", "ABI", "FunctionEncoding", "Attributes"),
                               GetUsingDirective("Nethereum", "Contracts"),
                               GetUsingDirective("Nethereum", "Hex", "HexTypes"),
                               GetUsingDirective("Nethereum", "RPC", "Eth", "DTOs"),
                               GetUsingDirective("Nethereum", "Web3"),
                               GetUsingDirective("Solidity", "Roslyn", "Core"))
                    .AddMembers(classDeclarationWithMethods)
                    .AddMembers(outputTypes.ToArray());

                return namespaceDeclaration;
            });

            return Task.FromResult(List<MemberDeclarationSyntax>(results));
        }

        private static UsingDirectiveSyntax GetUsingDirective(params string[] namespaces) =>
            UsingDirective(
                namespaces.Skip(1)
                          .Aggregate((NameSyntax) AliasQualifiedName(
                                         IdentifierName(
                                             Token(SyntaxKind.GlobalKeyword)),
                                         IdentifierName(namespaces.First())),
                                     (syntax, s) => QualifiedName(syntax, IdentifierName(s))));

        private static NameSyntax GetQualifiedName(string dotSeparatedName)
        {
            var separated = dotSeparatedName.Split('.');
            return separated.Skip(1).Aggregate((NameSyntax) IdentifierName(separated[0]), (result, segment) => QualifiedName(result, IdentifierName(segment)));
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
                                                    IdentifierName("ParameterAttribute"))
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
                                            List(members))
                           .AddBaseListTypes(
                               SimpleBaseType(
                                   IdentifierName("IEventDTO")))
                           .WithAttributeLists(
                               SingletonList(
                                   AttributeList(
                                       SingletonSeparatedList(
                                           Attribute(
                                                   IdentifierName("EventAttribute"))
                                               .AddArgumentListArguments(AttributeArgument(
                                                                             LiteralExpression(
                                                                                 SyntaxKind
                                                                                     .StringLiteralExpression,
                                                                                 Literal(
                                                                                     abi.Name))))))));

            outputTypes.Add(eventDto);

            return MethodDeclaration(
                    GenericName("Event")
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
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("Web3"),
                                        IdentifierName("Eth")),
                                    GenericName("GetEvent")
                                        .AddTypeArgumentListArguments(IdentifierName(eventDto.Identifier))))
                            .WithArgumentList(
                                ArgumentList(
                                    SingletonSeparatedList(
                                        Argument(
                                            IdentifierName(
                                                "Address")))))))
                .WithSemicolonToken(
                    Token(SyntaxKind.SemicolonToken));
        }

        private static MethodDeclarationSyntax GetSendTxMethodDeclarationSyntax(ArgumentSyntax[] callParameters,
                                                                                Abi abi,
                                                                                ParameterSyntax[] methodParameters,
                                                                                SyntaxToken contractProperty)
        {
            var gasIdentifier = Identifier("gas");
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
                            IdentifierName("Address"))),
                    Argument(
                        IdentifierName(gasIdentifier))
                }.Concat(callParameters)
                 .ToArray();

            var parameters = methodParameters
                             .Concat(new[]
                             {
                                 Parameter(
                                         gasIdentifier)
                                     .WithType(
                                         IdentifierName("HexBigInteger"))
                                     .WithDefault(
                                         EqualsValueClause(
                                             LiteralExpression(
                                                 SyntaxKind.NullLiteralExpression)))
                             })
                             .ToArray();

            return MethodDeclaration(
                    GenericName(
                            Identifier("Task"))
                        .AddTypeArgumentListArguments(
                            IdentifierName("TransactionReceipt")),
                    Identifier(Capitalize(abi.Name) + "Async"))
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    parameters)
                .WithExpressionBody(
                    ArrowExpressionClause(
                        InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    InvocationExpression(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                IdentifierName(contractProperty),
                                                IdentifierName("GetFunction")))
                                        .AddArgumentListArguments(
                                            Argument(
                                                LiteralExpression(
                                                    SyntaxKind.StringLiteralExpression,
                                                    Literal(abi.Name)))),
                                    IdentifierName("SendDefaultTransactionAndWaitForReceiptAsync")))
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
                methodName = "CallAsync";
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
                                        IdentifierName("FunctionOutputAttribute"))))))
                    .AddMembers(outputParameters
                                    .Select((output,
                                             i) => PropertyDeclaration(IdentifierName(output.Type),
                                                                       output.Name.Trim('_'))
                                                .WithAttributeLists(
                                                    SingletonList(
                                                        AttributeList(
                                                            SingletonSeparatedList(
                                                                Attribute(
                                                                        IdentifierName("ParameterAttribute"))
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
                methodName = "CallDeserializingToObjectAsync";
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
                                                IdentifierName(contractProperty),
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

        private static IEnumerable<MethodDeclarationSyntax> GetDeployDeclarations(SyntaxToken web3Identifier,
                                                                         ParameterSyntax[] methodParameters,
                                                                         ClassDeclarationSyntax contractClassDeclaration,
                                                                         SyntaxToken abiIdentifier,
                                                                         SyntaxToken binIdentifier,
                                                                         IdentifierNameSyntax[] initializerParameters)
        {
            var gasIdentifier = Identifier("gas");
            var constructorParameters = new[]
                {
                    Parameter(
                            web3Identifier)
                        .WithType(
                            IdentifierName("Web3"))
                }.Concat(methodParameters)
                 .Concat(new[]
                 {
                     Parameter(
                             gasIdentifier)
                         .WithType(
                             IdentifierName("HexBigInteger"))
                         .WithDefault(
                             EqualsValueClause(
                                 LiteralExpression(
                                     SyntaxKind.NullLiteralExpression)))
                 })
                 .ToArray();

            var receiptSyntaxToken = Identifier("receipt");
            var deployedContractSyntaxToken = Identifier("deployedContract");
            var deploymentResultType = GenericName(
                    "DeploymentResult")
                .AddTypeArgumentListArguments(IdentifierName(contractClassDeclaration.Identifier));
            const string deployAndGetReceiptAsyncMethodName = "DeployAndGetReceiptAsync";
            yield return MethodDeclaration(
                    GenericName(
                            Identifier("Task"))
                        .AddTypeArgumentListArguments(
                            deploymentResultType),
                    Identifier(deployAndGetReceiptAsyncMethodName))
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
                                                            IdentifierName("ContractBase"),
                                                            IdentifierName("DeployAsync")))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            IdentifierName(web3Identifier)),
                                                        Argument(
                                                            IdentifierName(abiIdentifier)),
                                                        Argument(
                                                            IdentifierName(binIdentifier)),
                                                        Argument(
                                                            IdentifierName(gasIdentifier)),
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
                                                                                  IdentifierName("ContractAddress")))))))),
                    ReturnStatement(
                        ObjectCreationExpression(deploymentResultType)
                            .AddArgumentListArguments(
                                Argument(IdentifierName(deployedContractSyntaxToken)),
                                Argument(IdentifierName(receiptSyntaxToken)))));

            var arguments = constructorParameters.Select(x => Argument(IdentifierName(x.Identifier)))
                                                 .ToArray();

            yield return MethodDeclaration(
                             GenericName(
                                     Identifier("Task"))
                                 .AddTypeArgumentListArguments(
                                     IdentifierName(contractClassDeclaration.Identifier)),
                             Identifier("DeployAsync"))
                         .AddModifiers(
                             Token(SyntaxKind.PublicKeyword),
                             Token(SyntaxKind.StaticKeyword))
                         .AddParameterListParameters(
                             constructorParameters)
                .WithExpressionBody(
                    ArrowExpressionClause(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                InvocationExpression(
                                    IdentifierName("DeployAndGetReceiptAsync"))
                                .WithArgumentList(
                                    ArgumentList(
                                        SeparatedList(
                                            arguments))),
                                IdentifierName("ContinueWith")))
                        .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList(
                                    Argument(
                                        SimpleLambdaExpression(
                                            Parameter(
                                                Identifier("r")),
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName("r"),
                                                    IdentifierName("Result")),
                                                IdentifierName("Value")))))))))
                .WithSemicolonToken(
                    Token(SyntaxKind.SemicolonToken));
        }

        private static string Capitalize(string value) => string.IsNullOrEmpty(value) ? value : $"{char.ToUpper(value[0])}{value.Substring(1)}";
        private static string Decapitalize(string value) => string.IsNullOrEmpty(value) ? value : $"{char.ToLower(value[0])}{value.Substring(1)}";

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
                           ? name
                           : missingReplacement;
                Type = type;
                OriginalType = originalType;
                Indexed = indexed;
            }
        }
    }
}

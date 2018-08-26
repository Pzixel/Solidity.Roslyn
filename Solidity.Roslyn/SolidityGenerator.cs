using System;
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
            var solidityFiles = Directory.EnumerateFiles(context.ProjectDirectory, "*.sol", SearchOption.AllDirectories).Select(File.ReadAllText);
            var contracts = solidityFiles.SelectMany(file => Regex.Matches(file, @"^\s*contract\s+(\w+)", RegexOptions.Multiline).Cast<Match>().Select(x => x.Groups[1].Value)).ToArray();

            var results = contracts.Select(x=>ClassDeclaration(x)
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
                                        Literal("Some abi"))))))
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.ConstKeyword)))));

            return Task.FromResult(List<MemberDeclarationSyntax>(results));
        }
    }
}

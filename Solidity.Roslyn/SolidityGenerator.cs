using System;
using System.Diagnostics.CodeAnalysis;
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
        public SolidityGenerator(AttributeData _)
        {
        }

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context,
                                                                       IProgress<Diagnostic> progress,
                                                                       CancellationToken cancellationToken)
        {
            var results = new[]
            {
                ClassDeclaration("BuildInfo")
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .WithMembers(
                        SingletonList<MemberDeclarationSyntax>(
                            FieldDeclaration(
                                    VariableDeclaration(
                                            IdentifierName("System.DateTime"))
                                        .WithVariables(
                                            SingletonSeparatedList(
                                                VariableDeclarator(
                                                        Identifier("BuldTime"))
                                                    .WithInitializer(
                                                        EqualsValueClause(
                                                            InvocationExpression(
                                                                    MemberAccessExpression(
                                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                                        IdentifierName("System.DateTime"),
                                                                        IdentifierName("FromBinary")))
                                                                .WithArgumentList(
                                                                    ArgumentList(
                                                                        SingletonSeparatedList(
                                                                            Argument(
                                                                                LiteralExpression(
                                                                                    SyntaxKind.NumericLiteralExpression,
                                                                                    Literal(DateTime.Now.ToBinary())))))))))))
                                .WithModifiers(
                                    TokenList(
                                        new []{
                                            Token(SyntaxKind.PublicKeyword),
                                            Token(SyntaxKind.StaticKeyword),
                                            Token(SyntaxKind.ReadOnlyKeyword)}))))
            };
            return Task.FromResult(List<MemberDeclarationSyntax>(results));
        }
    }
}

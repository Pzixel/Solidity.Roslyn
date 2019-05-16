using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Solidity.Roslyn
{
    internal static class XGenerator
    {
        public static ClassDeclarationSyntax WithPragma(this ClassDeclarationSyntax classDeclaration)
        {
            return classDeclaration
                   .WithLeadingTrivia(Trivia(PragmaWarningDirectiveTrivia(Token(SyntaxKind.DisableKeyword), true)
                                                 .WithErrorCodes(
                                                     SeparatedList<ExpressionSyntax>(
                                                         new[]
                                                         {
                                                             LiteralExpression(
                                                                 SyntaxKind.NumericLiteralExpression,
                                                                 Literal(108)),
                                                             LiteralExpression(
                                                                 SyntaxKind.NumericLiteralExpression,
                                                                 Literal(114))
                                                         }))))
                   .WithTrailingTrivia(Trivia(PragmaWarningDirectiveTrivia(Token(SyntaxKind.RestoreKeyword), true)
                                                  .WithErrorCodes(
                                                      SeparatedList<ExpressionSyntax>(
                                                          new[]
                                                          {
                                                              LiteralExpression(
                                                                  SyntaxKind.NumericLiteralExpression,
                                                                  Literal(108)),
                                                              LiteralExpression(
                                                                  SyntaxKind.NumericLiteralExpression,
                                                                  Literal(114))
                                                          }))));
        }
    }
}

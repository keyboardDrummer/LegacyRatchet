﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DebtAnalyzer.MethodDebt;
using DebtAnalyzer.ParameterCount;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DebtAnalyzer.DebtAnnotation
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TechnicalDebtAnnotationProvider)), Shared]
	public class TechnicalDebtAnnotationProvider : CodeFixProvider
	{
		public const string Title = "Update technical debt annotation";

		public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
			MethodParameterCountAnalyzer.DiagnosticId, MethodLengthAnalyzer.DiagnosticId);

		public sealed override FixAllProvider GetFixAllProvider()
		{
			return new MyFixAllProvider();
		}

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var syntaxRoot = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			
			var diagnostic = context.Diagnostics.First();
			var methodSyntax = (BaseMethodDeclarationSyntax)syntaxRoot.FindNode(context.Span);

			context.RegisterCodeFix(CodeAction.Create(Title, async c =>
			{
				var project = await AddDebtAnnotation(context.Document, methodSyntax, c);
				return project.Solution;
			}, methodSyntax.SpanStart + ""), diagnostic);
		}

		public static async Task<Project> AddDebtAnnotation(Document document, BaseMethodDeclarationSyntax methodBaseDecl, CancellationToken cancellationToken)
		{
			var syntaxRoot = (CompilationUnitSyntax) await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

			syntaxRoot = syntaxRoot.ReplaceNode(methodBaseDecl, GetNewMethod(methodBaseDecl));
			syntaxRoot = AddUsing(syntaxRoot);

			var newDocument = document.WithSyntaxRoot(syntaxRoot);
			return newDocument.Project;
		}

		public static BaseMethodDeclarationSyntax GetNewMethod(BaseMethodDeclarationSyntax methodBaseDecl)
		{
			var attributeSyntax = GetAttribute(methodBaseDecl);
			return (BaseMethodDeclarationSyntax)new UpdateOrAddDebtAttribute(attributeSyntax).Visit(methodBaseDecl);
		}

		public static CompilationUnitSyntax AddUsing(CompilationUnitSyntax syntaxRoot)
		{
			var debtAnalyzerNamespace = SyntaxFactory.IdentifierName("DebtAnalyzer");
			var usingDirectiveSyntax = SyntaxFactory.UsingDirective(
				debtAnalyzerNamespace).WithUsingKeyword(SyntaxFactory.Token(SyntaxKind.UsingKeyword))
				.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

			return syntaxRoot.Usings.All(@using => (@using.Name as IdentifierNameSyntax)?.Identifier.ValueText != debtAnalyzerNamespace.Identifier.ValueText) 
				? syntaxRoot.AddUsings(usingDirectiveSyntax) 
				: syntaxRoot;
		}

		public static AttributeSyntax GetAttribute(BaseMethodDeclarationSyntax methodBaseDecl)
		{
			var attributeType = typeof (DebtMethod);
			var lineCountArgument = GetLineCountArgument(methodBaseDecl);
			var parameterCountArgument = GetParameterCountArgument(methodBaseDecl);
			var attributeArgumentSyntaxs = new List<AttributeArgumentSyntax> {lineCountArgument, parameterCountArgument};
			return SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(attributeType.Name), SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(attributeArgumentSyntaxs)));
		}

		static AttributeArgumentSyntax GetParameterCountArgument(BaseMethodDeclarationSyntax methodBaseDecl)
		{
			var parameterCountArgument = GetNamedAttributeArgument(nameof(DebtMethod.ParameterCount), methodBaseDecl.ParameterList.Parameters.Count);
			return parameterCountArgument;
		}

		static AttributeArgumentSyntax GetLineCountArgument(BaseMethodDeclarationSyntax methodBaseDecl)
		{
			var methodLength = MethodLengthAnalyzer.GetMethodLength(methodBaseDecl);
			return GetNamedAttributeArgument(nameof(DebtMethod.LineCount), methodLength);
		}

		static AttributeArgumentSyntax GetNamedAttributeArgument(string parameterName, object parameterValue)
		{

			return SyntaxFactory.AttributeArgument(
				SyntaxFactory.LiteralExpression(
					SyntaxKind.NumericLiteralExpression,
					SyntaxFactory.Literal(
						SyntaxFactory.TriviaList(),
						parameterValue.ToString(),
						parameterValue.ToString(),
						SyntaxFactory.TriviaList())))
				.WithNameEquals(
					SyntaxFactory.NameEquals(
						SyntaxFactory.IdentifierName(
							parameterName))
						.WithEqualsToken(
							SyntaxFactory.Token(
								SyntaxKind.EqualsToken)));
		}
	}
}
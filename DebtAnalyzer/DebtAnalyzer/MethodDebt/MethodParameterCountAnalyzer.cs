using System.Linq;
using DebtAnalyzer.Common;
using DebtAnalyzer.DebtAnnotation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DebtAnalyzer.ParameterCount
{
	public class MethodParameterCountAnalyzer
	{
		public const string DiagnosticId = "MaxParameterCount";

		private static readonly LocalizableString title = new LocalizableResourceString(nameof(Resources.TooManyParametersTitle), Resources.ResourceManager, typeof(Resources));
		private static readonly LocalizableString messageFormat = new LocalizableResourceString(nameof(Resources.TooManyParametersMessage), Resources.ResourceManager, typeof(Resources));
		public static int DefaultMaximumParameterCount = 8;

		public void AnalyzeSymbol(SymbolAnalysisContext context)
		{
			var methodSymbol = (IMethodSymbol)context.Symbol;
			var maxParameterCount = GetMaxParameterCount(methodSymbol.ContainingAssembly);
			var previousParameterCount = GetPreviousParameterCount(methodSymbol);
			var parameterCount = methodSymbol.Parameters.Length;
			if (parameterCount > previousParameterCount && parameterCount > maxParameterCount)
			{
				var severity = DebtAsErrorUtil.GetDiagnosticSeverity(methodSymbol);
				var descriptor = CreateDiagnosticDescriptor(severity);
				var diagnostic = Diagnostic.Create(descriptor, methodSymbol.Locations[0], methodSymbol.Name, parameterCount, maxParameterCount);

				context.ReportDiagnostic(diagnostic);
			}
		}

		public DiagnosticDescriptor CreateDiagnosticDescriptor(DiagnosticSeverity severity)
		{
			return new DiagnosticDescriptor(DiagnosticId, title, messageFormat, "Debt", severity, true);
		}

		static int GetPreviousParameterCount(IMethodSymbol methodSymbol)
		{
			return MethodDebtAnalyzer.GetDebtMethods(methodSymbol.GetAttributes()).FirstOrDefault()?.ParameterCount ?? 0;
		}

		public static int GetMaxParameterCount(IAssemblySymbol assembly)
		{
			return assembly.GetAttributes().Where(data => data.AttributeClass.Name == typeof (MaxParameters).Name && data.ConstructorArguments.Length > 0).
				Select(data => data.ConstructorArguments[0].Value as int?).FirstOrDefault() ?? DefaultMaximumParameterCount;
		}
	}
}
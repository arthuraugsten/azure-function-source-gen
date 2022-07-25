using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Functions.SourceGen.Services;

[Generator]
internal sealed class ServiceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
            ctx.AddSource(
                $"{ServiceAttributeGenHelper.AttributeName}.g.cs",
                SourceText.From(ServiceAttributeGenHelper.AttributeSourceCode, Encoding.UTF8)
            )
        );

        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s), // select classes with attributes
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx) // sect the class with the [ServicesSourceGenAttribute] attribute
            ).Where(static m => m is not null)!; // filter out attributed classes that we don't care about

        var compilationAndEnums = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndEnums, static (spc, source) => Execute(source.Item1, source.Item2!, spc));
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node) => node is ClassDeclarationSyntax m && m.AttributeLists.Count > 0;

    private static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;

        foreach (var attributeListSyntax in classDeclarationSyntax.AttributeLists)
        {
            foreach (var attributeSyntax in attributeListSyntax.Attributes)
            {
                if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is not IMethodSymbol attributeSymbol)
                    continue;

                var attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                var fullName = attributeContainingTypeSymbol.ToDisplayString();

                if (fullName == ServiceAttributeGenHelper.FullName)
                    return classDeclarationSyntax;
            }
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty)
            return;

        var distinctFunctions = classes.Distinct();
        var functionsToGenerate = GetTypesToGenerate(compilation, distinctFunctions, context.CancellationToken);

        if (functionsToGenerate.Count == 0)
            return;

        foreach (var function in functionsToGenerate)
        {
            var serviceSourceCode = GenerateServiceInterface(function);
            context.AddSource($"{function.Name}.g.cs", SourceText.From(serviceSourceCode, Encoding.UTF8));
        }

        var dependencyInjection = GenerateDependencyInjection(functionsToGenerate);
        context.AddSource("DependencyInjection.g.cs", SourceText.From(dependencyInjection, Encoding.UTF8));
    }

    private static List<ServiceInfo> GetTypesToGenerate(Compilation compilation, IEnumerable<ClassDeclarationSyntax> functions, CancellationToken ct)
    {
        var functionsToGenerate = new List<ServiceInfo>();
        var functionAttribute = compilation.GetTypeByMetadataName(ServiceAttributeGenHelper.FullName);

        if (functionAttribute is null)
            return functionsToGenerate;

        foreach (var classDeclarationSyntax in functions)
        {
            ct.ThrowIfCancellationRequested();

            var semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is not INamedTypeSymbol classSymbol)
                continue;

            var serviceNamespace = classSymbol.ContainingNamespace.ToDisplayString();

            foreach (var attributeData in classSymbol.GetAttributes())
            {
                if (!functionAttribute.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                    continue;

                foreach (var namedArgument in attributeData.NamedArguments)
                {
                    if (namedArgument.Key == ServiceAttributeGenHelper.ServiceNamespacePropertyName && namedArgument.Value.Value?.ToString() is { } n)
                        serviceNamespace = n;
                }

                break;
            }

            functionsToGenerate.Add(
                new ServiceInfo(
                    classSymbol.Name,
                    serviceNamespace
                )
            );
        }

        return functionsToGenerate;
    }

    private static string GenerateServiceInterface(ServiceInfo functionInfo)
    {
        var source =
@$"using System.Threading.Tasks;

namespace {functionInfo.Namespace};

public interface I{functionInfo.Name}
{{
    Task HandleAsync();
}}";

        return source;
    }

    private static string GenerateDependencyInjection(List<ServiceInfo> functionsInfo)
    {
        var sourceCode = new StringBuilder(5000)
            .Append(
@$"using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Functions.SourceGen.DependencyInjection;

public static class DependencyInjectionExtension
{{
    public static void AddSourceGenDI(this IFunctionsHostBuilder builder)
    {{
");

        foreach (var functionInfo in functionsInfo)
        {
            sourceCode.AppendLine(
                $@"        builder.Services.AddScoped<{functionInfo.Namespace}.I{functionInfo.Name}, {functionInfo.Namespace}.{functionInfo.Name}>();"
            );
        }

        sourceCode.Append(@"    }
}");

        return sourceCode.ToString();
    }
}

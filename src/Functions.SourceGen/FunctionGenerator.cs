using Functions.SourceGen.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Functions.SourceGen;

[Generator]
public sealed class FunctionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
            ctx.AddSource(
                $"{AttributeGenHelper.AttributeName}.g.cs",
                SourceText.From(AttributeGenHelper.AttributeSourceCode, Encoding.UTF8)
            )
        );

        var enumDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s), // select classes with attributes
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx) // sect the class with the [FunctionSourceGenAttribute] attribute
            ).Where(static m => m is not null)!; // filter out attributed classes that we don't care about

        var compilationAndEnums = context.CompilationProvider.Combine(enumDeclarations.Collect());

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

                if (fullName == AttributeGenHelper.FullName)
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
            var functionSourceCode = GenerateFunctionsClass(function);
            context.AddSource($"{function.Name}.g.cs", SourceText.From(functionSourceCode, Encoding.UTF8));

            var serviceSourceCode = GenerateServiceClass(function);
            context.AddSource($"{function.ServiceName}.g.cs", SourceText.From(serviceSourceCode, Encoding.UTF8));
        }

        var dependencyInjection = GenerateDependencyInjection(functionsToGenerate);
        context.AddSource("DependencyInjection.g.cs", SourceText.From(dependencyInjection, Encoding.UTF8));
    }

    private static List<FunctionInfo> GetTypesToGenerate(Compilation compilation, IEnumerable<ClassDeclarationSyntax> functions, CancellationToken ct)
    {
        var functionsToGenerate = new List<FunctionInfo>();
        var functionAttribute = compilation.GetTypeByMetadataName(AttributeGenHelper.FullName);

        if (functionAttribute is null)
            return functionsToGenerate;

        foreach (var classDeclarationSyntax in functions)
        {
            ct.ThrowIfCancellationRequested();

            var semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is not INamedTypeSymbol classSymbol)
                continue;

            var serviceName = $"{classSymbol.Name.Substring(0, classSymbol.Name.IndexOf("Function"))}Service";
            var serviceInterface = $"I{serviceName}";
            var serviceNamespace = classSymbol.ContainingNamespace.ToDisplayString();

            foreach (var attributeData in classSymbol.GetAttributes())
            {
                if (!functionAttribute.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                    continue;

                foreach (var namedArgument in attributeData.NamedArguments)
                {
                    if (namedArgument.Key == AttributeGenHelper.ServiceNamespacePropertyName && namedArgument.Value.Value?.ToString() is { } n)
                        serviceNamespace = n;
                }

                break;
            }

            functionsToGenerate.Add(
                new FunctionInfo(
                    serviceNamespace,
                    classSymbol.Name,
                    classSymbol.ContainingNamespace.ToDisplayString(),
                    serviceName,
                    serviceInterface
                )
            );
        }

        return functionsToGenerate;
    }

    private static string GenerateFunctionsClass(FunctionInfo functionInfo)
    {
        var source =
@$"using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace {functionInfo.Namespace};

public partial class {functionInfo.Name}
{{ 
    private readonly ILogger<{functionInfo.Name}> _logger;
    private readonly {functionInfo.ServiceNamespace}.I{functionInfo.ServiceName} _service;

    public {functionInfo.Name}(ILogger<{functionInfo.Name}> logger, {functionInfo.ServiceNamespace}.I{functionInfo.ServiceName} service)
    {{
        _logger = logger;
        _service = service;
    }}

    [FunctionName(""{functionInfo.Name}"")]
    public void Run([QueueTrigger(""myqueue-items"")]string myQueueItem)
    {{
        _logger.LogInformation($""C# Queue trigger function processed: {{myQueueItem}}"");
    }}

    [FunctionName(""{functionInfo.Name}PoisonQueue"")]
    public void PoisonQueue([QueueTrigger(""myqueue-items"")]string myQueueItem, ILogger log)
    {{
        log.LogInformation($""C# Queue trigger function processed: {{myQueueItem}}"");
    }}
}}";

        return source;
    }

    private static string GenerateServiceClass(FunctionInfo functionInfo)
    {
        var source =
@$"using System.Threading.Tasks;

namespace {functionInfo.ServiceNamespace};

public interface {functionInfo.ServiceInterface}
{{
    Task HandleAsync();
}}";

        return source;
    }

    private static string GenerateDependencyInjection(List<FunctionInfo> functionsInfo)
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
                $@"        builder.Services.AddScoped<{functionInfo.ServiceNamespace}.{functionInfo.ServiceInterface}, {functionInfo.ServiceNamespace}.{functionInfo.ServiceName}>();"
            );
        }

        sourceCode.Append(@"    }
}");

        return sourceCode.ToString();
    }
}

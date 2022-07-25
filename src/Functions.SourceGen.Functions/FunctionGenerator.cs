using Functions.SourceGen.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Functions.SourceGen.Functions;

[Generator]
public sealed class FunctionGenerator : GeneratorBase, IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
            ctx.AddSource(
                $"{FunctionAttributeGenHelper.AttributeName}.g.cs",
                SourceText.From(FunctionAttributeGenHelper.AttributeSourceCode, Encoding.UTF8)
            )
        );

        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s), // select classes with attributes
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx, FunctionAttributeGenHelper.FullName) // sect the class with the [FunctionSourceGenAttribute] attribute
            ).Where(static m => m is not null)!; // filter out attributed classes that we don't care about

        var compilationAndEnums = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndEnums, static (spc, source) => Execute(source.Item1, source.Item2!, spc));
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
        }
    }

    private static List<FunctionInfo> GetTypesToGenerate(Compilation compilation, IEnumerable<ClassDeclarationSyntax> functions, CancellationToken ct)
    {
        var functionsToGenerate = new List<FunctionInfo>();
        var functionAttribute = compilation.GetTypeByMetadataName(FunctionAttributeGenHelper.FullName);

        if (functionAttribute is null)
            return functionsToGenerate;

        foreach (var classDeclarationSyntax in functions)
        {
            ct.ThrowIfCancellationRequested();

            var semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is not INamedTypeSymbol classSymbol)
                continue;

            var serviceName = $"{classSymbol.Name.Substring(0, classSymbol.Name.IndexOf("Function"))}Service";
            var serviceNamespace = classSymbol.ContainingNamespace.ToDisplayString();

            foreach (var attributeData in classSymbol.GetAttributes())
            {
                if (!functionAttribute.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                    continue;

                foreach (var namedArgument in attributeData.NamedArguments)
                {
                    if (namedArgument.Key == FunctionAttributeGenHelper.ServiceNamespacePropertyName && namedArgument.Value.Value?.ToString() is { } n)
                        serviceNamespace = n;
                }

                break;
            }

            functionsToGenerate.Add(
                new FunctionInfo(
                    serviceNamespace,
                    classSymbol.Name,
                    classSymbol.ContainingNamespace.ToDisplayString(),
                    serviceName
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
}

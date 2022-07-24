using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Functions.SourceGen.Tests;

public static class TestHelper
{
    public static Task Verify(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(assemblyName: "Tests", syntaxTrees: new[] { syntaxTree });

        var generator = new FunctionGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);

        return Verifier.Verify(driver);
    }
}

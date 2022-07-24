namespace Functions.SourceGen.Attributes;

internal static class AttributeGenHelper
{
    internal const string NameSpace = "Functions.SourceGen.Attributes";
    internal const string AttributeName = "FunctionSourceGenAttribute";
    internal const string FullName = $"{NameSpace}.{AttributeName}";
    internal const string ServiceNamespacePropertyName = "ServiceNamespace";


    internal const string AttributeSourceCode = 
@$"namespace {NameSpace};

[System.AttributeUsage(System.AttributeTargets.Class)]
public class {AttributeName} : System.Attribute
{{
    public string {ServiceNamespacePropertyName} {{ get; set; }}
}}";
}

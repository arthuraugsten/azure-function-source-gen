namespace Functions.SourceGen.Services;

internal static class ServiceAttributeGenHelper
{
    internal const string NameSpace = "Functions.SourceGen.Attributes";
    internal const string AttributeName = "ServiceSourceGenAttribute";
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

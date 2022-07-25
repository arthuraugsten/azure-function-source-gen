namespace Functions.SourceGen.Functions;

internal readonly struct FunctionInfo
{
    public FunctionInfo(string serviceNamespace, string name, string @namespace, string serviceName)
    {
        ServiceNamespace = serviceNamespace;
        Name = name;
        Namespace = @namespace;
        ServiceName = serviceName;
    }

    public readonly string ServiceNamespace;
    public readonly string Name;
    public readonly string Namespace;
    public readonly string ServiceName;
}

namespace Functions.SourceGen;

public readonly struct FunctionInfo
{
    public FunctionInfo(string serviceNamespace, string name, string @namespace, string serviceName, string serviceInterface)
    {
        ServiceNamespace = serviceNamespace;
        Name = name;
        Namespace = @namespace;
        ServiceName = serviceName;
        ServiceInterface = serviceInterface;
    }

    public readonly string ServiceNamespace;
    public readonly string Name;
    public readonly string Namespace;
    public readonly string ServiceName;
    public readonly string ServiceInterface;
}

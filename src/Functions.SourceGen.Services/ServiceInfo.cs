namespace Functions.SourceGen.Services;

public readonly struct ServiceInfo
{
    public ServiceInfo(string name, string @namespace)
    {
        Name = name;
        Namespace = @namespace;
    }

    public readonly string Name;
    public readonly string Namespace;
}

namespace Functions.SourceGen.Services;

internal readonly struct ServiceInfo
{
    public ServiceInfo(string name, string @namespace)
    {
        Name = name;
        Namespace = @namespace;
    }

    public readonly string Name;
    public readonly string Namespace;
}

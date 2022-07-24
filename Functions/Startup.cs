using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Functions.SourceGen.DependencyInjection;

namespace Functions;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.AddSourceGenDI();
    }
}

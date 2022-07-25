using Functions.SourceGen.Attributes;

namespace Functions.ApplicationService.Services;

[ServiceSourceGen]
public sealed class OrderService : IOrderService
{
    public Task HandleAsync()
    {
        throw new NotImplementedException();
    }
}

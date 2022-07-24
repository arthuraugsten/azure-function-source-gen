using System.Threading.Tasks;

namespace Functions.Services;
public class OrderService : IOrderService
{
    public Task HandleAsync()
    {
        return Task.CompletedTask;
    }
}

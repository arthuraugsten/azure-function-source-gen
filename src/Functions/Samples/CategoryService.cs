using System.Threading.Tasks;

namespace Functions.Samples;

public partial class CategoryService : ICategoryService
{
    public Task HandleAsync()
    {
        System.Console.WriteLine("Handle");
        return Task.CompletedTask;
    }
}

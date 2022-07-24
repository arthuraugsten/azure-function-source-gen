using System.Threading.Tasks;

namespace Functions;

public partial class EmployeeService : IEmployeeService
{
    public Task HandleAsync()
    {
        System.Console.WriteLine("Handle");
        return Task.CompletedTask;
    }
}

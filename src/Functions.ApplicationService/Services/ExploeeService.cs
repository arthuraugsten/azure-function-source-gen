using Functions.SourceGen.Attributes;

namespace Functions.ApplicationService.Services;

[ServiceSourceGen]
public sealed class EmployeeService : IEmployeeService
{
    public Task HandleAsync()
    {
        throw new NotImplementedException();
    }
}

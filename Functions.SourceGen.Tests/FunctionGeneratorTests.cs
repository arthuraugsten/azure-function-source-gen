namespace Functions.SourceGen.Tests;

[UsesVerify]
public class FunctionGeneratorTests
{
    [Fact]
    public async Task GeneratesEnumExtensionsCorrectly()
    {
        // The source code to test
        var source = 
@"using Functions.SourceGen.Attributes;

namespace Functions;

[FunctionSourceGen]
public partial class EmployeeFunction { }";

        await TestHelper.Verify(source);
    }
}

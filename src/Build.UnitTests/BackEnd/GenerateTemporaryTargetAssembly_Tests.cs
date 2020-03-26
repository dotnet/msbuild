using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    sealed public class GenerateTemporaryTargetAssembly_Tests
    {
        [Fact]
        public void FailsWithOnlyTargetErrors()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectFailure(@"
<Project>
    <UsingTask TaskName=""FailingBuilderTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests"" />
    <Target Name=""MyTarget"">
        <FailingBuilderTask CurrentProject="".\otherproj.csproj"" />
    </Target>
</Project>");
            logger.ErrorCount.ShouldBe(1);
        }
    }
}

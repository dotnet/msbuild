
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class GenerateTemporaryTargetAssembly_Tests
    {
        [Fact]
        public void FailsWithOnlyTargetErrors()
        {
            using (TestEnvironment testEnv = TestEnvironment.Create())
            {
                TransientTestFolder folder = testEnv.CreateFolder(createFolder: true);
                TransientTestFile toBuild = testEnv.CreateFile(folder, "ProjectToBuild.csproj", @"
<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <UsingTask
    TaskName=""Microsoft.Build.Tasks.Windows.GenerateTemporaryTargetAssembly""
    AssemblyFile=""C:\Program Files\Reference Assemblies\Microsoft\Framework\v3.0\PresentationBuildTasks.dll"" />
  <Target Name=""GenerateTemporaryTargetAssemblyTask"">
    <GenerateTemporaryTargetAssembly
      AssemblyName=""FullBuild""
      CompileTargetName=""CoreCompile""
      CompileTypeName=""Compile""
      CurrentProject=""FullBuild.proj""
      IntermediateOutputPath="".\obj\bug\""
      MSBuildBinPath=""$(MSBuildBinPath)""
      ReferencePathTypeName=""ReferencePath"" />
  </Target>
</Project>");
                TransientTestFile fullBuild = testEnv.CreateFile(folder, "FullBuild.proj", @"
<Project>
  <Target Name=""Inside"">
    <Error />
  </Target>
</Project>
");
                MockLogger logger = ObjectModelHelpers.BuildProjectExpectFailure(toBuild.Path);
                logger.ErrorCount.ShouldBe(1);
            }
        }
    }
}

using System;
namespace Microsoft.NET.Build.Tests
{
    public class ProjectConfigurationDescription : SdkTest
    {
        public void ProjectConfigurationDescription_DefaultTest()
        {
            var testProj = new TestProject()
            {
                Name = "CompilationConstants",
                TargetFrameworks = "netcoreapp2.1;netcoreapp3.1",
                IsExe = true,
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProj);
            File.WriteAllText(Path.Combine(testAsset.Path, testProj.Name, $"{testProj.Name}.cs"), @"
            using System;
            class Program
            {
                static void Main(string[] args)
                {
                    #if NETCOREAPP2_1
                        Consol.WriteLine(""NETCOREAPP2_1"");
                    #endif
                    #if NETCOREAPP3_1
                        Console.WriteLine(""NETCOREAPP3_1"");
                    #endif
                }
            }");

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.Path, testProj.Name));
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var runCommand = new RunExeCommand(Log, Path.Combine(buildCommand.GetOutputDirectory(targetFramework).FullName, $"{testProj.Name}.exe"));
            var stdOut = runCommand.Execute().StdOut.Split(Environment.NewLine.ToCharArray()).Where(line => !string.IsNullOrWhiteSpace(line));
            stdOut.Should().BeEquivalentTo(expectedOutput);

            //line 745 can be used to do the should fail expected output
        }
    }
}

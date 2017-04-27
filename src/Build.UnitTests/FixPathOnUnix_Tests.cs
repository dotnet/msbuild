using System;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    [PlatformSpecific(Xunit.PlatformID.AnyUnix)]
    public  class FixPathOnUnixTests
    {
        string _assemblyPath;

        public FixPathOnUnixTests()
        {
            _assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        }

        [Fact]
        public void TestPathFixupInMetadata()
        {
            string buildProjectContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='Build'>
                        <MSBuild Projects='projectDirectory/main.proj' />
                    </Target>
               </Project>";

            string mainProjectContents = String.Format(@"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <UsingTask TaskName='TestTask' AssemblyFile='{0}' />
                    <ItemGroup>
                        <Item0 Include='xyz'>
                            <Md0>lib\foo.dll</Md0>
                        </Item0>
                    </ItemGroup>
                    <Target Name='Build'>
                        <TestTask Items='@(Item0)' />
                    </Target>
                </Project>", _assemblyPath);

            string buildProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("build.proj", buildProjectContents);
            string mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("projectDirectory/main.proj", mainProjectContents);
            ObjectModelHelpers.CreateFileInTempProjectDirectory(Path.Combine("projectDirectory", "lib", "foo.dll"), "just a text file");

            var projColln = new ProjectCollection();
            var logger = new MockLogger();
            projColln.RegisterLogger(logger);

            var project = projColln.LoadProject(buildProjectPath);
            var result = project.Build("Build");

            Assert.True(result);

            logger.AssertLogContains($"ItemMetadata: Md0 = {Path.Combine("lib", "foo.dll")}");
        }
    }

    public class TestTask : Task
    {
        public ITaskItem[] Items { get; set; }

        public override bool Execute()
        {
            if (Items != null)
            {
                foreach (var item in Items)
                {
                    Log.LogMessage ($"Item: {item.ItemSpec}");
                    foreach (string name in item.MetadataNames)
                    {
                        Log.LogMessage ($"ItemMetadata: {name} = {item.GetMetadata(name)}");
                    }
                }
            }

            return true;
        }
    }
}

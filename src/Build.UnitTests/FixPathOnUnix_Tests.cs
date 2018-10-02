using System;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    [PlatformSpecific(TestPlatforms.AnyUnix)]
    public  class FixPathOnUnixTests
    {
        [Fact]
        public void TestPathFixupInMetadata()
        {
            string buildProjectContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='Build'>
                        <MSBuild Projects='projectDirectory/main.proj' />
                    </Target>
               </Project>";

            string mainProjectContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <UsingTask TaskName='LogTaskPropertiesTask' AssemblyName='Microsoft.Build.Engine.UnitTests' />
                    <ItemGroup>
                        <Item0 Include='xyz'>
                            <Md0>lib\foo.dll</Md0>
                        </Item0>
                    </ItemGroup>
                    <Target Name='Build'>
                        <LogTaskPropertiesTask Items='@(Item0)' />
                    </Target>
                </Project>";

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

    public class LogTaskPropertiesTask : Task
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

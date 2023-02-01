// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.NetCore.Extensions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class FixPathOnUnixTests
    {
        [UnixOnlyFact]
        public void TestPathFixupInMetadata()
        {
            string buildProjectContents = @"
                <Project>
                    <Target Name='Build'>
                        <MSBuild Projects='projectDirectory/main.proj' />
                    </Target>
               </Project>";

            string mainProjectContents = @"
                <Project>
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
            ObjectModelHelpers.CreateFileInTempProjectDirectory("projectDirectory/main.proj", mainProjectContents);
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
                    Log.LogMessage($"Item: {item.ItemSpec}");
                    foreach (string name in item.MetadataNames)
                    {
                        Log.LogMessage($"ItemMetadata: {name} = {item.GetMetadata(name)}");
                    }
                }
            }

            return true;
        }
    }
}

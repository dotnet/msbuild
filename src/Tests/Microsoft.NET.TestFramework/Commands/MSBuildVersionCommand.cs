// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Commands
{
    public class MSBuildVersionCommand : TestCommand
    { 
        public MSBuildVersionCommand(ITestOutputHelper log) : base(log) {}

        protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            if (TestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild)
            {
                return new SdkCommandSpec()
                {
                    FileName = TestContext.Current.ToolsetUnderTest.FullFrameworkMSBuildPath,
                    Arguments = { "-version" },
                    WorkingDirectory = WorkingDirectory
                };
            }
            else
            {
                return new SdkCommandSpec()
                {
                    FileName = TestContext.Current.ToolsetUnderTest.DotNetHostPath,
                    Arguments = { "msbuild", "-version" },
                    WorkingDirectory = WorkingDirectory
                };
            }
        }
    }
}

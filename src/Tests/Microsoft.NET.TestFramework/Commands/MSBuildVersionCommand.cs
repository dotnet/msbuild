// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Xunit.Abstractions;

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

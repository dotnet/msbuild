// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Commands
{
    public class DotnetBuildCommand : DotnetCommand
    {
        public DotnetBuildCommand(ITestOutputHelper log, params string[] args) : base(log)
        {
            Arguments.Add("build");
            Arguments.AddRange(args);
        }

        public DotnetBuildCommand(TestAsset testAsset, params string[] args) : this(testAsset.Log, args)
        {
            if (testAsset.TestProject != null)
            {
                WorkingDirectory = Path.Combine(testAsset.TestRoot, testAsset.TestProject.Name);
            }
            else
            {
                WorkingDirectory = testAsset.TestRoot;
            }
        }
    }
}

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Test {
    public class TestExec : Exec
    {
        public override bool Execute() => true;
    }

    public class TestCsc : Csc
    {
        public string MSBuildBinPath { get; set; }
        public string AsmVersion { get; set; }

        public override bool Execute() => Path.Combine(MSBuildBinPath, $"Microsoft.Build.Tasks.{AsmVersion}.dll")
                                            == typeof(Csc).Assembly.Location;
    }

    // tests Microsoft.Build.Utilities
    public class TestTask : ToolTask
    {
        public override bool Execute () => Path.GetFileName(typeof(ToolTask).Assembly.Location) == "Microsoft.Build.Utilities.Core.dll";

        protected override string GenerateFullPathToTool () => null;

        protected override string ToolName { get { return null; } }

    }
}

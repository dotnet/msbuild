// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Test {
    public class TestExec : Exec
    {
        public override bool Execute() => true;
    }

    public class TestCsc : Csc
    {
        public override bool Execute() => true;
    }

    // tests Microsoft.Build.Utilities
    public class TestTask : ToolTask
    {
        public override bool Execute () => true;

        protected override string GenerateFullPathToTool () => null;

        protected override string ToolName { get { return null; } }

    }
}

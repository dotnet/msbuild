// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Reads an environment variable from inside the (possibly out-of-proc) task execution process and returns
    /// its value together with the executing process id. Used to verify that an out-of-proc task host applies the
    /// build process environment correctly, including across consecutive tasks where the environment apply may be
    /// skipped as an optimization.
    /// </summary>
    public class ReadEnvironmentVariableTask : Task
    {
        [Required]
        public string VariableName { get; set; }

        [Output]
        public string Value { get; set; }

        [Output]
        public int Pid { get; set; }

        public override bool Execute()
        {
            Value = Environment.GetEnvironmentVariable(VariableName) ?? string.Empty;
            Pid = Process.GetCurrentProcess().Id;
            return true;
        }
    }
}

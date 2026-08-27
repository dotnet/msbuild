// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Sets an environment variable from inside the (possibly out-of-proc) task execution process and returns the
    /// executing process id. Used together with <see cref="ReadEnvironmentVariableTask"/> to verify that an
    /// environment change made by one task-host task is observed by a subsequent task-host task on the same
    /// connection, i.e. that the build process environment is re-sent (not falsely deduplicated) after it changes.
    /// </summary>
    public class SetEnvironmentVariableTask : Task
    {
        [Required]
        public string VariableName { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        [Output]
        public int Pid { get; set; }

        public override bool Execute()
        {
            Environment.SetEnvironmentVariable(VariableName, Value);
            Pid = Process.GetCurrentProcess().Id;
            return true;
        }
    }
}

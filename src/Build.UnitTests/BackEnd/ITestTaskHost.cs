// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Interface used by the test task to communicate what the TaskExecutionHost did to it.
    /// </summary>
    internal interface ITestTaskHost : ITaskHost
    {
        /// <summary>
        /// Called when a parameter is set on the task.
        /// </summary>
        void ParameterSet(string parameterName, object valueSet);

        /// <summary>
        /// Called when an output is read from the task.
        /// </summary>
        void OutputRead(string parameterName, object actualValue);
    }
}

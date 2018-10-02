// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;

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

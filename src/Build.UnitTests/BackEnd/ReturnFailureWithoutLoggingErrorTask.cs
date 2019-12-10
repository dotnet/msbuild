// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// This task was created for https://github.com/microsoft/msbuild/issues/2036
    /// </summary>
    public class ReturnFailureWithoutLoggingErrorTask : Task
    {
        /// <summary>
        /// Intentionally return false without logging an error to test proper error catching.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            return false;
        }
    }
}

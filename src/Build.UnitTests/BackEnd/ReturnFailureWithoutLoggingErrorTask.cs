// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;
#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// This task was created for https://github.com/dotnet/msbuild/issues/2036
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

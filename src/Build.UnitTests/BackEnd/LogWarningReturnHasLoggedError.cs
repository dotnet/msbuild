// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// This task was created for https://github.com/microsoft/msbuild/issues/2036
    /// </summary>
    public class LogWarningReturnHasLoggedError : Task
    {
        [Required]
        public string WarningCode { get; set; }

        /// <summary>
        /// Log a warning and return whether or not the TaskLoggingHelper knows it was turned into an error.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            Log.LogWarning(null, WarningCode, null, null, 0, 0, 0, 0, "Warning Logged!", null);

            // This is what tasks should return by default.
            return !Log.HasLoggedErrors;
        }
    }
}

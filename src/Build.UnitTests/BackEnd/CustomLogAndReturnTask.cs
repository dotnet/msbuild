// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
namespace Microsoft.Build.UnitTests
{
    public class CustomLogAndReturnTask : Task
    {
        public string WarningCode { get; set; }

        public string ErrorCode { get; set; }

        public bool ReturnHasLoggedErrors { get; set; }

        [Required]
        public bool Return { get; set; }

        // Unused for now, created for task batching.
        public ITaskItem[] Sources { get; set; }

        /// <summary>
        /// This task returns and logs what you want based on the running test.
        /// </summary>
        public override bool Execute()
        {
            if(!string.IsNullOrEmpty(WarningCode))
            {
                Log.LogWarning(null, WarningCode, null, null, 0, 0, 0, 0, "Warning Logged!", null);
            }

            if(!string.IsNullOrEmpty(ErrorCode))
            {
                Log.LogError(null, ErrorCode, null, null, 0, 0, 0, 0, "Error Logged!", null);
            }
            return ReturnHasLoggedErrors ? !Log.HasLoggedErrors : Return;
        }
    }
}

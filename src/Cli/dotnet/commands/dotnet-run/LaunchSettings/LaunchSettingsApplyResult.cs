// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Run.LaunchSettings
{
    public class LaunchSettingsApplyResult
    {
        public LaunchSettingsApplyResult(bool success, string failureReason, ProjectLaunchSettingsModel launchSettings = null)
        {
            Success = success;
            FailureReason = failureReason;
            LaunchSettings = launchSettings;
        }

        public bool Success { get; }

        public string FailureReason { get; }

        public ProjectLaunchSettingsModel LaunchSettings { get; }
    }
}

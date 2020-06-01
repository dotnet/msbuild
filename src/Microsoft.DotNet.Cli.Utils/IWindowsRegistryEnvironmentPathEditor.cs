// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Utils
{
    internal interface IWindowsRegistryEnvironmentPathEditor
    {
        string Get(SdkEnvironmentVariableTarget sdkEnvironmentVariableTarget);
        void Set(string value, SdkEnvironmentVariableTarget sdkEnvironmentVariableTarget);
    }

    internal enum SdkEnvironmentVariableTarget
    {
        DotDefault,
        // the current user could be DotDefault if it is running under System
        CurrentUser
    }
}

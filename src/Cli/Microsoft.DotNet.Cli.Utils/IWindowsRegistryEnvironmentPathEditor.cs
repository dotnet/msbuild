// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

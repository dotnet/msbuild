// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools
{
    internal interface IFilePermissionSetter
    {
        void SetUserExecutionPermission(string path);
        void SetPermission(string path, string chmodArgument);
    }
}

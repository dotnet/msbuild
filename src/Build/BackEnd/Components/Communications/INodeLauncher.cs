// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Build.BackEnd
{
    internal interface INodeLauncher
    {
        Process Start(string msbuildLocation, string commandLineArgs, int nodeId);
    }
}

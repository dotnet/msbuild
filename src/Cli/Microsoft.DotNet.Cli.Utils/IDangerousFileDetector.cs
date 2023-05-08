// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    internal interface IDangerousFileDetector
    {
        bool IsDangerous(string filePath);
    }
}

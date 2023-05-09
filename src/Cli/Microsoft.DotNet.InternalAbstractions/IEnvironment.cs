// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.EnvironmentAbstractions
{
    internal interface IEnvironment
    {
        string GetEnvironmentVariable(string name);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.BuildCheck.Infrastructure;

internal sealed class BuildCheckConfigurationException : Exception
{
    /// <summary>
    /// Exception to communicate issues with user specified configuration - unsupported scenarios, malformations, etc.
    /// This exception usually leads to defuncting the particular analyzer for the rest of the build (even if issue occured with a single project).
    /// </summary>
    public BuildCheckConfigurationException(string message) : base(message)
    {
    }
}

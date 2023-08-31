// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_REPORTFILEACCESSES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Factory for creating the DetouredNodeLauncher
    /// </summary>
    /// <remarks>
    /// Must be a separate class to avoid loading the BuildXL assemblies when not opted in.
    /// </remarks>
    internal static class DetouredNodeLauncherFactory
    {
        public static IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrowArgumentOutOfRange(type == BuildComponentType.NodeLauncher, nameof(type));
            return new DetouredNodeLauncher();
        }
    }
}
#endif

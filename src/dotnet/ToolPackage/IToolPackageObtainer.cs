// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackage
{
    internal interface IToolPackageObtainer
    {
        ToolConfigurationAndExecutablePath ObtainAndReturnExecutablePath(
            string packageId, 
            string packageVersion = null, 
            FilePath? nugetconfig = null, 
            string targetframework = null,
            string source = null,
            string verbosity = null);
    }
}

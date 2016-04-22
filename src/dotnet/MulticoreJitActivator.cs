// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;    
using System.Runtime.Loader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli
{
    public class MulticoreJitActivator
    {
        public bool TryActivateMulticoreJit()
        {
            var disableMulticoreJit = IsMulticoreJitDisabled();
                
            if (disableMulticoreJit)
            {
                return false;
            }

            StartCliProfileOptimization();
            
            return true;
        }
        
        private bool IsMulticoreJitDisabled()
        {
            return Environment.GetEnvironmentVariable("DOTNET_DISABLE_MULTICOREJIT") == "1";
        }
        
        private void StartCliProfileOptimization()
        {
            var profileOptimizationRootPath = new MulticoreJitProfilePathCalculator().MulticoreJitProfilePath;

            PathUtility.EnsureDirectory(profileOptimizationRootPath);
            
            AssemblyLoadContext.Default.SetProfileOptimizationRoot(profileOptimizationRootPath);
            
            AssemblyLoadContext.Default.StartProfileOptimization("dotnet");
        }
    }
}
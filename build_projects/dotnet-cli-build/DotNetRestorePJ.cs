// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Build
{
    public class DotNetRestorePJ : DotNetTool
    {
        protected override string Command 
        { 
            get { return "restore"; } 
        } 
 
        protected override string Args 
        { 
            get { return $"{GetVerbosity()} {GetFallbackSource()} {GetPackages()} {GetDisableParallel()}"; } 
        } 
 
        public string FallbackSource { get; set; } 
 
        public string Packages { get; set; } 
 
        public string Verbosity { get; set; } 
 
        private string GetFallbackSource() 
        { 
            if (!string.IsNullOrEmpty(FallbackSource)) 
            { 
                return $"--fallbacksource {FallbackSource}"; 
            } 
 
            return null; 
        } 
 
        private string GetPackages() 
        { 
            if (!string.IsNullOrEmpty(Packages)) 
            { 
                return $"--packages {Packages}"; 
            } 
 
            return null; 
        } 
 
        private string GetVerbosity() 
        { 
            if (!string.IsNullOrEmpty(Verbosity)) 
            { 
                return $"--verbosity {Verbosity}"; 
            } 
 
            return null; 
        } 
 
        private string GetDisableParallel() 
        { 
            return "--disable-parallel"; 
        } 
    }
}

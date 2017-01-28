// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    public abstract class DotNetMSBuildTool : DotNetTool
    {
        public int MaxCpuCount {get; set;} = 0;

        protected override string Args 
        { 
            get
            {
                return $"{GetMaxCpuCountArg()}";
            } 
        }

        private string GetMaxCpuCountArg()
        {
            if (MaxCpuCount > 0)
            {
                return $"/m:{MaxCpuCount}";
            }

            return null;
        }
    }
}

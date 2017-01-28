// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Build
{
    public class DotNetMSBuild : DotNetMSBuildTool
    {
        protected override string Command
        {
            get { return "msbuild"; }
        }

        protected override string Args
        {
            get { return $"{base.Args} {GetArguments()}"; }
        }

        public string Arguments { get; set; }

        private string GetArguments()
        {
            if (!string.IsNullOrEmpty(Arguments))
            {
                return $"{Arguments}";
            }

            return null;
        }
    }
}

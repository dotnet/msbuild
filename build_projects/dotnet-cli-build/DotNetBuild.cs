// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Build
{
    public class DotNetBuild : DotNetMSBuildTool
    {
        protected override string Command
        {
            get { return "build"; }
        }

        protected override string Args
        {
            get { return $"{base.Args} {GetProjectPath()} {GetConfiguration()} {GetFramework()} {GetRuntime()} {GetOutputPath()}"; }
        }

        public string BuildBasePath { get; set; }

        public string Configuration { get; set; }

        public string Framework { get; set; }
        
        public string Runtime { get; set; }

        public string ProjectPath { get; set; }

        public string OutputPath { get; set; }
        
        private string GetOutputPath()
        {
            if (!string.IsNullOrEmpty(OutputPath))
            {
                return $"--output {OutputPath}";
            }

            return null;
        }

        private string GetConfiguration()
        {
            if (!string.IsNullOrEmpty(Configuration))
            {
                return $"--configuration {Configuration}";
            }

            return null;
        }

        private string GetFramework()
        {
            if (!string.IsNullOrEmpty(Framework))
            {
                return $"--framework {Framework}";
            }

            return null;
        }
        
        private string GetRuntime()
        {
            if (!string.IsNullOrEmpty(Runtime))
            {
                return $"--runtime {Runtime}";
            }

            return null;
        }

        private string GetProjectPath()
        {
            if (!string.IsNullOrEmpty(ProjectPath))
            {
                return $"{ProjectPath}";
            }

            return null;
        }
    }
}

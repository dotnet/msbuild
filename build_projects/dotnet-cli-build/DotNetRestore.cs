// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Build
{
    public class DotNetRestore : DotNetTool
    {
        protected override string Command
        {
            get { return "restore"; }
        }

        protected override string Args
        {
            get { return $"{GetProjectPath()} {GetSource()} {GetPackages()} {GetSkipInvalidConfigurations()} {GetRuntime()} {GetAdditionalParameters()}"; }
        }

        public string AdditionalParameters { get; set; }

        public string ProjectPath { get; set; }

        public string Source { get; set; }

        public string Packages { get; set; }

        public bool SkipInvalidConfigurations { get; set; }
        
        public string Runtime { get; set; }

        private string GetSource()
        {
            if (!string.IsNullOrEmpty(Source))
            {
                return $"--source {Source}";
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

        private string GetProjectPath()
        {
            if (!string.IsNullOrEmpty(ProjectPath))
            {
                return $"{ProjectPath}";
            }

            return null;
        }

        private string GetSkipInvalidConfigurations()
        {
            if (SkipInvalidConfigurations)
            {
                return "/p:SkipInvalidConfigurations=true";
            }

            return null;
        }
        
        private string GetRuntime()
        {
            if (!string.IsNullOrEmpty(Runtime))
            {
                return $"/p:RuntimeIdentifier={Runtime}";
            }

            return null;
        }

        private string GetAdditionalParameters()
        {
            return AdditionalParameters;
        }
    }
}

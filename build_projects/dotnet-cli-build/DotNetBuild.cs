using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class DotNetBuild : DotNetTool
    {
        protected override string Command
        {
            get { return "build"; }
        }

        protected override string Args
        {
            get { return $"{GetProjectPath()} {GetConfiguration()} {GetFramework()} {GetBuildBasePath()}"; }
        }

        public string BuildBasePath { get; set; }

        public string Configuration { get; set; }

        public string Framework { get; set; }

        public string ProjectPath { get; set; }

        private string GetBuildBasePath()
        {
            if (!string.IsNullOrEmpty(BuildBasePath))
            {
                return $"--build-base-path {BuildBasePath}";
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

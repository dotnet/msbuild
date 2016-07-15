// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Build
{
    public class DotNetTest : DotNetTool
    {
        protected override string Command
        {
            get { return "test"; }
        }

        protected override string Args
        {
            get { return $"{GetConfiguration()} {GetNoBuild()} {GetXml()} {GetNoTrait()}"; }
        }

        public string Configuration { get; set; }

        public string Xml { get; set; }

        public string NoTrait { get; set; }

        public bool NoBuild { get; set; }

        private string GetConfiguration()
        {
            if (!string.IsNullOrEmpty(Configuration))
            {
                return $"--configuration {Configuration}";
            }

            return null;
        }

        private string GetNoTrait()
        {
            if (!string.IsNullOrEmpty(Configuration))
            {
                return $"-notrait {NoTrait}";
            }

            return null;
        }

        private string GetXml()
        {
            if (!string.IsNullOrEmpty(Xml))
            {
                return $"-xml {Xml}";
            }

            return null;
        }

        private string GetNoBuild()
        {
            if (NoBuild)
            {
                return "--no-build";
            }

            return null;
        }
    }
}

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Build
{
    public class DotNetNew : DotNetTool
    {
        protected override string Command
        {
            get { return "new"; }
        }

        protected override string Args
        {
            get { return $"{GetTemplateType()}"; }
        }

        public string TemplateType { get; set; }

        private string GetTemplateType()
        {
            if (!string.IsNullOrEmpty(TemplateType))
            {
                return $"--type {TemplateType}";
            }

            return null;
        }
    }
}

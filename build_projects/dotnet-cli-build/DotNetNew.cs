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
            get { return $"{TemplateType} {TemplateArgs}"; }
        }

        public string TemplateType { get; set; }

        public string TemplateArgs { get; set; }
    }
}

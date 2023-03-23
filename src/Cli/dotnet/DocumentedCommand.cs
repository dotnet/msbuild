// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public class DocumentedCommand : Command
    {
        public string DocsLink { get; set; }

        public DocumentedCommand(string name, string docsLink, string description = null) : base(name, description)
        {
            DocsLink = docsLink;
        }
    }
}

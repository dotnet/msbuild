// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public class DocumentedCommand : CliCommand
    {
        public string DocsLink { get; set; }

        public DocumentedCommand(string name, string docsLink, string description = null) : base(name, description)
        {
            DocsLink = docsLink;
        }
    }
}

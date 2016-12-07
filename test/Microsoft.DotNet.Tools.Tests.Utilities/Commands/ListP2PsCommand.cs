// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class ListP2PsCommand : TestCommand
    {
        private string _projectName = null;

        public ListP2PsCommand()
            : base("dotnet")
        {
        }

        public override CommandResult Execute(string args = "")
        {
            args = $"list {_projectName} p2ps {args}";
            return base.ExecuteWithCapturedOutput(args);
        }

        public ListP2PsCommand WithProject(string projectName)
        {
            _projectName = projectName;
            return this;
        }
    }
}

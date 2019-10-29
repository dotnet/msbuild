// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class ListReferenceCommand : DotnetCommand
    {
        private string _projectName = null;

        public override CommandResult Execute(string args = "")
        {
            args = $"list {_projectName} reference {args}";
            return base.ExecuteWithCapturedOutput(args);
        }

        public ListReferenceCommand WithProject(string projectName)
        {
            _projectName = projectName;
            return this;
        }
    }
}

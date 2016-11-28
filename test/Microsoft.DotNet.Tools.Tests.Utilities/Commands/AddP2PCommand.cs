// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class AddP2PCommand : TestCommand
    {
        private string _projectName = null;

        public AddP2PCommand()
            : base("dotnet")
        {
        }

        public override CommandResult Execute(string args = "")
        {
            args = $"add {_projectName} p2p {args}";
            return base.ExecuteWithCapturedOutput(args);
        }

        public AddP2PCommand WithProject(string projectName)
        {
            _projectName = projectName;
            return this;
        }
    }
}

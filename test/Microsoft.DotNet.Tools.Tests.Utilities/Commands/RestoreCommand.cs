// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class RestoreCommand : DotnetCommand
    {
        private string _runtime;

        public RestoreCommand WithRuntime(string runtime)
        {
            _runtime = runtime;

            return this;
        }

        public override CommandResult Execute(string args = "")
        {
            args = $"restore {GetRuntime()} {args} --disable-parallel";
            return base.Execute(args);
        }

        public override CommandResult ExecuteWithCapturedOutput(string args = "")
        {
            args = $"restore {GetRuntime()} {args} --disable-parallel";
            return base.ExecuteWithCapturedOutput(args);
        }

        private string GetRuntime()
        {
            if (_runtime == null)
            {
                return null;
            }

            return $"-property:RuntimeIdentifier={_runtime}";
        }
    }
}

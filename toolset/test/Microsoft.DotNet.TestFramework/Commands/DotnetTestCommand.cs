// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class DotnetTestCommand : DotnetCommand
    {
        private string _runtime;

        public override CommandResult Execute(string args = "")
        {
            args = $"test {GetRuntime()} {args}";
            return base.Execute(args);
        }
        
        public override CommandResult ExecuteWithCapturedOutput(string args = "")
        {
            args = $"test {GetRuntime()} {args}";
            return base.ExecuteWithCapturedOutput(args);
        }

        public DotnetTestCommand WithRuntime(string runtime)
        {
            _runtime = runtime;

            return this;
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

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Test
{
    public class DotnetTestRunnerFactory : IDotnetTestRunnerFactory
    {
        public IDotnetTestRunner Create(int? port)
        {
            IDotnetTestRunner dotnetTestRunner = new ConsoleTestRunner();
            if (port.HasValue)
            {
                dotnetTestRunner = new DesignTimeRunner();
            }

            return dotnetTestRunner;
        }
    }
}

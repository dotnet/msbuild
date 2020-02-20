// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.MSBuild
{
    public class MSBuildCommand
    {
        public static int Run(string[] args)
        {
            return new MSBuildForwardingApp(args).Execute();
        }
    }
}

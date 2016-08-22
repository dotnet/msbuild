// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test
{
    public interface IDotnetTestRunner
    {
        int RunTests(ProjectContext projectContext, DotnetTestParams dotnetTestParams, BuildWorkspace workspace);
    }
}

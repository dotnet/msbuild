// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Sdk;
using Microsoft.DotNet.Tools.List;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.New;
using Microsoft.DotNet.Tools.NuGet;
using Microsoft.DotNet.Tools.Pack;
using Microsoft.DotNet.Tools.Publish;
using Microsoft.DotNet.Tools.Remove;
using Microsoft.DotNet.Tools.Restore;
using Microsoft.DotNet.Tools.Run;
using Microsoft.DotNet.Tools.Sln;
using Microsoft.DotNet.Tools.Store;
using Microsoft.DotNet.Tools.Test;
using Microsoft.DotNet.Tools.VSTest;
using System.Collections.Generic;
using Microsoft.DotNet.Tools.Tool;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Tools.Format;

namespace Microsoft.DotNet.Cli
{
    public static class BuiltInCommandsCatalog
    {
        public static Dictionary<string, BuiltInCommandMetadata> Commands = new Dictionary<string, BuiltInCommandMetadata>
        {
        };
    }
}

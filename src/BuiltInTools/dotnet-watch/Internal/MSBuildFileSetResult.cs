// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Watcher.Internal
{
    public class MSBuildFileSetResult
    {
        public string RunCommand { get; set; }

        public string RunArguments { get; set; }

        public string RunWorkingDirectory { get; set; }

        public bool IsNetCoreApp { get; set; }

        public string TargetFrameworkVersion { get; set; }

        public Dictionary<string, ProjectItems> Projects { get; set; }
    }

    public class ProjectItems
    {
        public List<string> Files { get; set; } = new();

        public List<StaticFileItem> StaticFiles { get; set; } = new();
    }

    public class StaticFileItem
    {
        public string FilePath { get; set; }

        public string StaticWebAssetPath { get; set; }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.DotNet.Watcher.Internal
{
    [DataContract]
    internal sealed class MSBuildFileSetResult
    {
        [DataMember]
        public string RunCommand { get; init; }

        [DataMember]
        public string RunArguments { get; init; }

        [DataMember]
        public string RunWorkingDirectory { get; init; }

        [DataMember]
        public bool IsNetCoreApp { get; init; }

        [DataMember]
        public string TargetFrameworkVersion { get; init; }

        [DataMember]
        public string RuntimeIdentifier { get; init; }

        [DataMember]
        public string DefaultAppHostRuntimeIdentifier { get; init; }

        [DataMember]
        public Dictionary<string, ProjectItems> Projects { get; init; }
    }

    [DataContract]
    internal sealed class ProjectItems
    {
        [DataMember]
        public List<string> Files { get; init; } = new();

        [DataMember]
        public List<StaticFileItem> StaticFiles { get; init; } = new();
    }

    [DataContract]
    internal sealed class StaticFileItem
    {
        [DataMember]
        public string FilePath { get; init; }

        [DataMember]
        public string StaticWebAssetPath { get; init; }
    }
}

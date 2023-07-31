// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;

namespace Microsoft.DotNet.Watcher.Internal
{
    [DataContract]
    internal sealed class MSBuildFileSetResult
    {
        [DataMember]
        public required string RunCommand { get; init; }

        [DataMember]
        public required string RunArguments { get; init; }

        [DataMember]
        public required string RunWorkingDirectory { get; init; }

        [DataMember]
        public required bool IsNetCoreApp { get; init; }

        [DataMember]
        public required string TargetFrameworkVersion { get; init; }

        [DataMember]
        public required string RuntimeIdentifier { get; init; }

        [DataMember]
        public required string DefaultAppHostRuntimeIdentifier { get; init; }

        [DataMember]
        public required Dictionary<string, ProjectItems> Projects { get; init; }
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
        public required string FilePath { get; init; }

        [DataMember]
        public required string StaticWebAssetPath { get; init; }
    }
}

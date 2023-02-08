// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

public static class KnownStrings
{
    public static class Properties
    {
        public static readonly string ContainerBaseImage = nameof(ContainerBaseImage);
        public static readonly string ContainerRegistry = nameof(ContainerRegistry);
        public static readonly string ContainerImageName = nameof(ContainerImageName);
        public static readonly string ContainerImageTag = nameof(ContainerImageTag);
        public static readonly string ContainerImageTags = nameof(ContainerImageTags);
        public static readonly string ContainerWorkingDirectory = nameof(ContainerWorkingDirectory);
        public static readonly string ContainerEntrypoint = nameof(ContainerEntrypoint);
        public static readonly string UseAppHost = nameof(UseAppHost);
        public static readonly string ContainerLabel = nameof(ContainerLabel);
        public static readonly string SelfContained = nameof(SelfContained);
        public static readonly string ContainerPort = nameof(ContainerPort);
        public static readonly string ContainerEnvironmentVariable = nameof(ContainerEnvironmentVariable);

        public static readonly string ComputeContainerConfig = nameof(ComputeContainerConfig);
        public static readonly string AssemblyName = nameof(AssemblyName);
        public static readonly string ContainerBaseRegistry = nameof(ContainerBaseRegistry);
        public static readonly string ContainerBaseName = nameof(ContainerBaseName);
        public static readonly string ContainerBaseTag = nameof(ContainerBaseTag);

        public static readonly string ContainerGenerateLabels = nameof(ContainerGenerateLabels);
    }

    public static class ErrorCodes
    {
        public static readonly string CONTAINER001 = nameof(CONTAINER001);
        public static readonly string CONTAINER004 = nameof(CONTAINER004);
        public static readonly string CONTAINER005 = nameof(CONTAINER005);
    }
}
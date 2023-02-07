// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

public static class KnownStrings
{
    public static class Properties
    {
        public static string ContainerBaseImage = nameof(ContainerBaseImage);
        public static string ContainerRegistry = nameof(ContainerRegistry);
        public static string ContainerImageName = nameof(ContainerImageName);
        public static string ContainerImageTag = nameof(ContainerImageTag);
        public static string ContainerImageTags = nameof(ContainerImageTags);
        public static string ContainerWorkingDirectory = nameof(ContainerWorkingDirectory);
        public static string ContainerEntrypoint = nameof(ContainerEntrypoint);
        public static string UseAppHost = nameof(UseAppHost);
        public static string ContainerLabel = nameof(ContainerLabel);
        public static string SelfContained = nameof(SelfContained);
        public static string ContainerPort = nameof(ContainerPort);
        public static string ContainerEnvironmentVariable = nameof(ContainerEnvironmentVariable);

        public static string ComputeContainerConfig = nameof(ComputeContainerConfig);
        public static string AssemblyName = nameof(AssemblyName);
        public static string ContainerBaseRegistry = nameof(ContainerBaseRegistry);
        public static string ContainerBaseName = nameof(ContainerBaseName);
        public static string ContainerBaseTag = nameof(ContainerBaseTag);

        public static string ContainerGenerateLabels = nameof(ContainerGenerateLabels);
    }

    public static class ErrorCodes
    {
        public static string CONTAINER001 = nameof(CONTAINER001);
        public static string CONTAINER004 = nameof(CONTAINER004);
        public static string CONTAINER005 = nameof(CONTAINER005);
    }
}
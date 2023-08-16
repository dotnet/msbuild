// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

internal static class KnownStrings
{
    public static class Properties
    {
        public static readonly string ContainerBaseImage = nameof(ContainerBaseImage);
        public static readonly string ContainerFamily = nameof(ContainerFamily);
        public static readonly string _ContainerBaseImageTag = nameof(_ContainerBaseImageTag);
        public static readonly string ContainerRegistry = nameof(ContainerRegistry);
        /// <summary>Note that this is deprecated in favor of <see cref="ContainerRepository"/></summary>
        public static readonly string ContainerImageName = nameof(ContainerImageName);
        public static readonly string ContainerRepository = nameof(ContainerRepository);
        public static readonly string ContainerImageTag = nameof(ContainerImageTag);
        public static readonly string ContainerImageTags = nameof(ContainerImageTags);
        public static readonly string ContainerWorkingDirectory = nameof(ContainerWorkingDirectory);
        public static readonly string ContainerEntrypoint = nameof(ContainerEntrypoint);
        public static readonly string UseAppHost = nameof(UseAppHost);
        public static readonly string ContainerLabel = nameof(ContainerLabel);
        public static readonly string SelfContained = nameof(SelfContained);
        public static readonly string ContainerPort = nameof(ContainerPort);
        public static readonly string ContainerEnvironmentVariable = nameof(ContainerEnvironmentVariable);

        public static readonly string ComputeContainerBaseImage = nameof(ComputeContainerBaseImage);
        public static readonly string _ComputeContainerBaseImageTag = nameof(_ComputeContainerBaseImageTag);
        public static readonly string ComputeContainerConfig = nameof(ComputeContainerConfig);
        public static readonly string AssemblyName = nameof(AssemblyName);
        public static readonly string ContainerBaseRegistry = nameof(ContainerBaseRegistry);
        public static readonly string ContainerBaseName = nameof(ContainerBaseName);
        public static readonly string ContainerBaseTag = nameof(ContainerBaseTag);

        public static readonly string ContainerGenerateLabels = nameof(ContainerGenerateLabels);

        public static readonly string ContainerRuntimeIdentifier = nameof(ContainerRuntimeIdentifier);
    }

    public static class ErrorCodes
    {
        public static readonly string CONTAINER002 = nameof(CONTAINER002);
        public static readonly string CONTAINER003 = nameof(CONTAINER003);
        public static readonly string CONTAINER1011 = nameof(CONTAINER1011);
        public static readonly string CONTAINER1012 = nameof(CONTAINER1012);
        public static readonly string CONTAINER1013 = nameof(CONTAINER1013);

        public static readonly string CONTAINER2005 = nameof(CONTAINER2005);
        public static readonly string CONTAINER2007 = nameof(CONTAINER2007);
        public static readonly string CONTAINER2008 = nameof(CONTAINER2008);
        public static readonly string CONTAINER2009 = nameof(CONTAINER2009);
        public static readonly string CONTAINER2010 = nameof(CONTAINER2010);
        public static readonly string CONTAINER2012 = nameof(CONTAINER2012);

        public static readonly string CONTAINER4001 = nameof(CONTAINER4001);
        public static readonly string CONTAINER4002 = nameof(CONTAINER4002);
        public static readonly string CONTAINER4003 = nameof(CONTAINER4003);
        public static readonly string CONTAINER4004 = nameof(CONTAINER4004);
    }
}

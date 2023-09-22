// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

public static class KnownLocalRegistryTypes
{
    public const string Docker = nameof(Docker);
    public const string Podman = nameof(Podman);

    public static readonly string[] SupportedLocalRegistryTypes = new[] { Docker, Podman };

    internal static ILocalRegistry CreateLocalRegistry(string? type, ILoggerFactory loggerFactory)
    {
        if (string.IsNullOrEmpty(type))
        {
            return new DockerCli(null, loggerFactory);
        }

        return type switch
        {
            Podman => new DockerCli(DockerCli.PodmanCommand, loggerFactory),
            Docker => new DockerCli(DockerCli.DockerCommand, loggerFactory),
            _ => throw new NotSupportedException(
                Resource.FormatString(
                    nameof(Strings.UnknownLocalRegistryType),
                    type,
                    string.Join(",", SupportedLocalRegistryTypes)))
        };
    }
}

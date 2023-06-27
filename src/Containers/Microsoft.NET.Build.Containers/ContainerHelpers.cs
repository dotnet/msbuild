// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System;
#endif
#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Text.RegularExpressions;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;
public static class ContainerHelpers
{
    internal const string HostObjectUser = "SDK_CONTAINER_REGISTRY_UNAME";

    internal const string HostObjectPass = "SDK_CONTAINER_REGISTRY_PWORD";

    internal const string ForceChunkedUploadEnabled = "SDK_CONTAINER_DEBUG_REGISTRY_FORCE_CHUNKED_UPLOAD";
    internal const string ChunkedUploadSizeBytes = "SDK_CONTAINER_REGISTRY_CHUNKED_UPLOAD_SIZE_BYTES";

    internal const string ParallelUploadEnabled = "SDK_CONTAINER_REGISTRY_PARALLEL_UPLOAD";

    internal const string DockerRegistryAlias = "docker.io";

    /// <summary>
    /// Matches an environment variable name - must start with a letter or underscore, and can only contain letters, numbers, and underscores.
    /// </summary>
    private static Regex envVarRegex = new Regex(@"^[a-zA-Z_]{1,}[a-zA-Z0-9_]*$");

    /// <summary>
    /// Matches if the string is not lowercase or numeric, or ., _, or -.
    /// </summary>
    /// <remarks>Technically the period should be allowed as well, but due to inconsistent support between cloud providers we're removing it.</remarks>
    private static Regex imageNameCharacters = new Regex(@"[^a-z0-9_\-/]");


    /// <summary>
    /// The enum contains possible error reasons during port parsing using <see cref="TryParsePort(string, out Port?, out ParsePortError?)"/> or <see cref="TryParsePort(string?, string?, out Port?, out ParsePortError?)"/>.
    /// </summary>
    [Flags]
    public enum ParsePortError
    {
        MissingPortNumber,
        InvalidPortNumber,
        InvalidPortType,
        UnknownPortFormat
    }

    /// <summary>
    /// Tries to parse the port from <paramref name="portNumber"/> and <paramref name="portType"/>.
    /// </summary>
    /// <param name="portNumber">The port number to parse.</param>
    /// <param name="portType">The port type to parse (tcp or udp).</param>
    /// <param name="port">Parsed port.</param>
    /// <param name="error">The error occurred during parsing. Only returned when method returns <see langword=""="false"/>.</param>
    /// <returns><see langword=""="true"/> when port was successfully parsed, <see langword=""="false"/> otherwise.</returns>
    public static bool TryParsePort(string? portNumber, string? portType, [NotNullWhen(true)] out Port? port, [NotNullWhen(false)] out ParsePortError? error)
    {
        var portNo = 0;
        error = null;
        if (String.IsNullOrEmpty(portNumber))
        {
            error = ParsePortError.MissingPortNumber;
        }
        else if (!int.TryParse(portNumber, out portNo))
        {
            error = ParsePortError.InvalidPortNumber;
        }

        if (!Enum.TryParse<PortType>(portType, out PortType t))
        {
            if (portType is not null)
            {
                error = (error ?? ParsePortError.InvalidPortType) | ParsePortError.InvalidPortType;
            }
            else
            {
                t = PortType.tcp;
            }
        }

        if (error is null)
        {
            port = new Port(portNo, t);
            return true;
        }
        else
        {
            port = null;
            return false;
        }

    }

    /// <summary>
    /// Tries to parse the port from <paramref name="input"/>.
    /// </summary>
    /// <param name="input">The port number to parse. Expected formats are: port number as int value, or value in format 'port number/port type' where
    /// port type can be tcp or udp. If the port type is not present, it is assumed to be tcp.</param>
    /// <param name="port">Parsed port.</param>
    /// <param name="error">The error occurred during parsing. Only returned when method returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when port was successfully parsed, <see langword="false"/> otherwise.</returns>
    public static bool TryParsePort(string input, [NotNullWhen(true)] out Port? port, [NotNullWhen(false)] out ParsePortError? error)
    {
        var parts = input.Split('/');
        if (parts.Length == 2)
        {
            string portNumber = parts[0];
            string type = parts[1];
            return TryParsePort(portNumber, type, out port, out error);
        }
        else if (parts.Length == 1)
        {
            string portNum = parts[0];
            return TryParsePort(portNum, null, out port, out error);
        }
        else
        {
            error = ParsePortError.UnknownPortFormat;
            port = null;
            return false;
        }
    }

    /// <summary>
    /// Ensures the given registry is valid.
    /// </summary>
    /// <param name="registryName"></param>
    /// <returns></returns>
    internal static bool IsValidRegistry(string registryName) => ReferenceParser.AnchoredDomainRegexp.IsMatch(registryName);

    /// <summary>
    /// Ensures the given image name is valid.
    /// Spec: https://github.com/opencontainers/distribution-spec/blob/4ab4752c3b86a926d7e5da84de64cbbdcc18d313/spec.md#pulling-manifests
    /// </summary>
    /// <param name="imageName"></param>
    /// <returns></returns>
    internal static bool IsValidImageName(string imageName)
    {
        return ReferenceParser.anchoredNameRegexp.IsMatch(imageName);
    }

    /// <summary>
    /// Ensures the given tag is valid.
    /// Spec: https://github.com/opencontainers/distribution-spec/blob/4ab4752c3b86a926d7e5da84de64cbbdcc18d313/spec.md#pulling-manifests
    /// </summary>
    /// <param name="imageTag"></param>
    /// <returns></returns>
    internal static bool IsValidImageTag(string imageTag)
    {
        return ReferenceParser.anchoredTagRegexp.IsMatch(imageTag);
    }

    /// <summary>
    /// Given an already-validated registry domain, this is our hueristic to determine what HTTP protocol should be used to interact with it.
    /// This is primarily for testing - in the real world almost all usage should be through HTTPS!
    /// </summary>
    internal static Uri TryExpandRegistryToUri(string alreadyValidatedDomain)
    {
        var prefix = alreadyValidatedDomain.StartsWith("localhost", StringComparison.Ordinal) ? "http" : "https";
        return new Uri($"{prefix}://{alreadyValidatedDomain}");
    }

    /// <summary>
    /// Ensures a given environment variable is valid.
    /// </summary>
    /// <param name="envVar"></param>
    /// <returns></returns>
    internal static bool IsValidEnvironmentVariable(string envVar)
    {
        return envVarRegex.IsMatch(envVar);
    }

    /// <summary>
    /// Parse a fully qualified container name (e.g. https://mcr.microsoft.com/dotnet/runtime:6.0)
    /// Note: Tag not required.
    /// </summary>
    /// <remarks>
    /// This code is adapted from <see href="https://github.com/distribution/distribution/blob/78b9c98c5c31c30d74f9acb7d96f98552f2cf78f/reference/reference.go#L191-L236">reference.go</see>
    /// and so may not match .NET idioms.
    /// NOTE: We explicitly are not handling digest references at the moment.
    /// </remarks>
    /// <param name="fullyQualifiedContainerName"></param>
    /// <param name="containerRegistry"></param>
    /// <param name="containerName"></param>
    /// <param name="containerTag"></param>
    /// <param name="containerDigest"></param>
    /// <returns>True if the parse was successful. When false is returned, all out vars are set to empty strings.</returns>
    internal static bool TryParseFullyQualifiedContainerName(string fullyQualifiedContainerName,
                                                            [NotNullWhen(true)] out string? containerRegistry,
                                                            [NotNullWhen(true)] out string? containerName,
                                                            out string? containerTag, // tag is always optional - we can't guarantee anything here
                                                            out string? containerDigest, // digest is always optional - we can't guarantee anything here
                                                            out bool isRegistrySpecified
                                                            )
    {

        /// if we don't have a reference at all, bail out
        var referenceMatch = ReferenceParser.ReferenceRegexp.Match(fullyQualifiedContainerName);
        if (referenceMatch is not { Success: true })
        {
            containerRegistry = null;
            containerName = null;
            containerTag = null;
            containerDigest = null;
            isRegistrySpecified = false;
            return false;
        }

        // if we have a reference, then we have three groups:
        // * reference name
        // * reference tag (optional)
        // * reference digest (optional)

        // this will always be successful if the ReferenceRegexp matched, so it's safe to index into.
        var namePortion = referenceMatch.Groups[1].Value;
        // we try to decompose the reference name into registry and image name parts.
        var nameMatch = ReferenceParser.anchoredNameRegexp.Match(namePortion);
        if (nameMatch is { Success: true })
        {
            // the name regex has two groups:
            // * registry (optional)
            // * image name

            // safely discover the registry
            var registryPortion = nameMatch.Groups[1];
            isRegistrySpecified = registryPortion.Success;
            containerRegistry = isRegistrySpecified ? registryPortion.Value
                                                    : DockerRegistryAlias;

            // direct access to the name portion is safe because the regex matched
            var imageNamePortion = nameMatch.Groups[2];
            containerName = imageNamePortion.Value;

            if (containerRegistry == DockerRegistryAlias)
            {
                // Add the 'library/' prefix to expand short names like 'ubuntu' to 'library/ubuntu'.
                if (!containerName.Contains("/"))
                {
                    containerName = $"library/{containerName}";
                }
            }
        }
        else
        {
            containerRegistry = null;
            containerName = null;
            containerTag = null;
            containerDigest = null;
            isRegistrySpecified = false;
            return false;
        }

        // tag may not exist in the reference, so we must safely access it
        var tagPortion = referenceMatch.Groups[2];
        containerTag = tagPortion.Success ? tagPortion.Value : null;

        // digest may not exist in the reference, so we must safely access it
        var digestPortion = referenceMatch.Groups[3];
        containerDigest = digestPortion.Success ? digestPortion.Value : null;

        return true;
    }

    /// <summary>
    /// Checks if a given container image name adheres to the image name spec. If not, and recoverable, then normalizes invalid characters.
    /// </summary>
    internal static bool NormalizeRepository(string containerRepository,
                                         [NotNullWhen(false)] out string? normalizedImageName)
    {
        if (IsValidImageName(containerRepository))
        {
            normalizedImageName = null;
            return true;
        }
        else
        {
            if (!Char.IsLetterOrDigit(containerRepository, 0))
            {
                throw new ArgumentException(Resources.Resource.GetString(nameof(Strings.InvalidImageName)));
            }
            var loweredImageName = containerRepository.ToLowerInvariant();
            normalizedImageName = imageNameCharacters.Replace(loweredImageName, "-");
            return false;
        }
    }
}

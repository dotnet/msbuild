// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System;
using System.Linq;
#endif
#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;
public static class ContainerHelpers
{
    internal const string HostObjectUser = "SDK_CONTAINER_REGISTRY_UNAME";

    internal const string HostObjectPass = "SDK_CONTAINER_REGISTRY_PWORD";

    internal const string DockerRegistryAlias = "docker.io";

    /// <summary>
    /// Matches an environment variable name - must start with a letter or underscore, and can only contain letters, numbers, and underscores.
    /// </summary>
    private static Regex envVarRegex = new(@"^[a-zA-Z_]{1,}[a-zA-Z0-9_]*$");

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
        if (string.IsNullOrEmpty(portNumber))
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
    internal static (string? normalizedImageName, (string, object[])? normalizationWarning, (string, object[])? normalizationError) NormalizeRepository(string containerRepository)
    {
        if (IsValidImageName(containerRepository))
        {
            return (containerRepository, null, null);
        }
        else
        {
            // check for leading alphanumeric character
            char firstChar = containerRepository[0];
            if (!IsAlpha(firstChar) && !IsNumeric(firstChar))
            {
                // The name did not start with an alphanumeric character, so we can't normalize it.
                var error = (nameof(Strings.InvalidImageName_NonAlphanumericStartCharacter), new[] { containerRepository });
                return (null, null, error);
            }


            // normalize the name. a little more complex, but this does all of our checks in a single pass and doesn't require coming back
            // after the normalization to check if our invariants hold
            var normalizedAllChars = true;
            var normalizationOccurred = false;
            var builder = new StringBuilder(containerRepository);
            for (int i = 0; i < containerRepository.Length; i++)
            {
                var current = containerRepository[i];
                if (IsLowerAlpha(current) || IsNumeric(current) || IsAllowedPunctuation(current))
                {
                    // no need to set the builder's char here, since we preloaded
                    normalizedAllChars = false;
                }
                else if (IsUpperAlpha(current))
                {
                    builder[i] = char.ToLowerInvariant(current);
                    normalizationOccurred = true;
                }
                else
                {
                    builder[i] = '-';
                    normalizationOccurred = true;
                }
            }
            var normalizedImageName = builder.ToString();

            // check for normalization to useless name
            if (normalizedAllChars)
            {
                // The name was normalized to all dashes, so there was nothing recoverable. We should throw.
                var error = (nameof(Strings.InvalidImageName_EntireNameIsInvalidCharacters), new string[] { containerRepository });
                return (null, null, error);
            }

            // check for warning/notification that we did indeed perform normalization
            if (normalizationOccurred)
            {
                var warning = (nameof(Strings.NormalizedContainerName), new string[] { containerRepository, normalizedImageName });
                return (normalizedImageName, warning, null);
            }

            // user value was already normalized, so we don't need to do anything
            else
            {
                return (containerRepository, null, null);
            }
        }

        static bool IsUpperAlpha(char c) => c >= 'A' && c <= 'Z';
        static bool IsLowerAlpha(char c) => c >= 'a' && c <= 'z';
        static bool IsAlpha(char c) => IsLowerAlpha(c) || IsUpperAlpha(c);
        static bool IsNumeric(char c) => c >= '0' && c <= '9';
        static bool IsAllowedPunctuation(char c) => (c == '_') || (c == '-') || (c == '/');
    }
}

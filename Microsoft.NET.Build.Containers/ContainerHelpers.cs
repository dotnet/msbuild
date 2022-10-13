namespace Microsoft.NET.Build.Containers;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using static ReferenceParser;

record Label(string name, string value);

// Explicitly lowercase to ease parsing - the incoming values are
// lowercased by spec
public enum PortType
{
    tcp,
    udp
}

public record Port(int number, PortType type);

public static class ContainerHelpers
{

    /// <summary>
    /// DefaultRegistry is the canonical representation of something that lives in the local docker daemon. It's used as the inferred registry for repositories
    /// that have no registry component.
    /// See <see href="https://github.com/distribution/distribution/blob/78b9c98c5c31c30d74f9acb7d96f98552f2cf78f/reference/normalize.go">normalize.go</see>.
    /// </summary>
    public const string DefaultRegistry = "docker.io";

    /// <summary>
    /// Matches if the string is not lowercase or numeric, or ., _, or -.
    /// </summary>
    /// <remarks>Technically the period should be allowed as well, but due to inconsistent support between cloud providers we're removing it.</remarks>
    private static Regex imageNameCharacters = new Regex(@"[^a-z0-9_\-/]");

    /// <summary>
    /// Ensures the given registry is valid.
    /// </summary>
    /// <param name="registryName"></param>
    /// <returns></returns>
    public static bool IsValidRegistry(string registryName) => AnchoredDomainRegexp.IsMatch(registryName);

    /// <summary>
    /// Ensures the given image name is valid.
    /// Spec: https://github.com/opencontainers/distribution-spec/blob/4ab4752c3b86a926d7e5da84de64cbbdcc18d313/spec.md#pulling-manifests
    /// </summary>
    /// <param name="imageName"></param>
    /// <returns></returns>
    public static bool IsValidImageName(string imageName)
    {
        return anchoredNameRegexp.IsMatch(imageName);
    }

    /// <summary>
    /// Ensures the given tag is valid.
    /// Spec: https://github.com/opencontainers/distribution-spec/blob/4ab4752c3b86a926d7e5da84de64cbbdcc18d313/spec.md#pulling-manifests
    /// </summary>
    /// <param name="imageTag"></param>
    /// <returns></returns>
    public static bool IsValidImageTag(string imageTag)
    {
        return anchoredTagRegexp.IsMatch(imageTag);
    }

    /// <summary>
    /// Given an already-validated registry domain, this is our hueristic to determine what HTTP protocol should be used to interact with it.
    /// This is primarily for testing - in the real world almost all usage should be through HTTPS!
    /// </summary>
    public static Uri TryExpandRegistryToUri(string alreadyValidatedDomain)
    {
        var prefix = alreadyValidatedDomain.StartsWith("localhost") ? "http" : "https";
        return new Uri($"{prefix}://{alreadyValidatedDomain}");
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
    public static bool TryParseFullyQualifiedContainerName(string fullyQualifiedContainerName,
                                                            [NotNullWhen(true)] out string? containerRegistry,
                                                            [NotNullWhen(true)] out string? containerName,
                                                            out string? containerTag, // tag is always optional - we can't guarantee anything here
                                                            out string? containerDigest // digest is always optional - we can't guarantee anything here
                                                            )
    {

        /// if we don't have a reference at all, bail out
        var referenceMatch = ReferenceRegexp.Match(fullyQualifiedContainerName);
        if (referenceMatch is not { Success: true })
        {
            containerRegistry = null;
            containerName = null;
            containerTag = null;
            containerDigest = null;
            return false;
        }

        // if we have a reference, then we have three groups:
        // * reference name
        // * reference tag (optional)
        // * reference digest (optional)

        // this will always be successful if the ReferenceRegexp matched, so it's safe to index into.
        var namePortion = referenceMatch.Groups[1].Value;
        // we try to decompose the reference name into registry and image name parts.
        var nameMatch = anchoredNameRegexp.Match(namePortion);
        if (nameMatch is { Success: true })
        {
            // the name regex has two groups:
            // * registry (optional)
            // * image name

            // safely discover the registry
            var registryPortion = nameMatch.Groups[1];
            if (registryPortion.Success)
            {
                containerRegistry = registryPortion.Value;
            }
            else
            {
                // intent of this is that if we have a 'bare' image name (like library/ruby for example)
                // then DefaultRegistry is used as the registry.
                containerRegistry = DefaultRegistry;
            }

            // direct access to the name portion is safe because the regex matched
            var imageNamePortion = nameMatch.Groups[2];
            containerName = imageNamePortion.Value;
        }
        else
        {
            containerRegistry = null;
            containerName = null;
            containerTag = null;
            containerDigest = null;
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
    public static bool NormalizeImageName(string containerImageName,
                                         [NotNullWhen(false)] out string? normalizedImageName)
    {
        if (IsValidImageName(containerImageName))
        {
            normalizedImageName = null;
            return true;
        }
        else
        {
            if (!Char.IsLetterOrDigit(containerImageName, 0))
            {
                throw new ArgumentException("The first character of the image name must be a lowercase letter or a digit.");
            }
            var loweredImageName = containerImageName.ToLowerInvariant();
            normalizedImageName = imageNameCharacters.Replace(loweredImageName, "-");
            return false;
        }
    }

    [Flags]
    public enum ParsePortError
    {
        MissingPortNumber,
        InvalidPortNumber,
        InvalidPortType,
        UnknownPortFormat
    }

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
}

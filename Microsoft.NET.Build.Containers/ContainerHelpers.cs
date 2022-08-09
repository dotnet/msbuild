namespace Microsoft.NET.Build.Containers;

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

public static class ContainerHelpers
{
    private static Regex imageTagRegex = new Regex("^[a-zA-Z0-9_][a-zA-Z0-9._-]{0,127}$");

    private static Regex imageNameRegex = new Regex("^[a-z0-9]+([._-][a-z0-9]+)*(/[a-z0-9]+([._-][a-z0-9]+)*)*$");

    /// <summary>
    /// Given some "fully qualified" image name (e.g. mcr.microsoft.com/dotnet/runtime), return
    /// a valid UriBuilder. This means appending 'https' if the URI is not absolute, otherwise UriBuilder will throw.
    /// </summary>
    /// <param name="containerBase"></param>
    /// <returns>A <see cref="Uri" /> with the given containerBase, or, if containerBase is relative, https:// + containerBase</returns>
    private static Uri? ContainerImageToUri(string containerBase)
    {
        Uri uri = new Uri(containerBase, UriKind.RelativeOrAbsolute);

        try
        {
            return uri.IsAbsoluteUri ? uri : new Uri(containerBase.Contains("localhost") ? "http://" : "https://" + uri);
        }
        catch (Exception e)
        {
            Console.WriteLine("Failed parsing the container image into a UriBuilder: {0}", e);
            return null;
        }
    }

    /// <summary>
    /// Ensures the given registry is valid.
    /// </summary>
    /// <param name="imageName"></param>
    /// <returns></returns>
    public static bool IsValidRegistry(string registryName)
    {
        // No scheme prefixed onto the registry
        if (string.IsNullOrEmpty(registryName) ||
            (!registryName.StartsWith("http://") && 
             !registryName.StartsWith("https://") && 
             !registryName.StartsWith("docker://")))
        {
            return false;
        }

        try
        {
            UriBuilder uri = new UriBuilder(registryName);
        }
        catch
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Ensures the given image name is valid.
    /// Spec: https://github.com/opencontainers/distribution-spec/blob/4ab4752c3b86a926d7e5da84de64cbbdcc18d313/spec.md#pulling-manifests
    /// </summary>
    /// <param name="imageName"></param>
    /// <returns></returns>
    public static bool IsValidImageName(string imageName)
    {
        return imageNameRegex.IsMatch(imageName);
    }

    /// <summary>
    /// Ensures the given tag is valid.
    /// Spec: https://github.com/opencontainers/distribution-spec/blob/4ab4752c3b86a926d7e5da84de64cbbdcc18d313/spec.md#pulling-manifests
    /// </summary>
    /// <param name="imageTag"></param>
    /// <returns></returns>
    public static bool IsValidImageTag(string imageTag)
    {
        return imageTagRegex.IsMatch(imageTag);
    }

    /// <summary>
    /// Parse a fully qualified container name (e.g. https://mcr.microsoft.com/dotnet/runtime:6.0)
    /// Note: Tag not required.
    /// </summary>
    /// <param name="fullyQualifiedContainerName"></param>
    /// <param name="containerRegistry"></param>
    /// <param name="containerName"></param>
    /// <param name="containerTag"></param>
    /// <returns>True if the parse was successful. When false is returned, all out vars are set to empty strings.</returns>
    public static bool TryParseFullyQualifiedContainerName(string fullyQualifiedContainerName, 
                                                            [NotNullWhen(true)] out string? containerRegistry, 
                                                            [NotNullWhen(true)] out string? containerName, 
                                                            [NotNullWhen(true)] out string? containerTag)
    {
        Uri? uri = ContainerImageToUri(fullyQualifiedContainerName);

        if (uri == null || uri.Segments.Length <= 1)
        {
            containerRegistry = null;
            containerName = null;
            containerTag = null;
            return false;
        }

        // The first segment is the '/', create a string out of everything after.
        string image = uri.PathAndQuery.Substring(1);

        // If the image has a ':', there's a tag we need to parse.
        int indexOfColon = image.IndexOf(':');

        containerRegistry = uri.Scheme + "://" + uri.Host + (uri.Port > 0 && !uri.IsDefaultPort ? ":" + uri.Port : "");
        containerName = indexOfColon == -1 ? image : image.Substring(0, indexOfColon);
        containerTag = indexOfColon == -1 ? "" : image.Substring(indexOfColon + 1);
        return true;
    }
}

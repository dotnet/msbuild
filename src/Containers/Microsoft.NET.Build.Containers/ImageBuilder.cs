// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// The class builds new image based on the base image.
/// </summary>
internal sealed class ImageBuilder
{
    private readonly ManifestV2 _manifest;
    private readonly ImageConfig _baseImageConfig;

    public ImageConfig BaseImageConfig => _baseImageConfig;

    internal ImageBuilder(ManifestV2 manifest, ImageConfig baseImageConfig)
    {
        _manifest = manifest;
        _baseImageConfig = baseImageConfig;
    }

    /// <summary>
    /// Gets a value indicating whether the base image is has a Windows operating system.
    /// </summary>
    public bool IsWindows => _baseImageConfig.IsWindows;

    /// <summary>
    /// Builds the image configuration <see cref="BuiltImage"/> ready for further processing.
    /// </summary>
    internal BuiltImage Build()
    {
        string imageJsonStr = _baseImageConfig.BuildConfig();
        string imageSha = DigestUtils.GetSha(imageJsonStr);
        string imageDigest = DigestUtils.GetDigestFromSha(imageSha);
        long imageSize = Encoding.UTF8.GetBytes(imageJsonStr).Length;

        ManifestConfig newManifestConfig = _manifest.Config with
        {
            digest = imageDigest,
            size = imageSize
        };

        ManifestV2 newManifest = _manifest with
        {
            Config = newManifestConfig
        };

        return new BuiltImage()
        {
            Config = imageJsonStr,
            ImageDigest = imageDigest,
            ImageSha = imageSha,
            ImageSize = imageSize,
            Manifest = newManifest,
        };
    }

    /// <summary>
    /// Adds a <see cref="Layer"/> to a base image.
    /// </summary>
    internal void AddLayer(Layer l)
    {
        _manifest.Layers.Add(new(l.Descriptor.MediaType, l.Descriptor.Size, l.Descriptor.Digest, l.Descriptor.Urls));
        _baseImageConfig.AddLayer(l);
    }

    /// <summary>
    /// Adds a label to a base image.
    /// </summary>
    internal void AddLabel(string name, string value) => _baseImageConfig.AddLabel(name, value);

    /// <summary>
    /// Adds environment variables to a base image.
    /// </summary>
    internal void AddEnvironmentVariable(string envVarName, string value) => _baseImageConfig.AddEnvironmentVariable(envVarName, value);

    /// <summary>
    /// Exposes additional port.
    /// </summary>
    internal void ExposePort(int number, PortType type) => _baseImageConfig.ExposePort(number, type);

    /// <summary>
    /// Sets working directory for the image.
    /// </summary>
    internal void SetWorkingDirectory(string workingDirectory) => _baseImageConfig.SetWorkingDirectory(workingDirectory);

    /// <summary>
    /// Sets the ENTRYPOINT and CMD for the image.
    /// </summary>
    internal void SetEntrypointAndCmd(string[] entrypoint, string[] cmd) => _baseImageConfig.SetEntrypointAndCmd(entrypoint, cmd);

    /// <summary>
    /// Sets the USER for the image.
    /// </summary>
    internal void SetUser(string user) => _baseImageConfig.SetUser(user);
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MSBuild.Benchmarks;

/// <summary>
///  Owns a unique temporary directory used by a benchmark.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    /// <summary>
    ///  Initializes a new temporary directory for the specified benchmark.
    /// </summary>
    /// <param name="name">The name used to group the directory beneath the benchmark temporary root.</param>
    public TemporaryDirectory(string name)
    {
        DirectoryPath = Path.Combine(
            Path.GetTempPath(),
            "MSBuild.Benchmarks",
            name,
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(DirectoryPath);
    }

    /// <summary>
    ///  Gets the full path of the temporary directory.
    /// </summary>
    public string DirectoryPath { get; }

    /// <summary>
    ///  Gets a path relative to the temporary directory.
    /// </summary>
    /// <param name="relativePath">The relative path to append.</param>
    /// <returns>
    ///  The combined full path.
    /// </returns>
    public string GetPath(string relativePath)
        => Path.Combine(DirectoryPath, relativePath);

    /// <summary>
    ///  Creates a directory relative to the temporary directory.
    /// </summary>
    /// <param name="relativePath">The relative directory path to create.</param>
    /// <returns>
    ///  The full path of the created directory.
    /// </returns>
    public string CreateDirectory(string relativePath)
    {
        string directoryPath = GetPath(relativePath);
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    /// <summary>
    ///  Writes a file relative to the temporary directory, creating its containing directory when necessary.
    /// </summary>
    /// <param name="relativePath">The relative file path to write.</param>
    /// <param name="contents">The file contents.</param>
    /// <returns>
    ///  The full path of the written file.
    /// </returns>
    public string WriteFile(string relativePath, string contents)
    {
        string filePath = GetPath(relativePath);
        string? directoryPath = Path.GetDirectoryName(filePath);

        if (directoryPath is not null)
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.WriteAllText(filePath, contents);
        return filePath;
    }

    /// <summary>
    ///  Deletes the temporary directory and its contents.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}

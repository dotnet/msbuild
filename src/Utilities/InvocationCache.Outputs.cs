// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Build.Framework;

#pragma warning disable MSBUILD001

namespace Microsoft.Build.Utilities;

public sealed partial class InvocationCache
{
    internal void ValidateOutputs(InvocationOutputs outputs, InvocationInputs inputs, string workingDirectory)
    {
        ValidateOutputs(outputs);

        if (outputs.DirectoryPath is null)
        {
            return;
        }

        StringComparison comparison = FileUtilities.PathComparison;
        string outputPrefix = outputs.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            string resolvedWorkingDirectory = ResolveWorkingDirectory(workingDirectory).Value;
            string workingDirPrefix = resolvedWorkingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (workingDirPrefix.StartsWith(outputPrefix, comparison))
            {
                throw new ArgumentException("The output directory must not contain the working directory.", nameof(outputs));
            }
        }

        foreach (string input in inputs.Files)
        {
            string inputPrefix = input.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (inputPrefix.StartsWith(outputPrefix, comparison))
            {
                throw new ArgumentException("The output directory must not contain declared inputs.", nameof(outputs));
            }
        }
    }

    internal void ValidateOutputs(InvocationOutputs outputs)
    {
        StringComparison comparison = FileUtilities.PathComparison;
        string cachePrefix = CacheDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (outputs.DirectoryPath is not null)
        {
            string outputPrefix = outputs.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (cachePrefix.StartsWith(outputPrefix, comparison) || outputPrefix.StartsWith(cachePrefix, comparison))
            {
                throw new ArgumentException("The cache and output directories must not contain each other.", nameof(outputs));
            }
        }
        else if (outputs.Files.Length > 0)
        {
            StringComparer comparer = FileUtilities.PathComparer;
            string[] prefixes = new string[outputs.Files.Length];
            for (int i = 0; i < outputs.Files.Length; i++)
            {
                string file = outputs.Files[i];
                string filePrefix = file.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (filePrefix.StartsWith(cachePrefix, comparison) || cachePrefix.StartsWith(filePrefix, comparison))
                {
                    throw new ArgumentException("Cache outputs must be distinct files outside the cache.", nameof(outputs));
                }

                prefixes[i] = filePrefix;
            }

            Array.Sort(prefixes, comparer);
            for (int i = 1; i < prefixes.Length; i++)
            {
                if (prefixes[i].Equals(prefixes[i - 1], comparison))
                {
                    throw new ArgumentException("Cache outputs must be distinct files outside the cache.", nameof(outputs));
                }

                if (prefixes[i].StartsWith(prefixes[i - 1], comparison))
                {
                    throw new ArgumentException("Declared output files must not contain each other.", nameof(outputs));
                }
            }
        }
    }

    private static List<string> GetFileNames(InvocationOutputs outputs)
    {
        var files = new List<string>();
        if (outputs.DirectoryPath is not null)
        {
            Collect(outputs.DirectoryPath, "");
            files.Sort(StringComparer.Ordinal);
        }
        else
        {
            for (int i = 0; i < outputs.Files.Length; i++)
            {
                files.Add(i.ToString(CultureInfo.InvariantCulture));
            }
        }

        return files;

        void Collect(string directory, string relativeDirectory)
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                string relativePath = Path.Combine(relativeDirectory, Path.GetFileName(path));
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new IOException($"Cannot cache output directory containing symbolic link or reparse point: '{path}'.");
                    }

                    Collect(path, relativePath);
                }
                else
                {
                    files.Add(relativePath);
                }
            }
        }
    }

    private static string GetOutputPath(InvocationOutputs outputs, string name, int index) =>
        outputs.DirectoryPath is not null ? Path.Combine(outputs.DirectoryPath, name) : outputs.Files[index];

    private static void DeleteOutputDirectory(string directory)
    {
        foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            ClearReadOnlyAttribute(file);
        }

        Directory.Delete(directory, recursive: true);
    }

    private static void ClearReadOnlyAttribute(string path)
    {
        try
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // Nothing to clear when the target path does not exist yet.
        }
    }

    private static void DeleteOutputFile(string path)
    {
        try
        {
            ClearReadOnlyAttribute(path);
            File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
            // A missing parent already satisfies the declared file's absence.
        }
    }
}

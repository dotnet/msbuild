// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Framework;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class ItemSpecModifiersBenchmark
{
    private string _currentDirectory = null!;
    private string _itemSpec = null!;
    private string _definingProjectEscaped = null!;
    private string _recursiveDirModifier = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _currentDirectory = Path.GetTempPath();
        _itemSpec = Path.Combine(_currentDirectory, "src", "Framework", "ItemSpecModifiers.cs");
        _definingProjectEscaped = Path.Combine(_currentDirectory, "src", "Framework", "Microsoft.Build.Framework.csproj");

        // Ensure the file exists so time-based modifiers can resolve.
        Directory.CreateDirectory(Path.GetDirectoryName(_itemSpec)!);
        if (!File.Exists(_itemSpec))
        {
            File.WriteAllText(_itemSpec, string.Empty);
        }

        _recursiveDirModifier = ItemSpecModifiers.RecursiveDir;
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (File.Exists(_itemSpec))
        {
            File.Delete(_itemSpec);
        }
    }

    // -----------------------------------------------------------------------
    // FrozenSet lookup – covers IsItemSpecModifier for all known modifiers
    // plus a miss.
    // -----------------------------------------------------------------------

    [Benchmark]
    public int IsItemSpecModifier_AllModifiers()
    {
        int count = 0;
        foreach (string modifier in ItemSpecModifiers.All)
        {
            if (ItemSpecModifiers.IsItemSpecModifier(modifier))
            {
                count++;
            }
        }

        // Also check a miss.
        if (ItemSpecModifiers.IsItemSpecModifier("SomeCustomMetadata"))
        {
            count++;
        }

        return count;
    }

    // -----------------------------------------------------------------------
    // IsDerivableItemSpecModifier – RecursiveDir is the only modifier that
    // hits the length+char guard returning false.
    // -----------------------------------------------------------------------

    [Benchmark]
    public bool IsDerivableItemSpecModifier_RecursiveDir()
        => ItemSpecModifiers.IsDerivableItemSpecModifier(_recursiveDirModifier);

    // -----------------------------------------------------------------------
    // GetItemSpecModifier – FullPath is the most commonly used modifier and
    // the baseline for the caching benchmark below.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string GetItemSpecModifier_FullPath()
        => ItemSpecModifiers.GetItemSpecModifier(_itemSpec, ItemSpecModifiers.FullPath, _currentDirectory, _definingProjectEscaped);

    // -----------------------------------------------------------------------
    // GetItemSpecModifier – Directory has the most complex logic: resolves
    // FullPath internally, strips the root, and differs by OS.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string GetItemSpecModifier_Directory()
        => ItemSpecModifiers.GetItemSpecModifier(_itemSpec, ItemSpecModifiers.Directory, _currentDirectory, _definingProjectEscaped);

    // -----------------------------------------------------------------------
    // GetItemSpecModifier – file-time modifier (I/O-bound). All three time
    // modifiers share the same code shape; ModifiedTime uses
    // FileUtilities.GetFileInfoNoThrow which is the most common path.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string GetItemSpecModifier_ModifiedTime()
        => ItemSpecModifiers.GetItemSpecModifier(_itemSpec, ItemSpecModifiers.ModifiedTime, _currentDirectory, _definingProjectEscaped);

    // -----------------------------------------------------------------------
    // GetItemSpecModifier – DefiningProjectDirectory is the most expensive
    // defining-project modifier: it recursively resolves both RootDir and
    // Directory on the defining project path.
    // -----------------------------------------------------------------------

    [Benchmark]
    public string GetItemSpecModifier_DefiningProjectDirectory()
        => ItemSpecModifiers.GetItemSpecModifier(_itemSpec, ItemSpecModifiers.DefiningProjectDirectory, _currentDirectory, _definingProjectEscaped);
}

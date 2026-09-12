// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json.Linq;

namespace MSBuild.Bootstrap.Utils.Tasks;

/// <summary>Packages the private cache runtime without exposing its restore feeds.</summary>
public sealed class GetTaskCachePackageContents : Task
{
    /// <summary>The restored engine assets file.</summary>
    [Required]
    public string AssetsFile { get; set; } = string.Empty;

    /// <summary>The package target framework alias.</summary>
    [Required]
    public string TargetFramework { get; set; } = string.Empty;

    /// <summary>The framework moniker used by NuGet's central transitive groups.</summary>
    [Required]
    public string FrameworkMoniker { get; set; } = string.Empty;

    /// <summary>The SDK runtime graph, used for managed RID asset fallbacks.</summary>
    public string? RuntimeGraphFile { get; set; }

    /// <summary>The optional implementation assembly included in every managed runtime group.</summary>
    public string? RuntimeAssembly { get; set; }

    /// <summary>Private cache package roots.</summary>
    [Required]
    public string[] CacheRoots { get; set; } = [];

    /// <summary>Files and their explicit package destinations.</summary>
    [Output]
    public ITaskItem[] PackageFiles { get; private set; } = [];

    /// <inheritdoc />
    public override bool Execute()
    {
        JObject assets = JObject.Parse(File.ReadAllText(AssetsFile));
        JObject target = (JObject?)assets["targets"]?[TargetFramework]
            ?? throw new InvalidDataException("The assets file has no target for " + TargetFramework);
        Dictionary<string, JProperty> entries = new(StringComparer.OrdinalIgnoreCase);
        foreach (JProperty entry in target.Properties())
        {
            entries.Add(entry.Name.Substring(0, entry.Name.LastIndexOf('/')), entry);
        }

        HashSet<string> roots = new(CacheRoots, StringComparer.OrdinalIgnoreCase);
        HashSet<string> publicRoots = new(StringComparer.OrdinalIgnoreCase);
        JObject dependencies = (JObject)assets["project"]!["frameworks"]![TargetFramework]!["dependencies"]!;
        foreach (JProperty dependency in dependencies.Properties())
        {
            if (!roots.Contains(dependency.Name)
                && !string.Equals((string?)dependency.Value["suppressParent"], "All", StringComparison.OrdinalIgnoreCase))
            {
                publicRoots.Add(dependency.Name);
            }
        }
        foreach (var entry in entries)
        {
            if ((string?)entry.Value.Value["type"] == "project")
            {
                publicRoots.Add(entry.Key);
            }
        }
        if (assets["centralTransitiveDependencyGroups"]?[FrameworkMoniker] is JObject central)
        {
            foreach (JProperty dependency in central.Properties())
            {
                if (!roots.Contains(dependency.Name))
                {
                    publicRoots.Add(dependency.Name);
                }
            }
        }

        HashSet<string> bundled = Closure(roots, entries);
        bundled.ExceptWith(Closure(publicRoots, entries));
        Dictionary<string, string> common = new(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(RuntimeAssembly))
        {
            common.Add(Path.GetFileName(RuntimeAssembly!), RuntimeAssembly!);
        }
        Dictionary<string, Dictionary<string, string>> overrides = new(StringComparer.OrdinalIgnoreCase);
        List<ITaskItem> output = [];
        foreach (string name in bundled)
        {
            JProperty entry = entries[name];
            if ((string?)entry.Value["type"] != "package")
            {
                continue;
            }
            JObject library = (JObject)assets["libraries"]![entry.Name]!;
            string packageDirectory = FindPackageDirectory(assets, (string)library["path"]!);
            AddAssets(entry.Value["runtime"] as JObject, packageDirectory, common, resources: false);
            AddAssets(entry.Value["resource"] as JObject, packageDirectory, common, resources: true);
            if (entry.Value["runtimeTargets"] is JObject runtimeTargets)
            {
                foreach (JProperty asset in runtimeTargets.Properties())
                {
                    if ((string?)asset.Value["assetType"] == "native")
                    {
                        if (((string?)asset.Value["rid"])?.EndsWith("-x64", StringComparison.OrdinalIgnoreCase) == true
                            && !asset.Name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                        {
                            output.Add(NativePackageFile(Path.Combine(packageDirectory, asset.Name), asset.Name));
                        }
                        continue;
                    }
                    if ((string?)asset.Value["assetType"] != "runtime" || !asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string rid = (string)asset.Value["rid"]!;
                    if (!overrides.TryGetValue(rid, out Dictionary<string, string>? files))
                    {
                        files = new(StringComparer.OrdinalIgnoreCase);
                        overrides.Add(rid, files);
                    }
                    AddFile(files, Path.GetFileName(asset.Name), Path.Combine(packageDirectory, asset.Name));
                }
            }
            foreach (string? file in library["files"]!.Values<string>())
            {
                if (file is null)
                {
                    throw new InvalidDataException("Invalid restored package file entry.");
                }
                string filename = Path.GetFileName(file);
                if (name.Equals("RocksDbNative", StringComparison.OrdinalIgnoreCase)
                    && file.StartsWith("build/native/amd64/", StringComparison.OrdinalIgnoreCase))
                {
                    string rid = filename switch
                    {
                        "rocksdb.dll" => "win-x64",
                        "librocksdb.so" => "linux-x64",
                        "librocksdb.dylib" => "osx-x64",
                        _ => throw new InvalidDataException("Unexpected RocksDB native asset: " + file),
                    };
                    output.Add(NativePackageFile(Path.Combine(packageDirectory, file), "runtimes/" + rid + "/native/" + filename));
                }
                if (filename.StartsWith("license", StringComparison.OrdinalIgnoreCase)
                    || filename.StartsWith("notice", StringComparison.OrdinalIgnoreCase)
                    || filename.StartsWith("third-party-notices", StringComparison.OrdinalIgnoreCase)
                    || filename.StartsWith("thirdparty", StringComparison.OrdinalIgnoreCase)
                    || filename.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
                {
                    output.Add(PackageFile(Path.Combine(packageDirectory, file), "task-cache-notices/" + TargetFramework + "/" + entry.Name + "/" + file));
                }
            }
        }

        foreach (var file in common)
        {
            output.Add(PackageFile(file.Value, "lib/" + TargetFramework + "/" + file.Key));
        }
        JObject? runtimeGraph = string.IsNullOrEmpty(RuntimeGraphFile) ? null
            : (JObject?)JObject.Parse(File.ReadAllText(RuntimeGraphFile!))["runtimes"];
        foreach (string rid in overrides.Keys)
        {
            // A RID group replaces the package's neutral runtime group. Include all
            // neutral assemblies, not only the platform override.
            Dictionary<string, string> files = new(common, StringComparer.OrdinalIgnoreCase);
            ApplyOverrides(rid, runtimeGraph, overrides, files, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            foreach (var file in files)
            {
                output.Add(PackageFile(file.Value, "runtimes/" + rid + "/lib/" + TargetFramework + "/" + file.Key));
            }
        }
        PackageFiles = output.ToArray();
        return true;
    }

    private static HashSet<string> Closure(IEnumerable<string> roots, Dictionary<string, JProperty> entries)
    {
        HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
        Queue<string> pending = new(roots);
        while (pending.Count != 0)
        {
            string name = pending.Dequeue();
            if (!result.Add(name))
            {
                continue;
            }
            if (!entries.TryGetValue(name, out JProperty? entry))
            {
                throw new InvalidDataException("Missing restored dependency " + name);
            }
            if (entry.Value["dependencies"] is JObject dependencies)
            {
                foreach (JProperty dependency in dependencies.Properties())
                {
                    pending.Enqueue(dependency.Name);
                }
            }
        }
        return result;
    }

    private static void AddAssets(JObject? assets, string directory, Dictionary<string, string> files, bool resources)
    {
        if (assets is null)
        {
            return;
        }
        foreach (JProperty asset in assets.Properties())
        {
            if (asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                string destination = resources ? (string)asset.Value["locale"]! + "/" + Path.GetFileName(asset.Name) : Path.GetFileName(asset.Name);
                AddFile(files, destination, Path.Combine(directory, asset.Name));
            }
        }
    }

    private static void AddFile(Dictionary<string, string> files, string destination, string source)
    {
        if (files.TryGetValue(destination, out string? previous) && !string.Equals(previous, source, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Conflicting cache runtime assets: " + previous + " and " + source);
        }
        files[destination] = source;
    }

    private static void ApplyOverrides(string rid, JObject? graph, Dictionary<string, Dictionary<string, string>> overrides,
        Dictionary<string, string> files, HashSet<string> visited)
    {
        if (!visited.Add(rid))
        {
            return;
        }
        if (graph?[rid]?["#import"] is JArray imports)
        {
            for (int i = imports.Count - 1; i >= 0; i--)
            {
                ApplyOverrides((string)imports[i]!, graph, overrides, files, visited);
            }
        }
        if (overrides.TryGetValue(rid, out Dictionary<string, string>? replacements))
        {
            foreach (var file in replacements)
            {
                files[file.Key] = file.Value;
            }
        }
    }

    private static string FindPackageDirectory(JObject assets, string relative)
    {
        foreach (JProperty folder in ((JObject)assets["packageFolders"]!).Properties())
        {
            string path = Path.Combine(folder.Name, relative);
            if (Directory.Exists(path))
            {
                return path;
            }
        }
        throw new DirectoryNotFoundException("Restored package directory was not found: " + relative);
    }

    private static ITaskItem PackageFile(string source, string destination)
    {
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("Cache runtime package file was not found.", source);
        }
        TaskItem item = new(source);
        item.SetMetadata("PackagePath", destination);
        return item;
    }

    private static ITaskItem NativePackageFile(string source, string destination)
    {
        ITaskItem item = PackageFile(source, destination);
        item.SetMetadata("TaskCacheNativeAsset", "true");
        return item;
    }
}

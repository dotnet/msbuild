// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MSBuild.Bootstrap.Utils.Tasks;

/// <summary>
/// Adds the task-cache dependency closure to the acquired SDK without replacing
/// the SDK's existing NuGet and framework dependency versions.
/// </summary>
public sealed class AddBootstrapTaskCacheDependencies : Task
{
    private static readonly string[] s_assetKinds = ["runtime", "native", "resources", "runtimeTargets"];

    /// <summary>Gets or sets the freshly built CLI dependency manifest.</summary>
    [Required]
    public string SourceDepsFile { get; set; } = string.Empty;

    /// <summary>Gets or sets the acquired SDK's application dependency manifests.</summary>
    [Required]
    public string[] TargetDepsFiles { get; set; } = [];

    /// <inheritdoc />
    public override bool Execute()
    {
        JObject source = JObject.Parse(File.ReadAllText(SourceDepsFile));
        JObject sourceTarget = (JObject)source["targets"]![(string)source["runtimeTarget"]!["name"]!]!;
        Dictionary<string, JProperty> sourceEntries = new(StringComparer.OrdinalIgnoreCase);
        foreach (JProperty entry in sourceTarget.Properties())
        {
            sourceEntries.Add(PackageName(entry.Name), entry);
        }

        foreach (string targetFile in TargetDepsFiles)
        {
            Merge(source, sourceEntries, targetFile);
        }

        return true;
    }

    private void Merge(JObject source, Dictionary<string, JProperty> sourceEntries, string targetFile)
    {
        // Keep the acquired SDK baseline so changing package versions on subsequent
        // builds replaces our additions instead of treating them as SDK-owned.
        string baseline = targetFile + ".without-task-cache";
        if (!File.Exists(baseline))
        {
            File.Copy(targetFile, baseline);
        }

        JObject target = JObject.Parse(File.ReadAllText(baseline));
        JObject targetEntries = (JObject)target["targets"]![(string)target["runtimeTarget"]!["name"]!]!;
        Dictionary<string, string> versions = new(StringComparer.OrdinalIgnoreCase);
        foreach (JProperty entry in targetEntries.Properties())
        {
            versions.Add(PackageName(entry.Name), PackageVersion(entry.Name));
        }

        string[] roots = ["Microsoft.Build.TaskCache", "Microsoft.BuildXL.Cache.MemoizationStore.Library", "RocksDbNative", "protobuf-net.Grpc"];
        Queue<string> pending = new(roots);
        List<JProperty> additions = [];
        while (pending.Count > 0)
        {
            string name = pending.Dequeue();
            if (versions.ContainsKey(name))
            {
                continue;
            }

            JProperty entry = sourceEntries[name];
            additions.Add(entry);
            versions.Add(name, PackageVersion(entry.Name));
            if (entry.Value["dependencies"] is JObject dependencies)
            {
                foreach (JProperty dependency in dependencies.Properties())
                {
                    pending.Enqueue(dependency.Name);
                }
            }
        }

        foreach (JProperty entry in additions)
        {
            JObject value = (JObject)entry.Value.DeepClone();
            if (value["dependencies"] is JObject dependencies)
            {
                foreach (JProperty dependency in dependencies.Properties())
                {
                    dependency.Value = versions[dependency.Name];
                }
            }

            targetEntries.Add(entry.Name, value);
            target["libraries"]![entry.Name] = source["libraries"]![entry.Name]!.DeepClone();
            CopyAssets(value, Path.GetDirectoryName(targetFile)!);
        }

        foreach (JProperty entry in targetEntries.Properties())
        {
            if (PackageName(entry.Name).Equals("Microsoft.Build", StringComparison.OrdinalIgnoreCase))
            {
                JObject dependencies = (JObject)(entry.Value["dependencies"] ??= new JObject());
                foreach (string name in roots)
                {
                    dependencies[name] = versions[name];
                }
            }
        }

        string staging = targetFile + "." + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(staging, target.ToString(Formatting.Indented));
            File.Replace(staging, targetFile, null);
        }
        finally
        {
            File.Delete(staging);
        }
    }

    private void CopyAssets(JObject entry, string destination)
    {
        string sourceDirectory = Path.GetDirectoryName(SourceDepsFile)!;
        foreach (string kind in s_assetKinds)
        {
            if (entry[kind] is not JObject assets)
            {
                continue;
            }

            foreach (JProperty asset in assets.Properties())
            {
                string relative = kind == "runtimeTargets" ? asset.Name : Path.GetFileName(asset.Name);
                if (kind == "resources")
                {
                    relative = Path.Combine((string)asset.Value["locale"]!, relative);
                }

                relative = relative.Replace('/', Path.DirectorySeparatorChar);
                string path = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.Copy(Path.Combine(sourceDirectory, relative), path, overwrite: true);
            }
        }
    }

    private static string PackageName(string identity) => identity.Substring(0, identity.LastIndexOf('/'));

    private static string PackageVersion(string identity) => identity.Substring(identity.LastIndexOf('/') + 1);
}

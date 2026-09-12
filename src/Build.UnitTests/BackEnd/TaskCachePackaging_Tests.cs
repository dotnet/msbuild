// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_BUILDXL_TASK_CACHE
using System;
using System.IO;
using System.Linq;
using MSBuild.Bootstrap.Utils.Tasks;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class TaskCachePackaging_Tests(ITestOutputHelper output)
{
    [Fact]
    public void BundlesPrivateRuntimeClosureAndPreservesRuntimeFallbacks()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        GetTaskCachePackageContents task = CreateTask(env, out _);
        task.Execute().ShouldBeTrue();
        var files = task.PackageFiles.ToDictionary(item => item.GetMetadata("PackagePath"), item => item.ItemSpec);
        files.Keys.ShouldContain("lib/net11.0/Cache.Root.dll");
        files.Keys.ShouldContain("lib/net11.0/Cache.Leaf.dll");
        files.Keys.ShouldNotContain(path => path.Contains("Shared.Public") || path.Contains("Unrelated.Private"));
        files.Keys.ShouldContain("runtimes/linux-x64/native/libcache.so");
        files["runtimes/win/lib/net11.0/Cache.Leaf.dll"].Replace('\\', '/').ShouldEndWith("runtimes/win/lib/net11.0/Cache.Leaf.dll");
        files["runtimes/win/lib/net11.0/Cache.Root.dll"].Replace('\\', '/').ShouldEndWith("lib/net11.0/Cache.Root.dll");
        files["runtimes/win-x64/lib/net11.0/Cache.Leaf.dll"].Replace('\\', '/').ShouldEndWith("runtimes/win/lib/net11.0/Cache.Leaf.dll");
        files["runtimes/win-x64/lib/net11.0/Cache.Root.dll"].Replace('\\', '/').ShouldEndWith("runtimes/win-x64/lib/net11.0/Cache.Root.dll");
        files.Keys.ShouldContain("task-cache-notices/net11.0/Cache.Root/1.0.0/LICENSE.txt");
    }

    [Fact]
    public void RuntimeImplementationIsIncludedInEveryRuntimeGroup()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        GetTaskCachePackageContents task = CreateTask(env, out _);
        task.RuntimeAssembly = env.CreateFile("Microsoft.Build.TaskCache.dll", "runtime").Path;
        task.Execute().ShouldBeTrue();
        var paths = task.PackageFiles.Select(item => item.GetMetadata("PackagePath")).ToArray();
        paths.ShouldContain("lib/net11.0/Microsoft.Build.TaskCache.dll");
        paths.ShouldContain("runtimes/win/lib/net11.0/Microsoft.Build.TaskCache.dll");
        paths.ShouldContain("runtimes/win-x64/lib/net11.0/Microsoft.Build.TaskCache.dll");
    }

    [Fact]
    public void RocksDbBuildAssetsAreMappedToAllSupportedNativeRids()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        GetTaskCachePackageContents task = CreateTask(env, out JObject assets);
        assets["targets"]!["net11.0"]!["Cache.Root/1.0.0"]!["dependencies"]!["RocksDbNative"] = "1.0.0";
        assets["targets"]!["net11.0"]!["RocksDbNative/1.0.0"] = JObject.Parse("""{"type":"package"}""");
        string root = ((JObject)assets["packageFolders"]!).Properties().Single().Name;
        string[] filenames = ["rocksdb.dll", "librocksdb.so", "librocksdb.dylib"];
        JArray files = [];
        foreach (string filename in filenames)
        {
            string relative = "build/native/amd64/" + filename;
            string path = Path.Combine(root, "rocksdbnative/1.0.0", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "native fixture");
            files.Add(relative);
        }
        assets["libraries"]!["RocksDbNative/1.0.0"] = new JObject { ["path"] = "rocksdbnative/1.0.0", ["files"] = files };
        File.WriteAllText(task.AssetsFile, assets.ToString());
        task.Execute().ShouldBeTrue();
        var paths = task.PackageFiles.Select(item => item.GetMetadata("PackagePath")).ToArray();
        paths.ShouldContain("runtimes/win-x64/native/rocksdb.dll");
        paths.ShouldContain("runtimes/linux-x64/native/librocksdb.so");
        paths.ShouldContain("runtimes/osx-x64/native/librocksdb.dylib");
    }

    [Fact]
    public void MissingDependencyFailsRatherThanPublishingAnIncompleteClosure()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        GetTaskCachePackageContents task = CreateTask(env, out JObject assets);
        assets["targets"]!["net11.0"]!["Cache.Root/1.0.0"]!["dependencies"]!["Missing"] = "1.0.0";
        File.WriteAllText(task.AssetsFile, assets.ToString());
        Should.Throw<InvalidDataException>(() => task.Execute()).Message.ShouldContain("Missing");
    }

    [Fact]
    public void MissingManagedAssemblyFailsBeforePacking()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        GetTaskCachePackageContents task = CreateTask(env, out _);
        task.Execute().ShouldBeTrue();
        File.Delete(task.PackageFiles.Single(item => item.GetMetadata("PackagePath") == "lib/net11.0/Cache.Leaf.dll").ItemSpec);
        Should.Throw<FileNotFoundException>(() => task.Execute());
    }

    private static GetTaskCachePackageContents CreateTask(TestEnvironment env, out JObject assets)
    {
        string directory = env.CreateFolder().Path;
        assets = JObject.Parse("""
            {
              "targets": { "net11.0": {
                "Cache.Root/1.0.0": { "type":"package", "dependencies":{"Cache.Leaf":"1.0.0","Shared.Public":"1.0.0"},
                  "runtime":{"lib/net11.0/Cache.Root.dll":{}},
                  "runtimeTargets":{"runtimes/win-x64/lib/net11.0/Cache.Root.dll":{"rid":"win-x64","assetType":"runtime"}} },
                "Cache.Leaf/1.0.0": { "type":"package", "runtime":{"lib/net11.0/Cache.Leaf.dll":{}},
                  "runtimeTargets":{
                    "runtimes/win/lib/net11.0/Cache.Leaf.dll":{"rid":"win","assetType":"runtime"},
                    "runtimes/linux-x64/native/libcache.so":{"rid":"linux-x64","assetType":"native"}} },
                "Shared.Public/1.0.0": { "type":"package", "runtime":{"lib/net11.0/Shared.Public.dll":{}} },
                "Unrelated.Private/1.0.0": { "type":"package", "runtime":{"lib/net11.0/Unrelated.Private.dll":{}} }
              }},
              "project":{"frameworks":{"net11.0":{"dependencies":{
                "Cache.Root":{"suppressParent":"All"},"Shared.Public":{},"Unrelated.Private":{"suppressParent":"All"}
              }}}},
              "libraries":{
                "Cache.Root/1.0.0":{"path":"cache.root/1.0.0","files":["lib/net11.0/Cache.Root.dll","runtimes/win-x64/lib/net11.0/Cache.Root.dll","LICENSE.txt"]},
                "Cache.Leaf/1.0.0":{"path":"cache.leaf/1.0.0","files":["lib/net11.0/Cache.Leaf.dll","runtimes/win/lib/net11.0/Cache.Leaf.dll","runtimes/linux-x64/native/libcache.so"]},
                "Shared.Public/1.0.0":{"path":"shared.public/1.0.0","files":[]},
                "Unrelated.Private/1.0.0":{"path":"unrelated.private/1.0.0","files":[]}
              }
            }
            """);
        assets["packageFolders"] = new JObject { [directory] = new JObject() };
        foreach (JProperty library in ((JObject)assets["libraries"]!).Properties())
        {
            foreach (string? file in library.Value["files"]!.Values<string>())
            {
                string path = Path.Combine(directory, (string)library.Value["path"]!, file!);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "fixture");
            }
        }
        string assetsFile = Path.Combine(directory, "project.assets.json");
        File.WriteAllText(assetsFile, assets.ToString());
        string graph = Path.Combine(directory, "runtime.json");
        File.WriteAllText(graph, """{"runtimes":{"win-x64":{"#import":["win"]},"win":{"#import":["any"]},"any":{}}}""");
        return new GetTaskCachePackageContents
        {
            AssetsFile = assetsFile, TargetFramework = "net11.0", FrameworkMoniker = ".NETCoreApp,Version=v11.0",
            RuntimeGraphFile = graph, CacheRoots = ["Cache.Root"],
        };
    }
}
#endif

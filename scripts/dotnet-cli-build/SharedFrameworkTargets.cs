using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.Extensions.PlatformAbstractions;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public class SharedFrameworkTargets
    {
        private const string CoreHostBaseName = "corehost";

        [Target]
        public static BuildTargetResult PublishSharedHost(BuildTargetContext c)
        {
            string SharedHostPublishRoot = Path.Combine(Dirs.Output, "obj", "sharedhost");

            if (Directory.Exists(SharedHostPublishRoot))
            {
                Directory.Delete(SharedHostPublishRoot, true);
            }

            DotNetCli.Stage0.Publish("--output", SharedHostPublishRoot, Path.Combine(Dirs.RepoRoot, "src", "sharedframework", "host")).Execute().EnsureSuccessful();

            // For the shared host, we only want corerun and not any of the other artifacts in the package (like the hostpolicy)
            foreach (var filePath in Directory.GetFiles(SharedHostPublishRoot))
            {
                if (Path.GetFileName(filePath) != $"{CoreHostBaseName}{Constants.ExeSuffix}")
                {
                    File.Delete(filePath);
                }
            }

            // corehost will be renamed to dotnet at some point and then this can be removed.
            File.Move(Path.Combine(SharedHostPublishRoot, $"{CoreHostBaseName}{Constants.ExeSuffix}"), Path.Combine(SharedHostPublishRoot, $"dotnet{Constants.ExeSuffix}"));

            c.BuildContext["SharedHostPublishRoot"] = SharedHostPublishRoot;

            return c.Success();
        }
    }
}
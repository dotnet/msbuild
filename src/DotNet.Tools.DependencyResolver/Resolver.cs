using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DotNet.Tools.DependencyResolver
{
    public static class Resolver
    {
        public static int Execute(IEnumerable<string> packageDirectories, string targetName, string output, IEnumerable<string> assetTypes, string lockFilePath)
        {
            // Open the lock file
            var lockFile = JObject.Parse(File.ReadAllText(lockFilePath));

            // Locate the target
            var target = lockFile["targets"][targetName] as JObject;
            if (target == null)
            {
                Console.Error.WriteLine($"Could not find target in lock file: {target}");
                return 1;
            }

            // Iterate over each package and prepare the dependency data
            bool success = true;
            List<string> files = new List<string>();
            foreach (var dependency in target)
            {
                // Parse the input string
                var splat = dependency.Key.Split('/');
                var id = splat[0];
                var version = splat[1];

                string packageRoot = null;
                foreach (var dir in packageDirectories)
                {
                    var candidate = Path.Combine(dir, id, version);
                    if (Directory.Exists(candidate))
                    {
                        packageRoot = candidate;
                        break;
                    }
                }

                if (packageRoot == null)
                {
                    Console.Error.WriteLine($"WARNING: Unable to locate {id} {version}");
                    success = false;
                }

                // Locate all the assets
                foreach (var assetType in assetTypes)
                {
                    var assetList = dependency.Value[assetType] as JObject;
                    if (assetList != null)
                    {
                        foreach (var asset in assetList)
                        {
                            var pathified = Path.Combine(asset.Key.Split('/'));
                            if (!Path.GetFileName(pathified).Equals("_._", StringComparison.Ordinal))
                            {
                                var file = Path.Combine(packageRoot, pathified);
                                if (!File.Exists(file))
                                {
                                    Console.Error.WriteLine($"WARNING: Missing asset: {file}");
                                    success = false;
                                }
                                files.Add(file);
                                Console.WriteLine(file);
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(output))
            {
                File.WriteAllLines(output, files);
            }

            return success ? 0 : 1;
        }
    }
}
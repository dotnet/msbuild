using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    public static class Utils
    {
        public static void CleanNuGetTempCache()
        {
            // Clean NuGet Temp Cache on Linux (seeing some issues on Linux)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && Directory.Exists("/tmp/NuGet"))
            {
                Directory.Delete("/tmp/NuGet", recursive: true);
            }
        }

        public static string GetOSName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "osx";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        public static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                var retry = 5;
                while (retry >= 0)
                {
                    try
                    {
                        Directory.Delete(path, true);
                        return;
                    }
                    catch (IOException)
                    {
                        if (retry == 0)
                        {
                            throw;
                        }
                        System.Threading.Thread.Sleep(200);
                        retry--;
                    }
                }
            }
        }

        public static void CopyDirectoryRecursively(string path, string destination, bool keepParentDir = false)
        {
            if (keepParentDir)
            {
                path = path.TrimEnd(Path.DirectorySeparatorChar);
                destination = Path.Combine(destination, Path.GetFileName(path));
                Directory.CreateDirectory(destination);
            }

            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                string destFile = file.Replace(path, destination);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                File.Copy(file, destFile, true);
            }
        }

        public static string GetVersionFileContent(string commitHash, string version)
        {
            return $@"{commitHash}{Environment.NewLine}{version}{Environment.NewLine}";
        }
    }
}

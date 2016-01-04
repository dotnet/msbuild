using System.IO;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    internal static class CoreHost
    {
        internal static string _path;

        public static string FileName = "corehost" + FileNameSuffixes.CurrentPlatform.Exe;

        public static string Path
        {
            get
            {
                if (_path == null)
                {
                    _path = Env.GetCommandPath(FileName, new[] {string.Empty});
                }

                return _path;
            }
        }

        public static void CopyTo(string destinationPath)
        {
            File.Copy(Path, destinationPath, overwrite: true);
        }
    }
}
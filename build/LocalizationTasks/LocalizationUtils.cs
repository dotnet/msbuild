using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.LocalizationTasks
{
    public static class LocalizationUtils
    {
        /// <summary>
        /// Path to the root directory that contains the xlf files for a neutral resource
        /// Assumes all neutral resource have the same relative path to their xlf
        /// </summary>
        public static string RelativePathToXlfRoot = "xlf";

        public static string ComputeXlfRootPath(ITaskItem neutralResource)
        {
            var neutralResxRootDirectory = Path.GetDirectoryName(neutralResource.GetMetadata("FullPath"));
            return Path.Combine(neutralResxRootDirectory, RelativePathToXlfRoot);
        }

        public static bool IsValidLocalizedXlfName(string xlfPath, string neutralResx)
        {
            var resxFileName = Path.GetFileNameWithoutExtension(neutralResx);
            var xlfFileName = Path.GetFileName(xlfPath);

            return Regex.IsMatch(xlfFileName, $"^{resxFileName}\\.[a-zA-Z\\-]+\\.xlf$");
        }

        public static IEnumerable<string> LocalizedXlfFiles(string xlfRootPath, string neutralResx)
        {
            return Directory.EnumerateFiles(xlfRootPath)
                .Where(f => IsValidLocalizedXlfName(f, neutralResx));
        }

        public static IEnumerable<string> LocalizedXlfFiles(ITaskItem neutralResouce)
        {
            return LocalizedXlfFiles(ComputeXlfRootPath(neutralResouce), neutralResouce.ItemSpec);
        }
    }
}
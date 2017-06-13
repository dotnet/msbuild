using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.Cli.Build
{
    public class GenerateMSBuildExtensionsSWR : Task
    {
        [Required]
        public string MSBuildExtensionsLayoutDirectory { get; set; }

        [Required]
        public string OutputFile { get; set; }

        public override bool Execute()
        {
            StringBuilder sb = new StringBuilder(SWR_HEADER);

            AddFolder(sb,
                      @"MSBuildSdkResolver",
                      @"MSBuild\15.0\Bin\SdkResolvers\Microsoft.DotNet.MSBuildSdkResolver");

            AddFolder(sb,
                      @"msbuildExtensions",
                      @"MSBuild");

            AddFolder(sb,
                      @"msbuildExtensions-ver",
                      @"MSBuild\15.0");

            File.WriteAllText(OutputFile, sb.ToString());

            return true;
        }

        private void AddFolder(StringBuilder sb, string relativeSourcePath, string swrInstallDir)
        {
            string sourceFolder = Path.Combine(MSBuildExtensionsLayoutDirectory, relativeSourcePath);
            var files = Directory.GetFiles(sourceFolder)
                            .Where(f => !Path.GetExtension(f).Equals(".pdb", StringComparison.OrdinalIgnoreCase))
                            .ToList();
            if (files.Any())
            {
                sb.Append(@"folder ""InstallDir:\");
                sb.Append(swrInstallDir);
                sb.AppendLine(@"\""");

                foreach (var file in files)
                {
                    sb.Append(@"  file source=""!(bindpath.sources)\Redist\Common\NetCoreSDK\MSBuildExtensions\");
                    sb.Append(Path.Combine(relativeSourcePath, Path.GetFileName(file)));
                    sb.AppendLine("\"");
                }

                sb.AppendLine();
            }

            foreach (var subfolder in Directory.GetDirectories(sourceFolder))
            {
                string subfolderName = Path.GetFileName(subfolder);
                string newRelativeSourcePath = Path.Combine(relativeSourcePath, subfolderName);
                string newSwrInstallDir = Path.Combine(swrInstallDir, subfolderName);

                AddFolder(sb, newRelativeSourcePath, newSwrInstallDir);
            }
        }

        readonly string SWR_HEADER = @"use vs

package name=Microsoft.Net.Core.SDK.MSBuildExtensions
        version=$(Version)
        vs.package.branch=$(VsSingletonPackageBranch)
        vs.package.internalRevision=$(PackageInternalRevision)

";
    }
}

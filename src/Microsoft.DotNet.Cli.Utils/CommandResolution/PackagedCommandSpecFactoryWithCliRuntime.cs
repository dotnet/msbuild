using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Tools.Common;
using NuGet.Packaging;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    public class PackagedCommandSpecFactoryWithCliRuntime : PackagedCommandSpecFactory
    {
        public PackagedCommandSpecFactoryWithCliRuntime() : base(AddAditionalParameters)
        {
        }

        private static void AddAditionalParameters(string commandPath, IList<string> arguments)
        {
            if(PrefersCliRuntime(commandPath))
            {
                arguments.Add("--fx-version");
                arguments.Add(new Muxer().SharedFxVersion);
            }
        }

        private static bool PrefersCliRuntime(string commandPath)
        {
            var libTFMPackageDirectory = Path.GetDirectoryName(commandPath);
            var packageDirectory = Path.Combine(libTFMPackageDirectory, "..", "..");
            var preferCliRuntimePath = Path.Combine(packageDirectory, "prefercliruntime");

            Reporter.Verbose.WriteLine(
                string.Format(
                    LocalizableStrings.LookingForPreferCliRuntimeFile,
                    "packagedcommandspecfactory",
                    preferCliRuntimePath));

            return File.Exists(preferCliRuntimePath);
        }
    }
}

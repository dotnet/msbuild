// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.CommandFactory
{
    public class PackagedCommandSpecFactoryWithCliRuntime : PackagedCommandSpecFactory
    {
        public PackagedCommandSpecFactoryWithCliRuntime() : base(AddAdditionalParameters)
        {
        }

        private static void AddAdditionalParameters(string commandPath, IList<string> arguments)
        {
            if (PrefersCliRuntime(commandPath))
            {
                var runtimeConfigFile = Path.ChangeExtension(commandPath, FileNameSuffixes.RuntimeConfigJson);

                if (!File.Exists(runtimeConfigFile))
                {
                    throw new GracefulException(string.Format(LocalizableStrings.CouldNotFindToolRuntimeConfigFile,
                                                              nameof(PackagedCommandSpecFactory),
                                                              Path.GetFileName(commandPath)));
                }

                var runtimeConfig = new RuntimeConfig(runtimeConfigFile);

                var muxer = new Muxer();

                Version currentFrameworkSimpleVersion = GetVersionWithoutPrerelease(muxer.SharedFxVersion);
                Version toolFrameworkSimpleVersion = GetVersionWithoutPrerelease(runtimeConfig.Framework.Version);

                if (currentFrameworkSimpleVersion.Major != toolFrameworkSimpleVersion.Major)
                {
                    Reporter.Verbose.WriteLine(
                        string.Format(
                            LocalizableStrings.IgnoringPreferCLIRuntimeFile,
                            nameof(PackagedCommandSpecFactory),
                            runtimeConfig.Framework.Version,
                            muxer.SharedFxVersion));
                }
                else
                {
                    arguments.Add("--fx-version");
                    arguments.Add(muxer.SharedFxVersion);
                }
            }
        }

        private static Version GetVersionWithoutPrerelease(string version)
        {
            int dashOrPlusIndex = version.IndexOfAny(new char[] { '-', '+' });

            if (dashOrPlusIndex >= 0)
            {
                version = version.Substring(0, dashOrPlusIndex);
            }

            return new Version(version);
        }

        private static bool PrefersCliRuntime(string commandPath)
        {
            var libTFMPackageDirectory = Path.GetDirectoryName(commandPath);
            var packageDirectory = Path.Combine(libTFMPackageDirectory, "..", "..");
            var preferCliRuntimePath = Path.Combine(packageDirectory, "prefercliruntime");

            Reporter.Verbose.WriteLine(
                string.Format(
                    LocalizableStrings.LookingForPreferCliRuntimeFile,
                    nameof(PackagedCommandSpecFactory),
                    preferCliRuntimePath));

            return File.Exists(preferCliRuntimePath);
        }
    }
}

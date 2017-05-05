// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.MSBuildSdkResolver
{
    public sealed class DotNetMSBuildSdkResolver : SdkResolver
    {
        public override string Name => "Microsoft.DotNet.MSBuildSdkResolver";

        // Default resolver has priority 10000 and we want to go before it and leave room on either side of us. 
        public override int Priority => 5000;

        public override SdkResult Resolve(SdkReference sdkReference, SdkResolverContext context, SdkResultFactory factory)
        {
            // These are overrides that are used to force the resolved SDK tasks and targets to come from a given
            // base directory and report a given version to msbuild (which may be null if unknown. One key use case
            // for this is to test SDK tasks and targets without deploying them inside the .NET Core SDK.
            string msbuildSdksDir = Environment.GetEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR");
            string netcoreSdkVersion = Environment.GetEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER");

            if (msbuildSdksDir == null)
            {
                string netcoreSdkDir = ResolveNetcoreSdkDirectory(context);
                if (netcoreSdkDir == null)
                {
                    return factory.IndicateFailure(
                        new[]
                        {
                            "Unable to locate the .NET Core SDK. Check that it is installed and that the version"
                            + " specified in global.json (if any) matches the installed version."
                        });
                }

                msbuildSdksDir = Path.Combine(netcoreSdkDir, "Sdks");
                netcoreSdkVersion = new DirectoryInfo(netcoreSdkDir).Name;;
            }

            string msbuildSdkDir = Path.Combine(msbuildSdksDir, sdkReference.Name, "Sdk");
            if (!Directory.Exists(msbuildSdkDir))
            {
                return factory.IndicateFailure(
                    new[] 
                    {
                        $"{msbuildSdkDir} not found. Check that a recent enough .NET Core SDK is installed"
                        + " and/or increase the version specified in global.json. "
                    });
            }

            return factory.IndicateSuccess(msbuildSdkDir, netcoreSdkVersion);
        }

        private string ResolveNetcoreSdkDirectory(SdkResolverContext context)
        {
            foreach (string exeDir in GetDotnetExeDirectoryCandidates())
            {
                string workingDir = context.SolutionFilePath ?? context.ProjectFilePath;
                string netcoreSdkDir = Interop.hostfxr_resolve_sdk(exeDir, workingDir);

                if (netcoreSdkDir != null)
                {
                    return netcoreSdkDir;
                }
            }

            return null;
        }

        // Search for [ProgramFiles]\dotnet in this order.
        private static readonly string[] s_programFiles = new[]
        {
            // "c:\Program Files" on x64 machine regardless process architecture.
            // Undefined on x86 machines.
            "ProgramW6432",

            // "c:\Program Files (x86)" on x64 machine regardless of process architecture
            // Undefined on x86 machines.
            "ProgramFiles(x86)",

            // "c:\Program Files" or "C:\Program Files (x86)" on x64 machine depending on process architecture. 
            // "c:\Program Files" on x86 machines (therefore not redundant with the two locations above in that case).
            //
            // NOTE: hostfxr will search this on its own if multilevel lookup is not disable, but we do it explicitly
            // to prevent an environment with disabled multilevel lookup from crippling desktop msbuild and VS.
            "ProgramFiles",
        };

        private List<string> GetDotnetExeDirectoryCandidates()
        {
            string environmentOverride = Environment.GetEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR");
            if (environmentOverride != null)
            {
                return new List<string>(capacity: 1) { environmentOverride };
            }

            // Initial capacity is 2 because while there are 3 candidates, we expect at most 2 unique ones (x64 + x86)
            // Also, N=3 here means that we needn't be concerned with the O(N^2) complexity of the foreach + contains.
            var candidates = new List<string>(capacity: 2); 
            foreach (string variable in s_programFiles)
            {
                string directory = Environment.GetEnvironmentVariable(variable);
                if (directory == null)
                {
                    continue;
                }

                directory = Path.Combine(directory, "dotnet");
                if (!candidates.Contains(directory))
                {
                    candidates.Add(directory);
                }
            }

            if (candidates.Count == 0)
            {
                candidates.Add(null); 
            }

            return candidates;
        }
    }
}

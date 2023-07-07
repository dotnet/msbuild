// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.NativeWrapper;
using System.Collections.Immutable;

#if NET
using Microsoft.DotNet.Cli;
#else
using Microsoft.DotNet.DotNetSdkResolver;
#endif

#nullable disable

namespace Microsoft.NET.Sdk.WorkloadMSBuildSdkResolver
{

    //  This SdkResolver is used by the .NET SDK version of MSBuild.  Workload resolution logic which
    //  is shared with Full Framework / Visual Studio MSBuild is in CachingWorkloadResolver.
    public class WorkloadSdkResolver : SdkResolver
    {
        public override string Name => "Microsoft.DotNet.MSBuildWorkloadSdkResolver";

        public override int Priority => 4000;

        private class CachedState
        {
            public string DotnetRootPath { get; init; }
            public string SdkVersion { get; init; }

            public string GlobalJsonPath { get; init; }

            public CachingWorkloadResolver WorkloadResolver { get; init; }
        }

        public override SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
        {
            CachedState cachedState = null;

            if (resolverContext.State is CachedState resolverContextState)
            {
                cachedState = resolverContextState;
            }

            
            if (cachedState == null)
            {
                var dotnetRootPath = GetDotNetRoot(resolverContext);

                var sdkDirectory = GetSdkDirectory(resolverContext);
                //  The SDK version is the name of the SDK directory (ie dotnet\sdk\5.0.100)
                var sdkVersion = Path.GetFileName(sdkDirectory);

                var globalJsonPath = SdkDirectoryWorkloadManifestProvider.GetGlobalJsonPath(GetGlobalJsonStartDir(resolverContext));

                cachedState = new CachedState()
                {
                    DotnetRootPath = dotnetRootPath,
                    SdkVersion = sdkVersion,
                    GlobalJsonPath = globalJsonPath,
                    WorkloadResolver = new CachingWorkloadResolver()
                };

                resolverContext.State = cachedState;
            }

            string userProfileDir = CliFolderPathCalculatorCore.GetDotnetUserProfileFolderPath();
            var result = cachedState.WorkloadResolver.Resolve(sdkReference.Name, cachedState.DotnetRootPath, cachedState.SdkVersion, userProfileDir, cachedState.GlobalJsonPath);


            return result.ToSdkResult(sdkReference, factory);
        }

        private string GetSdkDirectory(SdkResolverContext context)
        {
#if NET
            var sdkDirectory = Path.GetDirectoryName(typeof(DotnetFiles).Assembly.Location);
            return sdkDirectory;

#else
            string dotnetExeDir = EnvironmentProvider.GetDotnetExeDirectory();
            string globalJsonStartDir = Path.GetDirectoryName(context.SolutionFilePath ?? context.ProjectFilePath);
            var sdkResolutionResult = _sdkResolver.ResolveNETCoreSdkDirectory(globalJsonStartDir, context.MSBuildVersion, context.IsRunningInVisualStudio, dotnetExeDir);

            return sdkResolutionResult.ResolvedSdkDirectory;
#endif

        }

        private string GetDotNetRoot(SdkResolverContext context)
        {
            var sdkDirectory = GetSdkDirectory(context);
            var dotnetRoot = Directory.GetParent(sdkDirectory).Parent.FullName;
            return dotnetRoot;
        }

        //  Duplicated logic from DotNetMSBuildSdkResolver
        private static string GetGlobalJsonStartDir(SdkResolverContext context)
        {
            string startDir = Environment.CurrentDirectory;

            if (!string.IsNullOrWhiteSpace(context.SolutionFilePath))
            {
                startDir = Path.GetDirectoryName(context.SolutionFilePath);
            }
            else if (!string.IsNullOrWhiteSpace(context.ProjectFilePath))
            {
                startDir = Path.GetDirectoryName(context.ProjectFilePath);
            }

            return startDir;
        }
    }
}


// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.Collections.Immutable;

#if NET
using Microsoft.DotNet.Cli;
#else
using Microsoft.DotNet.DotNetSdkResolver;
#endif

#if USE_SERILOG
using Serilog;
#endif

#nullable disable

namespace Microsoft.NET.Sdk.WorkloadMSBuildSdkResolver
{

    //  This SdkResolver is used by the .NET SDK version of MSBuild.  Workload resolution logic which
    //  is shared with Full Framework / Visual Studio MSBuild is in WorkloadPartialResolver.
    public class WorkloadSdkResolver : SdkResolver
    {
        public override string Name => "Microsoft.DotNet.MSBuildWorkloadSdkResolver";

        public override int Priority => 4000;

        public WorkloadSdkResolver()
        {
#if USE_SERILOG
            _instanceId = System.Threading.Interlocked.Increment(ref _lastInstanceId);
#endif
        }

#if USE_SERILOG
        static Serilog.Core.Logger Logger;

        static int _lastInstanceId = 1;
        int _instanceId;

        static WorkloadSdkResolver()
        {
            Logger = new LoggerConfiguration()
                .WriteTo.Seq("http://localhost:5341")
                .CreateLogger();

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                Logger.Dispose();
            };
        }
#endif

        private class CachedState
        {
            public string DotnetRootPath { get; init; }
            public string SdkVersion { get; init; }

            public WorkloadPartialResolver WorkloadResolver { get; init; }
        }

        public override SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
        {
#if USE_SERILOG
            List<Action<Serilog.ILogger>> logActions = new List<Action<Serilog.ILogger>>();
#endif

            CachedState cachedState = null;

            if (resolverContext.State is CachedState resolverContextState)
            {
                cachedState = resolverContextState;
            }

            
            if (cachedState == null)
            {
#if USE_SERILOG
                logActions.Add(log => log.Information("Initializing resolver state"));
#endif
                var dotnetRootPath = GetDotNetRoot(resolverContext);

                var sdkDirectory = GetSdkDirectory(resolverContext);
                //  The SDK version is the name of the SDK directory (ie dotnet\sdk\5.0.100)
                var sdkVersion = Path.GetFileName(sdkDirectory);

                cachedState = new CachedState()
                {
                    DotnetRootPath = dotnetRootPath,
                    SdkVersion = sdkVersion,
                    WorkloadResolver = new WorkloadPartialResolver()
                };

                resolverContext.State = cachedState;
            }
            else
            {
#if USE_SERILOG
                logActions.Add(log => log.Information("Using cached resolver state"));
#endif
            }

            var result = cachedState.WorkloadResolver.Resolve(sdkReference.Name, cachedState.DotnetRootPath, cachedState.SdkVersion
#if USE_SERILOG
                , logActions
#endif
                );


#if USE_SERILOG
            var msbuildSubmissionId = (int?)System.Threading.Thread.GetData(System.Threading.Thread.GetNamedDataSlot("MSBuildSubmissionId"));
            var log = Logger
                .ForContext("Resolver", "Sdk")
                .ForContext("Process", System.Diagnostics.Process.GetCurrentProcess().Id)
                .ForContext("Thread", System.Threading.Thread.CurrentThread.ManagedThreadId)
                .ForContext("ResolverInstance", _instanceId)
                .ForContext("RunningInVS", resolverContext.IsRunningInVisualStudio)
                .ForContext("MSBuildSubmissionId", msbuildSubmissionId);

            foreach (var logAction in logActions)
            {
                logAction(log);
            }
#endif

            return result.ToSdkResult(sdkReference, factory);

        }

        private string GetSdkDirectory(SdkResolverContext context)
        {
#if NET
            var sdkDirectory = Path.GetDirectoryName(typeof(DotnetFiles).Assembly.Location);
            return sdkDirectory;

#else
            string dotnetExeDir = _sdkResolver.GetDotnetExeDirectory();
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
    }
}


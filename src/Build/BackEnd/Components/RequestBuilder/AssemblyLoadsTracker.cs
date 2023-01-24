// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections.Concurrent;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.Components.RequestBuilder
{
    internal class AssemblyLoadsTracker : IDisposable
    {
        private static readonly ConcurrentDictionary<AppDomain, AssemblyLoadsTracker> s_instances =
            new ConcurrentDictionary<AppDomain, AssemblyLoadsTracker>();
        private readonly LoggingContext _loggingContext;
        private readonly AppDomain _appDomain;

        private AssemblyLoadsTracker(LoggingContext loggingContext)
            : this(loggingContext, AppDomain.CurrentDomain)
        { }

        private AssemblyLoadsTracker(LoggingContext loggingContext, AppDomain appDomain)
        {
            _loggingContext = loggingContext;
            _appDomain = appDomain;
        }

        public static IDisposable StartTracking(LoggingContext loggingContext, AppDomain? appDomain = null)
        {
            var tracker = new AssemblyLoadsTracker(loggingContext, appDomain ?? AppDomain.CurrentDomain);
            if (appDomain != null)
            {
                s_instances.AddOrUpdate(appDomain, tracker, (_, loadsTracker) => loadsTracker);
            }
            tracker.StartTracking();
            return tracker;
        }

        public static void StopTracking(AppDomain appDomain)
        {
            if (s_instances.TryRemove(appDomain, out AssemblyLoadsTracker? tracker))
            {
                tracker.StopTracking();
            }
        }

        public void Dispose()
        {
            StopTracking();
        }
        private void StartTracking()
        {
            _appDomain.AssemblyLoad += CurrentDomainOnAssemblyLoad;
        }

        private void StopTracking()
        {
            _appDomain.AssemblyLoad -= CurrentDomainOnAssemblyLoad;
        }

        private void CurrentDomainOnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
        {
            string? assemblyName = args.LoadedAssembly.FullName;
            string? assemblyPath = args.LoadedAssembly.Location;
            Guid mvid = args.LoadedAssembly.ManifestModule.ModuleVersionId;

            AssemblyLoadBuildEventArgs buildArgs = new(assemblyName, assemblyPath, mvid, _appDomain.Id, _appDomain.FriendlyName)
            {
                BuildEventContext = _loggingContext.BuildEventContext
            };
            _loggingContext.LogBuildEvent(buildArgs);
        }
    }
}

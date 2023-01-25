// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections.Concurrent;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.Components.RequestBuilder
{
    internal class AssemblyLoadsTracker : MarshalByRefObject, IDisposable
    {
        private static readonly ConcurrentDictionary<AppDomain, AssemblyLoadsTracker> s_instances = new();
        private readonly LoggingContext? _loggingContext;
        private readonly LoggingService? _loggingService;
        private readonly AssemblyLoadingContext _context;
        private readonly AppDomain _appDomain;

        private AssemblyLoadsTracker(
            LoggingContext? loggingContext,
            LoggingService? loggingService,
            AssemblyLoadingContext context,
            AppDomain appDomain)
        {
            _loggingContext = loggingContext;
            _loggingService = loggingService;
            _context = context;
            _appDomain = appDomain;
        }

        public static IDisposable StartTracking(
            LoggingContext loggingContext,
            AssemblyLoadingContext context,
            AppDomain? appDomain = null)
        {
            return StartTracking(loggingContext, null, context, appDomain);
        }

        public static IDisposable StartTracking(
            LoggingService loggingService,
            AssemblyLoadingContext context,
            AppDomain? appDomain = null)
        {
            return StartTracking(null, loggingService, context, appDomain);
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

        private static IDisposable StartTracking(
            LoggingContext? loggingContext,
            LoggingService? loggingService,
            AssemblyLoadingContext context,
            AppDomain? appDomain = null)
        {
            var tracker = new AssemblyLoadsTracker(loggingContext, loggingService, context, appDomain ?? AppDomain.CurrentDomain);
            if (appDomain != null)
            {
                s_instances.AddOrUpdate(appDomain, tracker, (_, loadsTracker) => loadsTracker);
            }
            tracker.StartTracking();
            return tracker;
        }

        private void StartTracking()
        {
            // Make multisubscriptions idempotent
            _appDomain.AssemblyLoad -= CurrentDomainOnAssemblyLoad;
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

            AssemblyLoadBuildEventArgs buildArgs = new(_context, assemblyName, assemblyPath, mvid, _appDomain.Id, _appDomain.FriendlyName)
            {
                BuildEventContext = _loggingContext?.BuildEventContext
            };
            _loggingContext?.LogBuildEvent(buildArgs);
            _loggingService?.LogBuildEvent(buildArgs);
        }
    }
}

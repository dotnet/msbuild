// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if FEATURE_APPDOMAIN
using System.Collections.Generic;
using System.Linq;
#endif
using System.Reflection;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.Components.RequestBuilder
{
    internal sealed class AssemblyLoadsTracker : MarshalByRefObject, IDisposable
    {
#if FEATURE_APPDOMAIN
        private static readonly List<AssemblyLoadsTracker> s_instances = new();
#endif
        private readonly LoggingContext? _loggingContext;
        private readonly LoggingService? _loggingService;
        private readonly AssemblyLoadingContext _context;
        private readonly string? _initiator;
        private readonly AppDomain _appDomain;

        private AssemblyLoadsTracker(
            LoggingContext? loggingContext,
            LoggingService? loggingService,
            AssemblyLoadingContext context,
            Type? initiator,
            AppDomain appDomain)
        {
            _loggingContext = loggingContext;
            _loggingService = loggingService;
            _context = context;
            _initiator = initiator?.FullName;
            _appDomain = appDomain;
        }

        public static IDisposable StartTracking(
            LoggingContext loggingContext,
            AssemblyLoadingContext context,
            Type? initiator,
            AppDomain? appDomain = null)
            => StartTracking(loggingContext, null, context, initiator, null, appDomain);

        public static IDisposable StartTracking(
            LoggingContext loggingContext,
            AssemblyLoadingContext context,
            string? initiator = null,
            AppDomain? appDomain = null)
            => StartTracking(loggingContext, null, context, null, initiator, appDomain);

        public static IDisposable StartTracking(
            LoggingService loggingService,
            AssemblyLoadingContext context,
            Type initiator,
            AppDomain? appDomain = null)
            => StartTracking(null, loggingService, context, initiator, null, appDomain);



#if FEATURE_APPDOMAIN
        public static void StopTracking(AppDomain appDomain)
        {
            if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_6) && !appDomain.IsDefaultAppDomain())
            {
                lock (s_instances)
                {
                    foreach (AssemblyLoadsTracker tracker in s_instances.Where(t => t._appDomain == appDomain))
                    {
                        tracker.StopTracking();
                    }

                    s_instances.RemoveAll(t => t._appDomain == appDomain);
                }
            }
        }
#endif

        public void Dispose()
        {
            StopTracking();
        }

        private static bool IsBuiltinType(string? typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            return typeName!.StartsWith("Microsoft.Build", StringComparison.Ordinal) ||
                   typeName.StartsWith("Microsoft.NET.Build", StringComparison.Ordinal) ||
                   typeName.StartsWith("Microsoft.NET.Sdk", StringComparison.Ordinal);
        }

        private static IDisposable StartTracking(
            LoggingContext? loggingContext,
            LoggingService? loggingService,
            AssemblyLoadingContext context,
            Type? initiatorType,
            string? initiatorName,
            AppDomain? appDomain)
        {
            if (
                // Feature is not enabled
                !ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_6) ||
                (
                    // We do not want to load all assembly loads (including those triggered by builtin types)
                    !Traits.Instance.LogAllAssemblyLoads &&
                    (
                        // Load will be initiated by internal type - so we are not interested in those
                        initiatorType?.Assembly == Assembly.GetExecutingAssembly()
                        ||
                        IsBuiltinType(initiatorType?.FullName)
                        ||
                        IsBuiltinType(initiatorName)
                    )
                )
            )
            {
                return EmptyDisposable.Instance;
            }

            var tracker = new AssemblyLoadsTracker(loggingContext, loggingService, context, initiatorType, appDomain ?? AppDomain.CurrentDomain);
#if FEATURE_APPDOMAIN
            if (appDomain != null && !appDomain.IsDefaultAppDomain())
            {
                lock (s_instances)
                {
                    s_instances.Add(tracker);
                }
            }
#endif
            tracker.StartTracking();
            return tracker;
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
            string assemblyPath = args.LoadedAssembly.IsDynamic ? string.Empty : args.LoadedAssembly.Location;
            Guid mvid = args.LoadedAssembly.ManifestModule.ModuleVersionId;
            string? appDomainDescriptor = _appDomain.IsDefaultAppDomain()
                ? null
                : $"{_appDomain.Id}|{_appDomain.FriendlyName}";


            AssemblyLoadBuildEventArgs buildArgs = new(_context, _initiator, assemblyName, assemblyPath, mvid, appDomainDescriptor);

            // Fix #8816 - when LoggingContext does not have BuildEventContext it is unable to log anything
            if (_loggingContext?.BuildEventContext != null)
            {
                buildArgs.BuildEventContext = _loggingContext.BuildEventContext;
                _loggingContext.LogBuildEvent(buildArgs);
            }
            _loggingService?.LogBuildEvent(buildArgs);
        }

        private class EmptyDisposable : IDisposable
        {
            public static readonly IDisposable Instance = new EmptyDisposable();
            public void Dispose() { }
        }
    }
}

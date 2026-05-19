// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if FEATURE_APPDOMAIN
#endif
using System.Reflection;
#if FEATURE_ASSEMBLYLOADCONTEXT
using System.Runtime.Loader;
#endif
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.Components.RequestBuilder
{
    internal sealed class AssemblyLoadsTracker : MarshalByRefObject, IDisposable
    {
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
            if (// We do not want to load all assembly loads (including those triggered by builtin types)
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
            {
                return EmptyDisposable.Instance;
            }

#if FEATURE_APPDOMAIN
            if (appDomain != null && appDomain != AppDomain.CurrentDomain)
            {
                // Subscribing to AssemblyLoad on a remote AppDomain causes the event handler to be
                // invoked through a transparent proxy. AssemblyLoadEventArgs is not [Serializable],
                // so marshaling it across the AppDomain boundary throws SerializationException.
                // Skip assembly load tracking for tasks running in separate AppDomains.
                return EmptyDisposable.Instance;
            }
#endif

            var tracker = new AssemblyLoadsTracker(loggingContext, loggingService, context, initiatorType, appDomain ?? AppDomain.CurrentDomain);

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
#if FEATURE_ASSEMBLYLOADCONTEXT
            // AssemblyLoadContext.GetLoadContext returns null when the assembly isn't a RuntimeAssembly, which should not be the case here.
            // Name would only be null if the AssemblyLoadContext didn't supply a name, but MSBuildLoadContext does.
            string appDomainDescriptor = AssemblyLoadContext.GetLoadContext(args.LoadedAssembly)?.Name ?? "Unknown";
#else
            string? appDomainDescriptor = _appDomain.IsDefaultAppDomain()
                ? null
                : $"{_appDomain.Id}|{_appDomain.FriendlyName}";
#endif

            AssemblyLoadBuildEventArgs buildArgs = new(_context, _initiator, assemblyName, assemblyPath, mvid, appDomainDescriptor);

            // Fix #8816 - when LoggingContext does not have BuildEventContext it is unable to log anything
            if (_loggingContext?.BuildEventContext != null)
            {
                buildArgs.BuildEventContext = _loggingContext.BuildEventContext;
                // bypass the logging context validity check: it's possible that the load happened
                // on a thread unrelated to the context we're tracking loads in
                _loggingContext.LoggingService.LogBuildEvent(buildArgs);
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

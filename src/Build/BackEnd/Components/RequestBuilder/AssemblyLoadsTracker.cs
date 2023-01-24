// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.Components.RequestBuilder
{
    internal class AssemblyLoadsTracker : IDisposable
    {
        private readonly LoggingContext _loggingContext;

        private AssemblyLoadsTracker(LoggingContext loggingContext) => _loggingContext = loggingContext;

        public static IDisposable StartTracking(LoggingContext loggingContext)
        {
            // Debugger.Launch();
            var tracker = new AssemblyLoadsTracker(loggingContext);
            tracker.StartTracking();
            return tracker;
        }

        public void Dispose()
        {
            StopTracking();
        }
        private void StartTracking()
        {
            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomainOnAssemblyLoad;
        }

        private void StopTracking()
        {
            AppDomain.CurrentDomain.AssemblyLoad -= CurrentDomainOnAssemblyLoad;
        }

        private void CurrentDomainOnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
        {
            // Is it correct to get the resource within the args? Or should the caller pass it
            // (former seems as better separation of concerns)
            // string? message = ResourceUtilities.GetResourceString("TaskAssemblyLoaded");
            string? assemblyName = args.LoadedAssembly.FullName;
            string? assemblyPath = args.LoadedAssembly.Location;
            Guid mvid = args.LoadedAssembly.ManifestModule.ModuleVersionId;

            AssemblyLoadBuildEventArgs buildArgs = new(assemblyName, assemblyPath, mvid);
            buildArgs.BuildEventContext = _loggingContext.BuildEventContext;
            _loggingContext.LogBuildEvent(buildArgs);
        }
    }
}

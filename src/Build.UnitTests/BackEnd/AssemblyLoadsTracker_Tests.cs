// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_APPDOMAIN
using System;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using Microsoft.Build.BackEnd.Components.RequestBuilder;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class AssemblyLoadsTracker_Tests : IDisposable
    {
        private const string AssemblyPathDataKey = nameof(AssemblyLoadsTracker_Tests) + ".AssemblyPath";

        private readonly TestEnvironment _env;
        private readonly ITestOutputHelper _output;
        private readonly string _assemblyPath;

        public AssemblyLoadsTracker_Tests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(_output);
            _assemblyPath = _env.GetTempFile(".dll").Path;
            File.Copy(typeof(AssemblyLoadsTracker_Tests).Assembly.Location, _assemblyPath, overwrite: true);
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        [Fact]
        public void SameDomainTracker_LogsAssemblyLoads()
        {
            MockLogger logger = new(_output, verbosity: LoggerVerbosity.Diagnostic);
            LoggingService loggingService = (LoggingService)LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
            loggingService.RegisterLogger(logger);

            try
            {
                MockLoggingContext loggingContext = new(loggingService, new BuildEventContext(1, 2, 3, 4));
                using IDisposable tracker = AssemblyLoadsTracker.StartTracking(
                    loggingContext,
                    AssemblyLoadingContext.TaskRun);

                AppDomain.CurrentDomain.SetData(AssemblyPathDataKey, _assemblyPath);
                AppDomain.CurrentDomain.DoCallBack(static () =>
                {
                    bool sawSerializationException = false;
                    EventHandler<FirstChanceExceptionEventArgs> onFirstChanceException = (_, args) =>
                    {
                        if (args.Exception is SerializationException)
                        {
                            sawSerializationException = true;
                        }
                    };

                    AppDomain.CurrentDomain.FirstChanceException += onFirstChanceException;

                    try
                    {
                        string assemblyPath = (string)AppDomain.CurrentDomain.GetData(AssemblyPathDataKey)!;

                        Assembly loadedAssembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
                        _ = loadedAssembly.FullName;

                        sawSerializationException.ShouldBeFalse();
                    }
                    finally
                    {
                        AppDomain.CurrentDomain.FirstChanceException -= onFirstChanceException;
                    }
                });

                logger.AssertLogContains(typeof(AssemblyLoadsTracker_Tests).Assembly.FullName!);
            }
            finally
            {
                loggingService.ShutdownComponent();
            }
        }

        [Fact]
        public void CrossDomainTracker_DoesNotLogChildDomainLoads_AndDoesNotObserveSerializationException()
        {
            MockLogger logger = new(_output, verbosity: LoggerVerbosity.Diagnostic);
            LoggingService loggingService = (LoggingService)LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
            loggingService.RegisterLogger(logger);
            AppDomain? child = null;

            try
            {
                MockLoggingContext loggingContext = new(loggingService, new BuildEventContext(1, 2, 3, 4));
                child = AppDomain.CreateDomain(
                    "AssemblyLoadsTracker_Tests_ChildDomain",
                    null,
                    new AppDomainSetup { ApplicationBase = AppDomain.CurrentDomain.BaseDirectory });

                child.SetData(AssemblyPathDataKey, _assemblyPath);

                using IDisposable tracker = AssemblyLoadsTracker.StartTracking(
                    loggingContext,
                    AssemblyLoadingContext.TaskRun,
                    appDomain: child);

                child.DoCallBack(static () =>
                {
                    bool sawSerializationException = false;
                    EventHandler<FirstChanceExceptionEventArgs> onFirstChanceException = (_, args) =>
                    {
                        if (args.Exception is SerializationException)
                        {
                            sawSerializationException = true;
                        }
                    };

                    AppDomain.CurrentDomain.FirstChanceException += onFirstChanceException;

                    try
                    {
                        string assemblyPath = (string)AppDomain.CurrentDomain.GetData(AssemblyPathDataKey)!;
                
                        Assembly loadedAssembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
                        _ = loadedAssembly.FullName;

                        sawSerializationException.ShouldBeFalse();
                    }
                    finally
                    {
                        AppDomain.CurrentDomain.FirstChanceException -= onFirstChanceException;
                    }
                });

                logger.AssertLogDoesntContain(typeof(AssemblyLoadsTracker_Tests).Assembly.FullName!);
            }
            finally
            {
                if (child is not null)
                {
                    AppDomain.Unload(child);
                }

                loggingService.ShutdownComponent();
            }
        }
    }
}

#endif

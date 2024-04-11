// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BuildCheck.Acquisition
{
    internal class BuildCheckAcquisitionModule : IBuildCheckAcquisitionModule
    {
        private readonly ILoggingService _loggingService;

        internal BuildCheckAcquisitionModule(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

#if FEATURE_ASSEMBLYLOADCONTEXT
        /// <summary>
        /// AssemblyContextLoader used to load DLLs outside of msbuild.exe directory
        /// </summary>
        private static readonly CoreClrAssemblyLoader s_coreClrAssemblyLoader = new();
#endif

        public IEnumerable<BuildAnalyzerFactory> CreateBuildAnalyzerFactories(AnalyzerAcquisitionData analyzerAcquisitionData, BuildEventContext buildEventContext)
        {
            try
            {
                Assembly? assembly = null;
#if FEATURE_ASSEMBLYLOADCONTEXT
                assembly = s_coreClrAssemblyLoader.LoadFromPath(analyzerAcquisitionData.AssemblyPath);
#else
                assembly = Assembly.LoadFrom(analyzerAcquisitionData.AssemblyPath);
#endif

                IEnumerable<Type> analyzerTypes = assembly.GetTypes().Where(t => typeof(BuildAnalyzer).IsAssignableFrom(t));

                if (analyzerTypes.Any())
                {
                    var analyzersFactory = new List<BuildAnalyzerFactory>();
                    foreach (Type analyzerType in analyzerTypes)
                    {
                        if (Activator.CreateInstance(analyzerType) is BuildAnalyzer instance)
                        {
                            analyzersFactory.Add(() => instance);
                        }
                        else
                        {
                            throw new InvalidOperationException($"Failed to create an instance of type {analyzerType.FullName} as BuildAnalyzer.");
                        }
                    }

                    return analyzersFactory;
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                if (ex.LoaderExceptions.Length != 0)
                {
                    foreach (Exception? loaderException in ex.LoaderExceptions)
                    {
                        _loggingService.LogComment(buildEventContext, MessageImportance.Normal, "CustomAnalyzerFailedRuleLoading", loaderException?.Message);
                    }
                }
            }

            return Enumerable.Empty<BuildAnalyzerFactory>();
        }
    }
}

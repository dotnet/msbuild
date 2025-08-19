﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Acquisition;

internal class BuildCheckAcquisitionModule : IBuildCheckAcquisitionModule
{
    private readonly ILoggingService _loggingService;

    internal BuildCheckAcquisitionModule(ILoggingService loggingService) => _loggingService = loggingService;

#if FEATURE_ASSEMBLYLOADCONTEXT
    /// <summary>
    /// AssemblyContextLoader used to load DLLs outside of msbuild.exe directory.
    /// </summary>
    private static readonly CoreClrAssemblyLoader s_coreClrAssemblyLoader = new();
#endif

    /// <summary>
    /// Creates a list of factory delegates for building analyzer rules instances from a given assembly path.
    /// </summary>
    public List<BuildAnalyzerFactory> CreateBuildAnalyzerFactories(AnalyzerAcquisitionData analyzerAcquisitionData, BuildEventContext buildEventContext)
    {
        var analyzersFactories = new List<BuildAnalyzerFactory>();

        try
        {
            Assembly? assembly = null;
#if FEATURE_ASSEMBLYLOADCONTEXT
            assembly = s_coreClrAssemblyLoader.LoadFromPath(analyzerAcquisitionData.AssemblyPath);
#else
            assembly = Assembly.LoadFrom(analyzerAcquisitionData.AssemblyPath);
#endif

            IList<Type> availableTypes = assembly.GetExportedTypes();
            IList<Type> analyzerTypes = availableTypes.Where(t => typeof(BuildAnalyzer).IsAssignableFrom(t)).ToArray();

            foreach (Type analyzerCandidate in analyzerTypes)
            {
                analyzersFactories.Add(() => (BuildAnalyzer)Activator.CreateInstance(analyzerCandidate)!);
            }

            if (availableTypes.Count != analyzerTypes.Count)
            {
                availableTypes.Except(analyzerTypes).ToList().ForEach(t => _loggingService.LogComment(buildEventContext, MessageImportance.Normal, "CustomAnalyzerBaseTypeNotAssignable", t.Name, t.Assembly));
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
        catch (Exception ex)
        {
            _loggingService.LogComment(buildEventContext, MessageImportance.Normal, "CustomAnalyzerFailedRuleLoading", ex?.Message);
        }

        return analyzersFactories;
    }
}

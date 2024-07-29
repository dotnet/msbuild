// Licensed to the .NET Foundation under one or more agreements.
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
#if FEATURE_ASSEMBLYLOADCONTEXT
    /// <summary>
    /// AssemblyContextLoader used to load DLLs outside of msbuild.exe directory.
    /// </summary>
    private static readonly CoreClrAssemblyLoader s_coreClrAssemblyLoader = new();
#endif

    /// <summary>
    /// Creates a list of factory delegates for building analyzer rules instances from a given assembly path.
    /// </summary>
    public List<BuildAnalyzerFactory> CreateBuildAnalyzerFactories(
        AnalyzerAcquisitionData analyzerAcquisitionData,
        IAnalysisContext analysisContext)
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
                analysisContext.DispatchAsComment(MessageImportance.Normal, "CustomAnalyzerRegistered", analyzerCandidate.Name, analyzerCandidate.Assembly);
            }

            if (availableTypes.Count != analyzerTypes.Count)
            {
                availableTypes.Except(analyzerTypes).ToList()
                    .ForEach(t => analysisContext.DispatchAsComment(MessageImportance.Normal, "CustomAnalyzerBaseTypeNotAssignable", t.Name, t.Assembly));
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            if (ex.LoaderExceptions.Length != 0)
            {
                foreach (Exception? loaderException in ex.LoaderExceptions)
                {
                    analysisContext.DispatchAsComment(MessageImportance.Normal, "CustomAnalyzerFailedRuleLoading", loaderException?.Message);
                }
            }
        }
        catch (Exception ex)
        {
            analysisContext.DispatchAsComment(MessageImportance.Normal, "CustomAnalyzerFailedRuleLoading", ex?.Message);
        }

        return analyzersFactories;
    }
}

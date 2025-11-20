// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Framework;
#if FEATURE_ASSEMBLYLOADCONTEXT
using Microsoft.Build.Shared;
#endif

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
    /// Creates a list of factory delegates for building check rules instances from a given assembly path.
    /// </summary>
    public List<CheckFactory> CreateCheckFactories(
        CheckAcquisitionData checkAcquisitionData,
        ICheckContext checkContext)
    {
        var checksFactories = new List<CheckFactory>();

        try
        {
            Assembly? assembly = null;
#if FEATURE_ASSEMBLYLOADCONTEXT
            assembly = s_coreClrAssemblyLoader.LoadFromPath(checkAcquisitionData.AssemblyPath);
#else
            assembly = Assembly.LoadFrom(checkAcquisitionData.AssemblyPath);
#endif

            Type[] availableTypes = assembly.GetExportedTypes();
            Type[] checkTypes = availableTypes.Where(t => typeof(Check).IsAssignableFrom(t)).ToArray();

            foreach (Type checkCandidate in checkTypes)
            {
                checksFactories.Add(() => (Check)Activator.CreateInstance(checkCandidate)!);
                checkContext.DispatchAsComment(MessageImportance.Normal, "CustomCheckRegistered", checkCandidate.Name, checkCandidate.Assembly);
            }

            if (availableTypes.Length != checkTypes.Length)
            {
                availableTypes.Except(checkTypes).ToList()
                    .ForEach(t => checkContext.DispatchAsComment(MessageImportance.Normal, "CustomCheckBaseTypeNotAssignable", t.Name, t.Assembly));
            }
        }
        catch (ReflectionTypeLoadException ex) when (ex.LoaderExceptions.Length != 0)
        {
            foreach (Exception? unrolledEx in ex.LoaderExceptions.Where(e => e != null).Prepend(ex))
            {
                ReportLoadingError(unrolledEx!);
            }
        }
        catch (Exception ex)
        {
            ReportLoadingError(ex);
        }

        return checksFactories;

        void ReportLoadingError(Exception ex)
        {
            checkContext.DispatchAsComment(MessageImportance.Normal, "CustomCheckFailedRuleLoading", ex.Message);
            checkContext.DispatchFailedAcquisitionTelemetry(System.IO.Path.GetFileName(checkAcquisitionData.AssemblyPath), ex);
        }
    }
}

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
    /// Creates a list of factory delegates for building check rules instances from a given assembly path.
    /// </summary>
    public List<BuildExecutionCheckFactory> CreateBuildExecutionCheckFactories(
        CheckAcquisitionData checkAcquisitionData,
        ICheckContext checkContext)
    {
        var checksFactories = new List<BuildExecutionCheckFactory>();

        try
        {
            Assembly? assembly = null;
#if FEATURE_ASSEMBLYLOADCONTEXT
            assembly = s_coreClrAssemblyLoader.LoadFromPath(checkAcquisitionData.AssemblyPath);
#else
            assembly = Assembly.LoadFrom(checkAcquisitionData.AssemblyPath);
#endif

            IList<Type> availableTypes = assembly.GetExportedTypes();
            IList<Type> checkTypes = availableTypes.Where(t => typeof(BuildExecutionCheck).IsAssignableFrom(t)).ToArray();

            foreach (Type checkCandidate in checkTypes)
            {
                checksFactories.Add(() => (BuildExecutionCheck)Activator.CreateInstance(checkCandidate)!);
                checkContext.DispatchAsComment(MessageImportance.Normal, "CustomCheckRegistered", checkCandidate.Name, checkCandidate.Assembly);
            }

            if (availableTypes.Count != checkTypes.Count)
            {
                availableTypes.Except(checkTypes).ToList()
                    .ForEach(t => checkContext.DispatchAsComment(MessageImportance.Normal, "CustomCheckBaseTypeNotAssignable", t.Name, t.Assembly));
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            if (ex.LoaderExceptions.Length != 0)
            {
                foreach (Exception? loaderException in ex.LoaderExceptions)
                {
                    checkContext.DispatchAsComment(MessageImportance.Normal, "CustomCheckFailedRuleLoading", loaderException?.Message);
                }
            }
        }
        catch (Exception ex)
        {
            checkContext.DispatchAsComment(MessageImportance.Normal, "CustomCheckFailedRuleLoading", ex?.Message);
        }

        return checksFactories;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Configuration.Assemblies;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BuildCheck.Analyzers;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.BuildCheck.Acquisition;

internal class BuildCheckAcquisitionModule
{
    private static T Construct<T>() where T : new() => new();

    public BuildAnalyzerFactory CreateBuildAnalyzerFactory(AnalyzerAcquisitionData analyzerAcquisitionData)
    {
        try
        {
            Assembly? assembly = null;
#if FEATURE_ASSEMBLYLOADCONTEXT
            assembly = s_coreClrAssemblyLoader.LoadFromPath(assemblyPath);
#else
            assembly = Assembly.LoadFrom(analyzerAcquisitionData.AssemblyPath);
#endif

            Type type = assembly.GetTypes().FirstOrDefault();

            if (type != null)
            {
                // Check if the type is assignable to T
                if (!typeof(BuildAnalyzer).IsAssignableFrom(type))
                {
                    throw new ArgumentException($"The type is not assignable to {typeof(BuildAnalyzer).FullName}");
                }
                else
                {
                    // ??? how to instantiate
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            Console.WriteLine("Failed to load one or more types from the assembly:");
            foreach (Exception loaderException in ex.LoaderExceptions)
            {
                Console.WriteLine(loaderException.Message);
            }
        }

        return null;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BuildCheck.Acquisition
{
    internal class BuildCheckAcquisitionModule : IBuildCheckAcquisitionModule
    {
#if FEATURE_ASSEMBLYLOADCONTEXT
        /// <summary>
        /// AssemblyContextLoader used to load DLLs outside of msbuild.exe directory
        /// </summary>
        private static readonly CoreClrAssemblyLoader s_coreClrAssemblyLoader = new();
#endif

        public BuildAnalyzerFactory? CreateBuildAnalyzerFactory(AnalyzerAcquisitionData analyzerAcquisitionData)
        {
            try
            {
                Assembly? assembly = null;
#if FEATURE_ASSEMBLYLOADCONTEXT
                assembly = s_coreClrAssemblyLoader.LoadFromPath(analyzerAcquisitionData.AssemblyPath);
#else
                assembly = Assembly.LoadFrom(analyzerAcquisitionData.AssemblyPath);
#endif

                Type? analyzerType = assembly.GetTypes().FirstOrDefault(t => typeof(BuildAnalyzer).IsAssignableFrom(t));

                if (analyzerType != null)
                {
                    return () => Activator.CreateInstance(analyzerType) is not BuildAnalyzer instance
                            ? throw new InvalidOperationException($"Failed to create an instance of type {analyzerType.FullName} as BuildAnalyzer.")
                            : instance;
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                if (ex.LoaderExceptions.Length != 0)
                {
                    foreach (Exception? loaderException in ex.LoaderExceptions)
                    {
                        // How do we plan to handle these errors?
                        Console.WriteLine(loaderException?.Message ?? "Unknown error occurred.");
                    }
                }
            }

            return null;
        }
    }
}

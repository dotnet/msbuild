// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reflection;

using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Class for loading tasks
    /// </summary>
    internal static class TaskLoader
    {
#if FEATURE_APPDOMAIN
        /// <summary>
        /// For saving the assembly that was loaded by the TypeLoader
        /// We only use this when the assembly failed to load properly into the appdomain
        /// </summary>
        private static LoadedType s_resolverLoadedType;
#endif

        /// <summary>
        /// Delegate for logging task loading errors. 
        /// </summary>
        internal delegate void LogError(string taskLocation, int taskLine, int taskColumn, string message, params object[] messageArgs);

        /// <summary>
        /// Checks if the given type is a task factory.
        /// </summary>
        /// <remarks>This method is used as a type filter delegate.</remarks>
        /// <returns>true, if specified type is a task</returns>
        internal static bool IsTaskClass(Type type, object unused)
        {
            return type.GetTypeInfo().IsClass && !type.GetTypeInfo().IsAbstract && (
#if FEATURE_TYPE_GETINTERFACE
                type.GetTypeInfo().GetInterface("Microsoft.Build.Framework.ITask") != null);
#else
                type.GetInterfaces().Any(interfaceType => interfaceType.FullName == "Microsoft.Build.Framework.ITask"));
#endif
        }

        /// <summary>
        /// Creates an ITask instance and returns it.  
        /// </summary>
        internal static ITask CreateTask(LoadedType loadedType, string taskName, string taskLocation, int taskLine, int taskColumn, LogError logError
#if FEATURE_APPDOMAIN
            , AppDomainSetup appDomainSetup
#endif
            , bool isOutOfProc
#if FEATURE_APPDOMAIN
            , out AppDomain taskAppDomain
#endif
            )
        {
#if FEATURE_APPDOMAIN
            bool separateAppDomain = loadedType.HasLoadInSeparateAppDomainAttribute();
            s_resolverLoadedType = null;
            taskAppDomain = null;
            ITask taskInstanceInOtherAppDomain = null;
#endif

            try
            {
#if FEATURE_APPDOMAIN
                if (separateAppDomain)
                {
                    if (!loadedType.Type.GetTypeInfo().IsMarshalByRef)
                    {
                        logError
                        (
                            taskLocation,
                            taskLine,
                            taskColumn,
                            "TaskNotMarshalByRef",
                            taskName
                         );

                        return null;
                    }
                    else
                    {
                        // Our task depend on this name to be precisely that, so if you change it make sure
                        // you also change the checks in the tasks run in separate AppDomains. Better yet, just don't change it.

                        // Make sure we copy the appdomain configuration and send it to the appdomain we create so that if the creator of the current appdomain
                        // has done the binding redirection in code, that we will get those settings as well.
                        AppDomainSetup appDomainInfo = new AppDomainSetup();

                        // Get the current app domain setup settings
                        byte[] currentAppdomainBytes = appDomainSetup.GetConfigurationBytes();

                        // Apply the appdomain settings to the new appdomain before creating it
                        appDomainInfo.SetConfigurationBytes(currentAppdomainBytes);

                        if (BuildEnvironmentHelper.Instance.RunningTests)
                        {
                            // Prevent the new app domain from looking in the VS test runner location. If this
                            // is not done, we will not be able to find Microsoft.Build.* assemblies.
                            appDomainInfo.ApplicationBase = BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory;
                            appDomainInfo.ConfigurationFile = BuildEnvironmentHelper.Instance.CurrentMSBuildConfigurationFile;
                        }

                        AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver;
                        s_resolverLoadedType = loadedType;

                        taskAppDomain = AppDomain.CreateDomain(isOutOfProc ? "taskAppDomain (out-of-proc)" : "taskAppDomain (in-proc)", null, appDomainInfo);

                        if (loadedType.LoadedAssembly != null)
                        {
                            taskAppDomain.Load(loadedType.LoadedAssembly.GetName());
                        }

#if FEATURE_APPDOMAIN_UNHANDLED_EXCEPTION
                        // Hook up last minute dumping of any exceptions 
                        taskAppDomain.UnhandledException += ExceptionHandling.UnhandledExceptionHandler;
#endif
                    }
                }
                else
#endif
                {
                    // perf improvement for the same appdomain case - we already have the type object
                    // and don't want to go through reflection to recreate it from the name.
                    return (ITask)Activator.CreateInstance(loadedType.Type);
                }

#if FEATURE_APPDOMAIN
                if (loadedType.Assembly.AssemblyFile != null)
                {
                    taskInstanceInOtherAppDomain = (ITask)taskAppDomain.CreateInstanceFromAndUnwrap(loadedType.Assembly.AssemblyFile, loadedType.Type.FullName);

                    // this will force evaluation of the task class type and try to load the task assembly
                    Type taskType = taskInstanceInOtherAppDomain.GetType();

                    // If the types don't match, we have a problem. It means that our AppDomain was able to load
                    // a task assembly using Load, and loaded a different one. I don't see any other choice than
                    // to fail here.
                    if (taskType != loadedType.Type)
                    {
                        logError
                        (
                        taskLocation,
                        taskLine,
                        taskColumn,
                        "ConflictingTaskAssembly",
                        loadedType.Assembly.AssemblyFile,
                        loadedType.Type.GetTypeInfo().Assembly.Location
                        );

                        taskInstanceInOtherAppDomain = null;
                    }
                }
                else
                {
                    taskInstanceInOtherAppDomain = (ITask)taskAppDomain.CreateInstanceAndUnwrap(loadedType.Type.GetTypeInfo().Assembly.FullName, loadedType.Type.FullName);
                }

                return taskInstanceInOtherAppDomain;
#endif
            }
            finally
            {
#if FEATURE_APPDOMAIN
                // Don't leave appdomains open
                if (taskAppDomain != null && taskInstanceInOtherAppDomain == null)
                {
                    AppDomain.Unload(taskAppDomain);
                    RemoveAssemblyResolver();
                }
#endif
            }
        }

#if FEATURE_APPDOMAIN
        /// <summary>
        /// This is a resolver to help created AppDomains when they are unable to load an assembly into their domain we will help
        /// them succeed by providing the already loaded one in the currentdomain so that they can derive AssemblyName info from it
        /// </summary>
        internal static Assembly AssemblyResolver(object sender, ResolveEventArgs args)
        {
            if ((s_resolverLoadedType?.LoadedAssembly != null))
            {
                // Match the name being requested by the resolver with the FullName of the assembly we have loaded
                if (args.Name.Equals(s_resolverLoadedType.LoadedAssembly.FullName, StringComparison.Ordinal))
                {
                    return s_resolverLoadedType.LoadedAssembly;
                }
            }

            return null;
        }

        /// <summary>
        /// Check if we added a resolver and remove it
        /// </summary>
        internal static void RemoveAssemblyResolver()
        {
            if (s_resolverLoadedType != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= AssemblyResolver;
                s_resolverLoadedType = null;
            }
        }
#endif
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
#if FEATURE_APPDOMAIN
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Build.Shared.Debugging;
#endif

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
        private static LoadedType? s_resolverLoadedType;
#endif

        /// <summary>
        /// Delegate for logging task loading errors.
        /// </summary>
        internal delegate void LogError(string taskLocation, int taskLine, int taskColumn, string message, params object[] messageArgs);

        /// <summary>
        /// Creates an ITask instance and returns it.
        /// </summary>
#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
        internal static ITask? CreateTask(
            LoadedType loadedType,
            string taskName,
            string taskLocation,
            int taskLine,
            int taskColumn,
            LogError logError,
            TaskEnvironment? taskEnvironment,
#if FEATURE_APPDOMAIN
            AppDomainSetup appDomainSetup,
            Action<AppDomain> appDomainCreated,
#endif
            bool isOutOfProc
#if FEATURE_APPDOMAIN
            , out AppDomain? taskAppDomain
#endif
            )
#pragma warning restore SA1111, SA1009 // Closing parenthesis should be on line of last parameter
        {
#if FEATURE_APPDOMAIN
            bool separateAppDomain = loadedType.HasLoadInSeparateAppDomainAttribute;
            s_resolverLoadedType = null;
            taskAppDomain = null;
            ITask? taskInstanceInOtherAppDomain = null;
#endif

            try
            {
#if FEATURE_APPDOMAIN
                if (separateAppDomain)
                {
                    if (!loadedType.IsMarshalByRef)
                    {
                        logError(
                            taskLocation,
                            taskLine,
                            taskColumn,
                            "TaskNotMarshalByRef",
                            taskName);

                        return null;
                    }
                    else
                    {
                        taskAppDomain = CreateTaskAppDomain(loadedType, appDomainSetup, appDomainCreated, isOutOfProc);
                    }
                }
                else
#endif
                {
                    // perf improvement for the same appdomain case - we already have the type object
                    // and don't want to go through reflection to recreate it from the name.
                    // LoadedType owns instantiation: it invokes the TaskEnvironment-accepting constructor when
                    // the task declares one (so the task can compute environment-dependent defaults during
                    // construction) or the parameterless one otherwise, through a cached, Native AOT friendly
                    // mechanism. The engine still assigns the TaskEnvironment property separately afterward.
                    return loadedType.CreateInstance(taskEnvironment);
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
                        logError(
                        taskLocation,
                        taskLine,
                        taskColumn,
                        "ConflictingTaskAssembly",
                        loadedType.Assembly.AssemblyFile,
                        loadedType.Type.Assembly.Location);

                        taskInstanceInOtherAppDomain = null;
                    }
                }
                else
                {
                    taskInstanceInOtherAppDomain = (ITask)taskAppDomain.CreateInstanceAndUnwrap(loadedType.Type.Assembly.FullName, loadedType.Type.FullName);
                }

                return  taskInstanceInOtherAppDomain;
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
        /// Creates the AppDomain a [LoadInSeparateAppDomain] task will run in. Kept in a separate
        /// never-inlined method: the AppDomain.CreateDomain overload used here has
        /// System.Security.Policy.Evidence in its signature, a type that does not exist when a .NET
        /// host loads this .NET Framework assembly, and an unresolvable signature fails the JIT of
        /// the entire calling method. Isolating it here keeps <see cref="CreateTask"/> JITtable on
        /// .NET so the (overwhelmingly common) same-AppDomain path still works there.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static AppDomain CreateTaskAppDomain(LoadedType loadedType, AppDomainSetup appDomainSetup, Action<AppDomain> appDomainCreated, bool isOutOfProc)
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

            AppDomain taskAppDomain = AppDomain.CreateDomain(isOutOfProc ? "taskAppDomain (out-of-proc)" : "taskAppDomain (in-proc)", null, appDomainInfo);

            if (loadedType.LoadedAssembly != null)
            {
                taskAppDomain.Load(loadedType.LoadedAssemblyName);
            }

            // Hook up last minute dumping of any exceptions
            taskAppDomain.UnhandledException += DebugUtils.UnhandledExceptionHandler;
            appDomainCreated?.Invoke(taskAppDomain);

            return taskAppDomain;
        }

        /// <summary>
        /// This is a resolver to help created AppDomains when they are unable to load an assembly into their domain we will help
        /// them succeed by providing the already loaded one in the currentdomain so that they can derive AssemblyName info from it
        /// </summary>
        internal static Assembly? AssemblyResolver(object sender, ResolveEventArgs args)
        {
            if (args.Name.Equals(s_resolverLoadedType?.LoadedAssemblyName?.FullName, StringComparison.OrdinalIgnoreCase))
            {
                if (s_resolverLoadedType == null || s_resolverLoadedType.Path == null)
                {
                    return null;
                }
                return s_resolverLoadedType.LoadedAssembly ?? Assembly.Load(s_resolverLoadedType.Path);
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

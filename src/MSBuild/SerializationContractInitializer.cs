// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Experimental.ProjectCache;
using Microsoft.Build.Framework;

namespace Microsoft.Build.CommandLine
{
    internal static class SerializationContractInitializer
    {
        internal static void RegisterExcpetions()
        {
            Assembly microsoftDotBuildAssembly = typeof(BuildAbortedException).Assembly;

            typeof(InternalLoggerException).Assembly.GetType("Microsoft.Build.BackEnd.SdkResolution.SdkResolverException", throwOnError: true);

            Microsoft.Build.BackEnd.BuildExceptionSerializationHelper.InitializeSerializationContract(
                typeof(GenericBuildTransferredException),
                typeof(SdkResolverException),
                typeof(BuildAbortedException),
                typeof(CircularDependencyException),
                typeof(InternalLoggerException),
                typeof(InvalidProjectFileException),
                typeof(InvalidToolsetDefinitionException),
                typeof(ProjectCacheException),
                typeof(InternalErrorException),
                typeof(LoggerException),
                microsoftDotBuildAssembly.GetType("Microsoft.Build.BackEnd.NodeFailedToLaunchException", throwOnError: true)!,
                microsoftDotBuildAssembly.GetType("Microsoft.Build.BackEnd.SchedulerCircularDependencyException", throwOnError: true)!,
                microsoftDotBuildAssembly.GetType("Microsoft.Build.Exceptions.RegistryException", throwOnError: true)!,
                microsoftDotBuildAssembly.GetType("Microsoft.Build.Execution.HostObjectException", throwOnError: true)!,
                microsoftDotBuildAssembly.GetType("Microsoft.Build.Internal.UnbuildableProjectTypeException", throwOnError: true)!);
        }
    }
}

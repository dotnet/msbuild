// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.ProjectCache;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.BuildException;
using Microsoft.Build.Internal;

namespace Microsoft.Build.BackEnd
{
    internal static class SerializationContractInitializer
    {
        public static void Initialize()
        {
            RegisterExceptions();
            // reserved for future usage - BuildEventArgs, etc.
        }

        private static void RegisterExceptions()
        {
            // Any exception not contained int this list will be transferred as a GenericBuildTransferredException
            BuildExceptionSerializationHelper.InitializeSerializationContract(
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
                typeof(NodeFailedToLaunchException),
                typeof(SchedulerCircularDependencyException),
                typeof(RegistryException),
                typeof(HostObjectException),
                typeof(UnbuildableProjectTypeException));
        }
    }
}

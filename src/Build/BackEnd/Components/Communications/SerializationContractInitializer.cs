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
                new(typeof(GenericBuildTransferredException), (msg, inner) => new GenericBuildTransferredException(msg, inner)),
                new(typeof(SdkResolverException), (msg, inner) => new SdkResolverException(msg, inner)),
                new(typeof(BuildAbortedException), BuildAbortedException.CreateFromRemote),
                new(typeof(CircularDependencyException), (msg, inner) => new CircularDependencyException(msg, inner)),
                new(typeof(InternalLoggerException), (msg, inner) => new InternalLoggerException(msg, inner)),
                new(typeof(InvalidProjectFileException), (msg, inner) => new InvalidProjectFileException(msg, inner)),
                new(typeof(InvalidToolsetDefinitionException), (msg, inner) => new InvalidToolsetDefinitionException(msg, inner)),
                new(typeof(ProjectCacheException), (msg, inner) => new ProjectCacheException(msg, inner)),
                new(typeof(InternalErrorException), InternalErrorException.CreateFromRemote),
                new(typeof(LoggerException), (msg, inner) => new LoggerException(msg, inner)),
                new(typeof(NodeFailedToLaunchException), (msg, inner) => new NodeFailedToLaunchException(msg, inner)),
                new(typeof(SchedulerCircularDependencyException), (msg, inner) => new SchedulerCircularDependencyException(msg, inner)),
                new(typeof(RegistryException), (msg, inner) => new RegistryException(msg, inner)),
                new(typeof(HostObjectException), (msg, inner) => new HostObjectException(msg, inner)),
                new(typeof(UnbuildableProjectTypeException), (msg, inner) => new UnbuildableProjectTypeException(msg, inner)));
        }
    }
}

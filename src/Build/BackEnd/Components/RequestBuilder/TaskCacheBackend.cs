// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Build.Shared;
#if FEATURE_BUILDXL_TASK_CACHE
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Build.Framework;
#endif

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Build-scoped storage for opaque manifests and immutable file content.
/// </summary>
internal abstract class TaskCacheBackend : IDisposable
{
    internal static bool IsSupported =>
#if FEATURE_BUILDXL_TASK_CACHE
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64;
#else
        false;
#endif

    internal static TaskCacheBackend Create(string directory, CancellationToken cancellationToken = default)
    {
#if FEATURE_BUILDXL_TASK_CACHE
        if (FeatureSwitches.EnableReflectiveTaskExecution)
        {
            return CreateOptionalBackend(directory, cancellationToken);
        }
#endif
        throw new NotSupportedException(ResourceUtilities.GetResourceString("TaskCache.StorageUnavailable"));
    }

#if FEATURE_BUILDXL_TASK_CACHE
    [RequiresUnreferencedCode("Loads the optional, untrimmed task cache runtime by its fixed assembly and type names.")]
    private static TaskCacheBackend CreateOptionalBackend(string directory, CancellationToken cancellationToken)
    {
        try
        {
            Type type = Type.GetType("Microsoft.Build.BackEnd.BuildXLTaskCacheBackend, Microsoft.Build.TaskCache", throwOnError: true)!;
            return (TaskCacheBackend)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, args: [directory, 10240u, cancellationToken], culture: CultureInfo.InvariantCulture)!;
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
        catch (FileNotFoundException e)
        {
            throw new IOException(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TaskCache.RuntimeUnavailable"), e);
        }
    }
#endif

    protected static bool IsCriticalException(Exception exception) => Microsoft.Build.Framework.ExceptionHandling.IsCriticalException(exception);

    protected static string FormatOwnershipFailure(string directory) =>
        ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TaskCache.OwnershipUnavailable", directory);

    internal abstract byte[]? ReadManifest(string key, int maximumSize, CancellationToken cancellationToken = default);

    internal abstract Stream Open(string digest, CancellationToken cancellationToken = default);

    internal abstract string ContentPath(string digest);

    internal abstract string PutFile(string path, CancellationToken cancellationToken = default);

    internal abstract bool Publish(string key, byte[] manifest, Func<bool>? canPublish = null,
        IReadOnlyList<string>? artifacts = null, CancellationToken cancellationToken = default);

    public abstract void Dispose();
}

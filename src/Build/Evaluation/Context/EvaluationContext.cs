// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Evaluation.Context
{
    /// <summary>
    ///     An object used by the caller to extend the lifespan of evaluation caches (by passing the object on to other
    ///     evaluations).
    ///     The caller should throw away the context when the environment changes (IO, environment variables, SDK resolution
    ///     inputs, etc).
    ///     This class and it's closure needs to be thread safe since API users can do evaluations in parallel.
    /// </summary>
    public class EvaluationContext
    {
        public enum SharingPolicy
        {
            Shared,
            Isolated
        }

        internal static Action<EvaluationContext> TestOnlyHookOnCreate { get; set; }

        private int _used;

        internal SharingPolicy Policy { get; }

        internal ISdkResolverService SdkResolverService { get; }
        internal IFileSystem FileSystem { get; }
        internal EngineFileUtilities EngineFileUtilities { get; }

        /// <summary>
        /// Key to file entry list. Example usages: cache glob expansion and intermediary directory expansions during glob expansion.
        /// </summary>
        internal ConcurrentDictionary<string, ImmutableArray<string>> FileEntryExpansionCache { get; }

        internal EvaluationContext(SharingPolicy policy)
        {
            Policy = policy;

            SdkResolverService = new CachingSdkResolverService();
            FileEntryExpansionCache = new ConcurrentDictionary<string, ImmutableArray<string>>();
            FileSystem = new CachingFileSystemWrapper(FileSystems.Default);
            EngineFileUtilities = new EngineFileUtilities(new FileMatcher(FileSystem, FileEntryExpansionCache));
        }

        /// <summary>
        ///     Factory for <see cref="EvaluationContext" />
        /// </summary>
        public static EvaluationContext Create(SharingPolicy policy)
        {
            var context = new EvaluationContext(policy);
            TestOnlyHookOnCreate?.Invoke(context);

            return context;
        }

        private EvaluationContext CreateUsedIsolatedContext()
        {
            var context = Create(SharingPolicy.Isolated);
            context._used = 1;

            return context;
        }

        internal EvaluationContext ContextForNewProject()
        {
            // Projects using isolated contexts need to get a new context instance 
            switch (Policy)
            {
                case SharingPolicy.Shared:
                    return this;
                case SharingPolicy.Isolated:
                    // reuse the first isolated context if it has not seen an evaluation yet.
                    var previousValueWasUsed = Interlocked.CompareExchange(ref _used, 1, 0);
                    return previousValueWasUsed == 0
                        ? this
                        : CreateUsedIsolatedContext();
                default:
                    ErrorUtilities.ThrowInternalErrorUnreachable();
                    return null;
            }
        }
    }
}

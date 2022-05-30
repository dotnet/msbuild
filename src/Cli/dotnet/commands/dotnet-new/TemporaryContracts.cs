// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.DotNet.Tools.New
{
    //////////////////////////////////////////////////////////
    //
    // TO BE REMOVED BEFORE MERGE
    //   Those types will be pulled from Microsoft.TemplateEngine.Abstractions, once the TemplateEngine PR gets merged
    //     and package reference version in SDK gets bumped up
    //
    /////////////////////////////////////////////////////////


    /// <summary>
    /// Provider of descriptors of SDK workloads available to particular host (that is usually providing this component).
    /// </summary>
    public interface IWorkloadsInfoProvider : IIdentifiedComponent
    {
        /// <summary>
        /// Fetches set of installed workloads.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>Set of installed workloads.</returns>
        public Task<IEnumerable<WorkloadInfo>> GetInstalledWorkloadsAsync(CancellationToken token);

        /// <summary>
        /// Provides localized suggestion on action to be taken so that constraints requiring specified workloads can be met.
        /// This should be specific for current host (e.g. action to be taken for VS will differ from CLI host action.)
        /// This method should not perform any heavy processing (external services or file system queries) - as it's being
        ///   synchronously executed as part of constraint evaluation.
        /// </summary>
        /// <param name="supportedWorkloads">Workloads required by a constraint (in an 'OR' relationship).</param>
        /// <returns>Localized string with remedy suggestion specific to current host.</returns>
        public string ProvideConstraintRemedySuggestion(IReadOnlyList<string> supportedWorkloads);
    }

    /// <summary>
    /// Provider of SDK installation info.
    /// </summary>
    public interface ISdkInfoProvider : IIdentifiedComponent
    {
        /// <summary>
        /// Current SDK installation semver version string.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>SDK version.</returns>
        public Task<string> GetCurrentVersionAsync(CancellationToken cancellationToken);

        /// <summary>
        /// All installed SDK installations semver version strings.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>SDK version strings.</returns>
        public Task<IEnumerable<string>> GetInstalledVersionsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Provides localized suggestion on action to be taken so that constraints requiring specified workloads can be met.
        /// This should be specific for current host (e.g. action to be taken for VS will differ from CLI host action.)
        /// This method should not perform any heavy processing (external services or file system queries) - as it's being
        ///   synchronously executed as part of constraint evaluation.
        /// </summary>
        /// <param name="supportedVersions">SDK versions required by a constraint (in an 'OR' relationship).</param>
        /// <param name="viableInstalledVersions">SDK versions installed, that can meet the constraint - instructions should be provided to switch to any of those.</param>
        /// <returns>Localized string with remedy suggestion specific to current host.</returns>
        public string ProvideConstraintRemedySuggestion(IReadOnlyList<string> supportedVersions, IReadOnlyList<string> viableInstalledVersions);
    }

    /// <summary>
    /// SDK workload descriptor.
    /// Analogous to SDK type Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver.WorkloadInfo.
    /// </summary>
    public class WorkloadInfo
    {
        /// <summary>
        /// Creates new instance of <see cref="WorkloadInfo"/>.
        /// </summary>
        /// <param name="id">Workload identifier.</param>
        /// <param name="description">Workload description string - expected to be localized.</param>
        public WorkloadInfo(string id, string description)
        {
            Id = id;
            Description = description;
        }

        /// <summary>
        /// Workload identifier (from manifest).
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Workload description string - expected to be localized.
        /// </summary>
        public string Description { get; }
    }

    public static class EnumerableExtensions
    {
        /// <summary>
        /// Concatenates items of input sequence into csv string.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Sequence to be turned into csv string.</param>
        /// <param name="useSpace">Indicates whether space should be inserted between comas and following items.</param>
        /// <returns>Csv string.</returns>
        public static string ToCsvString<T>(this IEnumerable<T> source, bool useSpace = true)
        {
            return source == null ? "<NULL>" : string.Join("," + (useSpace ? " " : string.Empty), source);
        }
    }
}

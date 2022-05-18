// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    internal interface IWorkloadsInfoProvider : IIdentifiedComponent
    {
        /// <summary>
        /// Set of installed workloads.
        /// </summary>
        public IEnumerable<WorkloadInfo> InstalledWorkloads { get; }
    }

    /// <summary>
    /// Provider of SDK installation info.
    /// </summary>
    public interface ISdkInfoProvider : IIdentifiedComponent
    {
        /// <summary>
        /// Current SDK installation semver version string.
        /// </summary>
        public string VersionString { get; }
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
}

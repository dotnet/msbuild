// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.DotNet.PackageValidation
{
    public class GetLastStablePackage : TaskBase
    {
        [Required]
        public string PackageId { get; set; }

        public string PackageVersion { get; set; }

        public string[] NugetFeeds { get; set; }

        [Output]
        public string LastStableVersion { get; set; }

        protected override void ExecuteCore()
        {
            NuGetVersion currentPackageVersion = new NuGetVersion(PackageVersion);
            NuGetVersion version = null;
            foreach (string nugetFeed in NugetFeeds)
            {
                SourceRepository repository = Repository.Factory.GetCoreV3(nugetFeed);
                FindPackageByIdResource resource = repository.GetResource<FindPackageByIdResource>();
                SourceCacheContext cache = new SourceCacheContext();
                IEnumerable<NuGetVersion> versions = resource.GetAllVersionsAsync(PackageId, cache, NullLogger.Instance, CancellationToken.None).Result;

                NuGetVersion packageVersion = versions?.Where(t => !t.IsPrerelease && t != currentPackageVersion).OrderByDescending(t => t.Version).FirstOrDefault();

                if (packageVersion != null)
                {
                    if ((version == null || packageVersion > version) && packageVersion.Version < currentPackageVersion.Version)
                    {
                        version = packageVersion;
                    }
                }
            }
            
            if (version == null)
            {
                throw new Exception(string.Format(Resources.BaselinePackageNotFound, PackageId, Environment.NewLine + string.Join(Environment.NewLine + " - ", NugetFeeds)));
            }

            LastStableVersion = version?.ToNormalizedString();
        }
    }
}

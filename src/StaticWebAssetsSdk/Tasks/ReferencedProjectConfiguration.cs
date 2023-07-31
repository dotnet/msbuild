// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public partial class StaticWebAssetsManifest
    {
        public class ReferencedProjectConfiguration
        {
            internal static ReferencedProjectConfiguration Create(string identity, string source)
            {
                return new ReferencedProjectConfiguration()
                {
                    Identity = identity,
                    Source = source
                };
            }

            public string Identity { get; set; }

            public int Version { get; set; }

            public string Source { get; set; }

            public string GetPublishAssetsTargets { get; set; }

            public string AdditionalPublishProperties { get; set; }

            public string AdditionalPublishPropertiesToRemove { get; set; }

            public string GetBuildAssetsTargets { get; set; }

            public string AdditionalBuildProperties { get; set; }

            public string AdditionalBuildPropertiesToRemove { get; set; }

            public override bool Equals(object obj) => obj is ReferencedProjectConfiguration reference
                && Identity == reference.Identity
                && Version == reference.Version
                && Source == reference.Source
                && GetBuildAssetsTargets == reference.GetBuildAssetsTargets
                && AdditionalBuildProperties == reference.AdditionalBuildProperties
                && AdditionalBuildPropertiesToRemove == reference.AdditionalBuildPropertiesToRemove
                && GetPublishAssetsTargets == reference.GetPublishAssetsTargets
                && AdditionalPublishProperties == reference.AdditionalPublishProperties
                && AdditionalPublishPropertiesToRemove == reference.AdditionalPublishPropertiesToRemove;

            public override int GetHashCode()
            {
#if NET6_0_OR_GREATER
                var hashCode = new HashCode();
                hashCode.Add(Identity);
                hashCode.Add(Version);
                hashCode.Add(Source);
                hashCode.Add(GetBuildAssetsTargets);
                hashCode.Add(AdditionalBuildProperties);
                hashCode.Add(AdditionalBuildPropertiesToRemove);
                hashCode.Add(GetPublishAssetsTargets);
                hashCode.Add(AdditionalPublishProperties);
                hashCode.Add(AdditionalPublishPropertiesToRemove);

                return hashCode.ToHashCode();
#else
                int hashCode = -868952447;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Identity);
                hashCode = hashCode * -1521134295 + EqualityComparer<int>.Default.GetHashCode(Version);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Source);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(GetBuildAssetsTargets);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AdditionalBuildProperties);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AdditionalBuildPropertiesToRemove);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(GetPublishAssetsTargets);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AdditionalPublishProperties);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AdditionalPublishPropertiesToRemove);
                return hashCode;
#endif
            }

            public ITaskItem ToTaskItem()
            {
                var result = new TaskItem(Identity);

                result.SetMetadata(nameof(Version), Version.ToString(CultureInfo.InvariantCulture));
                result.SetMetadata(nameof(Source), Source);
                result.SetMetadata(nameof(GetBuildAssetsTargets), GetBuildAssetsTargets);
                result.SetMetadata(nameof(AdditionalBuildProperties), AdditionalBuildProperties);
                result.SetMetadata(nameof(AdditionalBuildPropertiesToRemove), AdditionalBuildPropertiesToRemove);
                result.SetMetadata(nameof(GetPublishAssetsTargets), GetPublishAssetsTargets);
                result.SetMetadata(nameof(AdditionalPublishProperties), AdditionalPublishProperties);
                result.SetMetadata(nameof(AdditionalPublishPropertiesToRemove), AdditionalPublishPropertiesToRemove);

                return result;
            }

            internal static ReferencedProjectConfiguration FromTaskItem(ITaskItem arg)
            {
                var result = new ReferencedProjectConfiguration();

                result.Identity = arg.GetMetadata("FullPath");
                result.Version = int.Parse(arg.GetMetadata(nameof(Version)), CultureInfo.InvariantCulture);
                result.Source = arg.GetMetadata(nameof(Source));
                result.GetBuildAssetsTargets = arg.GetMetadata(nameof(GetBuildAssetsTargets));
                result.AdditionalBuildProperties = arg.GetMetadata(nameof(AdditionalBuildProperties));
                result.AdditionalBuildPropertiesToRemove = arg.GetMetadata(nameof(AdditionalBuildPropertiesToRemove));
                result.GetPublishAssetsTargets = arg.GetMetadata(nameof(GetPublishAssetsTargets));
                result.AdditionalPublishProperties = arg.GetMetadata(nameof(AdditionalPublishProperties));
                result.AdditionalPublishPropertiesToRemove = arg.GetMetadata(nameof(AdditionalPublishPropertiesToRemove));

                return result;
            }
        }
    }
}

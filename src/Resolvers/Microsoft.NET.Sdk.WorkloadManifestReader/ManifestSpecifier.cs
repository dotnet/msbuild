// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public record class ManifestSpecifier(ManifestId Id, ManifestVersion Version, SdkFeatureBand FeatureBand)
    {
        public override string ToString() => $"{Id}: {Version}/{FeatureBand}";
    }
}



//  Add attribute to support init-only properties on .NET Framework
#if !NET
namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}
#endif

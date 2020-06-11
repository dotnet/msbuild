// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System.Collections.Generic;

using SdkReference = Microsoft.Build.Framework.SdkReference;
using SdkResultBase = Microsoft.Build.Framework.SdkResult;
using SdkResultFactoryBase = Microsoft.Build.Framework.SdkResultFactory;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// An internal implementation of <see cref="Microsoft.Build.Framework.SdkResultFactory"/>.
    /// </summary>
    internal class SdkResultFactory : SdkResultFactoryBase
    {
        private readonly SdkReference _sdkReference;

        internal SdkResultFactory(SdkReference sdkReference)
        {
            _sdkReference = sdkReference;
        }

        public override SdkResultBase IndicateFailure(IEnumerable<string> errors, IEnumerable<string> warnings = null)
        {
            return new SdkResult(_sdkReference, errors, warnings);
        }

        public override SdkResultBase IndicateSuccess(string path, string version, IEnumerable<string> warnings = null)
        {
            return new SdkResult(_sdkReference, path, version, warnings);
        }

        public override SdkResultBase IndicateSuccess(IEnumerable<string> paths,
                                                      string version,
                                                      IDictionary<string, string> propertiesToAdd = null,
                                                      IDictionary<string, SdkResultItem> itemsToAdd = null,
                                                      IEnumerable<string> warnings = null)
        {
            return new SdkResult(_sdkReference, paths, version, propertiesToAdd, itemsToAdd, warnings);
        }
    }
}

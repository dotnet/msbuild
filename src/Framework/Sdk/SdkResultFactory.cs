// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    ///     An abstract interface class provided to <see cref="SdkResolver" /> to create an
    ///     <see cref="SdkResult" /> object indicating success / failure.
    /// </summary>
    public abstract class SdkResultFactory
    {
        /// <summary>
        ///     Create an <see cref="SdkResolver" /> object indicating success resolving the SDK.
        /// </summary>
        /// <param name="path">Path to the SDK.</param>
        /// <param name="version">Version of the SDK that was resolved.</param>
        /// <param name="warnings">Optional warnings to display during resolution.</param>
        /// <returns></returns>
        public abstract SdkResult IndicateSuccess(string path, string version, IEnumerable<string> warnings = null);

        /// <summary>
        ///     Create an <see cref="SdkResolver" /> object indicating success resolving the SDK.
        /// </summary>
        /// <param name="path">Path to the SDK.</param>
        /// <param name="version">Version of the SDK that was resolved.</param>
        /// <param name="propertiesToAdd">Properties to set in the evaluation</param>
        /// <param name="itemsToAdd">Items to add to the evaluation</param>
        /// <param name="warnings">Optional warnings to display during resolution.</param>
        /// <returns></returns>
        public virtual SdkResult IndicateSuccess(string path,
            string version,
            IDictionary<string, string> propertiesToAdd,
            IDictionary<string, SdkResultItem> itemsToAdd,
            IEnumerable<string> warnings = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Create an <see cref="SdkResolver" /> object indicating success.
        /// </summary>
        /// <remarks>
        /// This overload allows any number (zero, one, or many) of SDK paths to be returned.  This means a "successful" result
        /// may not resolve to any SDKs.  The resolver can also supply properties or items to communicate information to the build.  This
        /// can allow resolvers to report SDKs that could not be resolved without hard-failing the evaluation, which can allow other
        /// components to take more appropriate action (for example installing optional workloads or downloading NuGet SDKs).
        /// </remarks>
        /// <param name="paths">SDK paths which should be imported</param>
        /// <param name="propertiesToAdd">Properties to set in the evaluation</param>
        /// <param name="itemsToAdd">Items to add to the evaluation</param>
        /// <param name="warnings">Optional warnings to display during resolution.</param>
        /// <returns></returns>
        public virtual SdkResult IndicateSuccess(IEnumerable<string> paths,
            string version,
            IDictionary<string, string> propertiesToAdd = null,
            IDictionary<string, SdkResultItem> itemsToAdd = null,
            IEnumerable<string> warnings = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Create an <see cref="SdkResolver" /> object indicating failure resolving the SDK.
        /// </summary>
        /// <param name="errors">
        ///     Errors / reasons the SDK could not be resolved. Will be logged as a
        ///     build error if no other SdkResolvers were able to indicate success.
        /// </param>
        /// <param name="warnings"></param>
        /// <returns></returns>
        public abstract SdkResult IndicateFailure(IEnumerable<string> errors, IEnumerable<string> warnings = null);
    }
}

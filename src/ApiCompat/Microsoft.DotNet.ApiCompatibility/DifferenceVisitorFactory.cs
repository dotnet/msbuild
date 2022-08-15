// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Factory to create an IDifferenceVisitor instance.
    /// </summary>
    public interface IDifferenceVisitorFactory
    {
        /// <summary>
        /// Creates an IDifferenceVisitor with an optionally provided count of the right elements that are compared. 
        /// </summary>
        /// <param name="rightCount">The number of rights that are compared against a left.</param>
        /// <returns></returns>
        IDifferenceVisitor Create(int rightCount = 1);
    }

    /// <summary>
    /// Factory to create an DifferenceVisitor instance.
    /// </summary>
    public sealed class DifferenceVisitorFactory : IDifferenceVisitorFactory
    {
        /// <inheritdoc />
        public IDifferenceVisitor Create(int rightCount = 1) => new DifferenceVisitor(rightCount);
    }
}

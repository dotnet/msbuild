// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// An interface representing an object which can provide properties to the Expander.
    /// </summary>
    /// <typeparam name="T">The type of properties provided.</typeparam>
    internal interface IPropertyProvider<T> where T : class
    {
        /// <summary>
        /// Returns a property with the specified name, or null if it was not found.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <returns>The property.</returns>
        T GetProperty(string name);

        /// <summary>
        /// Returns a property with the specified name, or null if it was not found.
        /// Name is the segment of the provided string with the provided start and end indexes.
        /// </summary>
        T GetProperty(string name, int startIndex, int endIndex);
    }
}

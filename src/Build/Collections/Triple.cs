// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A struct of three objects</summary>
//-----------------------------------------------------------------------

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// A struct containing three objects
    /// </summary>
    /// <typeparam name="A">Type of first object</typeparam>
    /// <typeparam name="B">Type of second object</typeparam>
    /// <typeparam name="C">Type of third object</typeparam>
    internal struct Triple<A, B, C>
    {

        /// <summary>
        /// Constructor
        /// </summary>
        public Triple(A first, B second, C third)
        {
            First = first;
            Second = second;
            Third = third;
        }

        /// <summary>
        /// First
        /// </summary>
        public A First { get; }

        /// <summary>
        /// Second
        /// </summary>
        public B Second { get; }

        /// <summary>
        /// Third
        /// </summary>
        public C Third { get; }
    }
}

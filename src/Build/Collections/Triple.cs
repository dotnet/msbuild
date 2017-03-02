// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A struct of three objects</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;

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
        /// First 
        /// </summary>
        private A _first;

        /// <summary>
        /// Second
        /// </summary>
        private B _second;

        /// <summary>
        /// Third
        /// </summary>
        private C _third;

        /// <summary>
        /// Constructor
        /// </summary>
        public Triple(A first, B second, C third)
        {
            _first = first;
            _second = second;
            _third = third;
        }

        /// <summary>
        /// First
        /// </summary>
        public A First
        {
            get { return _first; }
        }

        /// <summary>
        /// Second
        /// </summary>
        public B Second
        {
            get { return _second; }
        }

        /// <summary>
        /// Third
        /// </summary>
        public C Third
        {
            get { return _third; }
        }
    }
}
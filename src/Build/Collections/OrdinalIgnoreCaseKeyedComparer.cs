// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>
// Equality comparer for IKeyed objects that uses Ordinal comparison.
// </summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Equality comparer for IKeyed objects that uses Ordinal comparison.
    /// </summary>
    [Serializable]
    internal class OrdinalIgnoreCaseKeyedComparer : IEqualityComparer<IKeyed>
    {
        /// <summary>
        /// The one instance
        /// </summary>
        private static OrdinalIgnoreCaseKeyedComparer s_instance = new OrdinalIgnoreCaseKeyedComparer();

        /// <summary>
        /// Only create myself
        /// </summary>
        private OrdinalIgnoreCaseKeyedComparer()
        {
        }

        /// <summary>
        /// The one instance
        /// </summary>
        internal static OrdinalIgnoreCaseKeyedComparer Instance
        {
            get { return s_instance; }
        }

        /// <summary>
        /// Performs the "Equals" operation Ordinally
        /// </summary>
        public bool Equals(IKeyed one, IKeyed two)
        {
            return String.Equals(one.Key, two.Key, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get hash
        /// </summary>
        public int GetHashCode(IKeyed item)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(item.Key);
        }
    }
}

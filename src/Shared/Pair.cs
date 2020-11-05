// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This struct is functionally identical to KeyValuePair, but avoids
    /// CA908 warnings (types that in ngen images that will JIT).
    /// Instead of generic collections of KeyValuePair, use Pair.
    /// </summary>
    /// <comment>
    /// This trick is based on advice from 
    /// http://sharepoint/sites/codeanalysis/Wiki%20Pages/Rule%20-%20Avoid%20Types%20That%20Require%20JIT%20Compilation%20In%20Precompiled%20Assemblies.aspx.
    /// It works because although this is a value type, it is not defined in mscorlib.
    /// </comment>
    /// <typeparam name="TKey">Key</typeparam>
    /// <typeparam name="TValue">Value</typeparam>
    [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Justification = "Not possible as Equals cannot be implemented on the struct members")]
    internal struct Pair<TKey, TValue>
    {
        /// <summary>
        /// Key
        /// </summary>
        private TKey _key;

        /// <summary>
        /// Value
        /// </summary>
        private TValue _value;

        /// <summary>
        /// Constructor
        /// </summary>
        public Pair(TKey key, TValue value)
        {
            _key = key;
            _value = value;
        }

        /// <summary>
        /// Key
        /// </summary>
        internal TKey Key
        {
            get { return _key; }
        }

        /// <summary>
        /// Value
        /// </summary>
        internal TValue Value
        {
            get { return _value; }
        }
    }
}
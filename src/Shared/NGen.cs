// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// To avoid CA908 warnings (types that in ngen images that will JIT)
    /// wrap each problematic value type in the collection in 
    /// one of these objects.
    /// </summary>
    /// <comment>
    /// This trick is based on advice from 
    /// http://sharepoint/sites/codeanalysis/Wiki%20Pages/Rule%20-%20Avoid%20Types%20That%20Require%20JIT%20Compilation%20In%20Precompiled%20Assemblies.aspx.
    /// It works because although this is a value type, it is not defined in mscorlib.
    /// </comment>
    /// <typeparam name="T">Wrapped type</typeparam>
    internal struct NGen<T> where T : struct
    {
        /// <summary>
        /// Wrapped value
        /// </summary>
        private T _value;

        /// <summary>
        /// Constructor
        /// </summary>
        public NGen(T value)
        {
            _value = value;
        }

        /// <summary>
        /// Exposes the value
        /// </summary>
        public static implicit operator T(NGen<T> value)
        {
            return value._value;
        }

        /// <summary>
        /// Consumes the value
        /// </summary>
        public static implicit operator NGen<T>(T value)
        {
            return new NGen<T>(value);
        }
    }
}
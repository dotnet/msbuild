// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Description of a local type for a debugger local.</summary>
//-----------------------------------------------------------------------

#if FEATURE_MSBUILD_DEBUGGER

using System;
using Microsoft.Build.Shared;
using System.Diagnostics;

namespace Microsoft.Build.Debugging
{
    /// <summary>
    /// Immutable class to describe the name and type for an early bound local
    /// </summary>
#if JMC
    [DebuggerNonUserCode]
#endif
    internal struct DebuggerLocalType
    {
        /// <summary>
        /// Name of the local variable.
        /// </summary>
        private string _name;

        /// <summary>
        /// Type of the local variable.
        /// </summary>
        private Type _type;

        /// <summary>
        /// Constructor 
        /// </summary>
        internal DebuggerLocalType(string name, Type type)
        {
            ErrorUtilities.VerifyThrowInternalLength(name, "name");
            ErrorUtilities.VerifyThrowInternalNull(type, "type");

            _name = name;
            _type = type;
        }

        /// <summary>
        /// Name of the local variable.
        /// </summary>
        internal string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Type of the local variable.
        /// </summary>
        internal Type Type
        {
            get { return _type; }
        }
    }
}
#endif

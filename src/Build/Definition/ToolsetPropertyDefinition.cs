// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using ErrorUtilities = Microsoft.Build.Shared.ErrorUtilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// A class representing a property.  Used internally by the toolset readers.
    /// </summary>
    [DebuggerDisplay("Name={Name} Value={Value}")]
    internal class ToolsetPropertyDefinition
    {
        /// <summary>
        /// The property name
        /// </summary>
        private string _name;

        /// <summary>
        /// The property value
        /// </summary>
        private string _value;

        /// <summary>
        /// The property source
        /// </summary>
        private IElementLocation _source;

        /// <summary>
        /// Creates a new property
        /// </summary>
        /// <param name="name">The property name</param>
        /// <param name="value">The property value</param>
        /// <param name="source">The property source</param>
        public ToolsetPropertyDefinition(string name, string value, IElementLocation source)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, nameof(name));
            ErrorUtilities.VerifyThrowArgumentNull(source, nameof(source));

            // value can be the empty string but not null
            ErrorUtilities.VerifyThrowArgumentNull(value, nameof(value));

            _name = name;
            _value = value;
            _source = source;
        }

        /// <summary>
        /// The name of the property
        /// </summary>
        public string Name
        {
            get
            {
                return _name;
            }
        }

        /// <summary>
        /// The value of the property
        /// </summary>
        public string Value
        {
            get
            {
                return _value;
            }

            set
            {
                ErrorUtilities.VerifyThrowInternalNull(value, "Value");
                _value = value;
            }
        }

        /// <summary>
        /// A description of the location where the property was defined,
        /// such as a registry key path or a path to a config file and 
        /// line number.
        /// </summary>
        public IElementLocation Source
        {
            get
            {
                return _source;
            }
        }
    }
}

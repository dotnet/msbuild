// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary> Property description class for the XamlTaskFactory parser. </summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;

namespace Microsoft.Build.Tasks.Xaml
{
    /// <summary>
    /// The type of value this property takes.
    /// </summary>
    internal enum PropertyType
    {
        /// <summary>
        /// The property has no value type specified
        /// </summary>
        None,

        /// <summary>
        /// The property takes values of type Boolean
        /// </summary>
        Boolean,

        /// <summary>
        /// The property takes values of type String
        /// </summary>
        String,

        /// <summary>
        /// The property takes values of type Integer
        /// </summary>
        Integer,

        /// <summary>
        /// The property takes values of type String[]
        /// </summary>
        StringArray,

        /// <summary>
        /// The property takes values of type ITaskItem[]
        /// </summary>
        ItemArray
    }

    /// <summary>
    /// The class Property holds information about the properties
    /// for each task
    /// </summary>
    internal class Property
    {
        /// <summary>
        /// The property name
        /// </summary>
        private string _name = String.Empty;

        /// <summary>
        /// The property type
        /// </summary>
        private PropertyType _type = PropertyType.None;

        /// <summary>
        /// "true" if the property should itself be emitted on the command line.
        /// </summary>
        private bool _includeInCommandLine;

        /// <summary>
        /// "true" if the property is a reversible switch
        /// </summary>
        private string _reversible = String.Empty;

        /// <summary>
        /// The switch name
        /// </summary>
        private string _switchName = String.Empty;

        /// <summary>
        /// The reverse switch name.
        /// </summary>
        private string _reverseSwitchName = String.Empty;

        /// <summary>
        /// The false suffix.
        /// </summary>
        private string _falseSuffix = String.Empty;

        /// <summary>
        /// The true suffix
        /// </summary>
        private string _trueSuffix = String.Empty;

        /// <summary>
        /// The min value for an integer property.
        /// </summary>
        private string _max = String.Empty;

        /// <summary>
        /// The max value for an integer property.
        /// </summary>
        private string _min = String.Empty;

        /// <summary>
        /// The separator between the switch and its value.
        /// </summary>
        private string _separator = String.Empty;

        /// <summary>
        /// The default value for the property.
        /// </summary>
        private string _defaultValue = String.Empty;

        /// <summary>
        /// The argument from which the switch should derive its value.
        /// </summary>
        private string _argument = String.Empty;

        /// <summary>
        /// The fallback argument if the preferred argument is not specified.
        /// </summary>
        private string _fallback = String.Empty;

        /// <summary>
        /// "true" if the property is required.
        /// </summary>
        private string _required = String.Empty;

        /// <summary>
        /// True if this property is an output property.
        /// </summary>
        private bool _output = false;

        /// <summary>
        /// The prefix for thw property.
        /// </summary>
        private string _prefix = null;

        /// <summary>
        /// The property category
        /// </summary>
        private string _category = String.Empty;

        /// <summary>
        /// The display name.
        /// </summary>
        private string _displayName = String.Empty;

        /// <summary>
        /// The description.
        /// </summary>
        private string _description = String.Empty;

        /// <summary>
        /// The parents of this property.
        /// </summary>
        private LinkedList<string> _parents = new LinkedList<string>();

        /// <summary>
        /// The dependencies.
        /// </summary>
        private LinkedList<Property> _dependencies = new LinkedList<Property>();

        /// <summary>
        /// The values allowed for this property, if it is an enum.
        /// </summary>
        private List<Value> _values = new List<Value>();

        /// <summary>
        /// The arguments which can provide values to this property.
        /// </summary>
        private List<Argument> _arguments = new List<Argument>();

        /// <summary>
        /// Default constructor
        /// </summary>
        public Property()
        {
            // does nothing
        }

        #region Properties

        /// <summary>
        /// The type of the switch, i.e., boolean, stringarray, etc.
        /// </summary>
        public PropertyType Type
        {
            get
            {
                return _type;
            }

            set
            {
                _type = value;
            }
        }

        /// <summary>
        /// Specifies if the property should be included on the command line.
        /// </summary>
        public bool IncludeInCommandLine
        {
            get
            {
                return _includeInCommandLine;
            }

            set
            {
                _includeInCommandLine = value;
            }
        }

        /// <summary>
        /// Specifies whether the switch is reversible (has a false suffix) or not
        /// </summary>
        public string Reversible
        {
            get
            {
                return _reversible;
            }

            set
            {
                _reversible = value;
            }
        }

        /// <summary>
        /// The name of the switch, without the / in front of it
        /// i.e., Od for the Optimization property
        /// </summary>
        public string SwitchName
        {
            get
            {
                return _switchName;
            }

            set
            {
                _switchName = value;
            }
        }

        /// <summary>
        /// The name of the reverse switch, without the / in front of it
        /// </summary>
        public string ReverseSwitchName
        {
            get
            {
                return _reverseSwitchName;
            }

            set
            {
                _reverseSwitchName = value;
            }
        }

        /// <summary>
        /// The flag to append at the end of a switch when the switch is set to false
        /// i.e., for all CL switches that are reversible, the FalseSuffix is "-"
        /// </summary>
        public string FalseSuffix
        {
            get
            {
                return _falseSuffix;
            }

            set
            {
                _falseSuffix = value;
            }
        }

        /// <summary>
        /// The flag to append to the end of the switch when that switch is true
        /// i.e., In the OptimizeForWindows98, the switch is OPT, the FalseSuffix is
        /// :NOWIN98, and the TrueSuffix is :WIN98
        /// </summary>
        public string TrueSuffix
        {
            get
            {
                return _trueSuffix;
            }

            set
            {
                _trueSuffix = value;
            }
        }

        /// <summary>
        /// The max integer value an integer typed switch can have
        /// An exception should be thrown in the number the user specifies is 
        /// larger than the max
        /// </summary>
        public string Max
        {
            get
            {
                return _max;
            }

            set
            {
                _max = value;
            }
        }

        /// <summary>
        /// The minimum integer value an integer typed switch can have
        /// An exception should be thrown in the number the user specifies is 
        /// less than the minimum
        /// </summary>
        public string Min
        {
            get
            {
                return _min;
            }

            set
            {
                _min = value;
            }
        }

        /// <summary>
        /// The separator indicates the characters that go between the switch and the string
        /// in the string typed case, the characters that go between each name for the 
        /// string array case, or the characters that go between the switch and the 
        /// appendage for the boolean case.
        /// </summary>
        public string Separator
        {
            get
            {
                return _separator;
            }

            set
            {
                _separator = value;
            }
        }

        /// <summary>
        /// The default value for the switch to have (in the case of reversibles, true
        /// or false, in the case of files, a default file name)
        /// </summary>
        public string DefaultValue
        {
            get
            {
                return _defaultValue;
            }

            set
            {
                _defaultValue = value;
            }
        }

        /// <summary>
        /// The argument specifies which property to look for when appending a
        /// file name, and that property contains the actual file name.
        /// i.e., UsePrecompiledHeader has the argument "PrecompiledHeaderThrough"
        /// and the values "CreateUsingSpecific", "GenerateAuto", and "UseUsingSpecific"
        /// that have the switches /Yc, /YX, and /Yu.
        /// If PrecompiledHeaderThrough has the value "myfile", then the emitted switch
        /// would be /Ycmyfile, /YXmyfile, or /Yumyfile
        /// </summary>
        public string Argument
        {
            get
            {
                return _argument;
            }

            set
            {
                _argument = value;
            }
        }

        /// <summary>
        /// The Fallback attribute is used to specify which property to look at in the
        /// case that the argument property is not set, or if the file that the 
        /// argument property indicates is nonexistent.
        /// </summary>
        public string Fallback
        {
            get
            {
                return _fallback;
            }

            set
            {
                _fallback = value;
            }
        }

        /// <summary>
        /// This property whether or not the property is required in the project file
        /// </summary>
        public string Required
        {
            get
            {
                return _required;
            }

            set
            {
                _required = value;
            }
        }

        /// <summary>
        /// This property indicates whether the property is an output, i.e., object files
        /// </summary>
        public bool Output
        {
            get
            {
                return _output;
            }

            set
            {
                _output = value;
            }
        }

        /// <summary>
        /// The name of the property this one is dependent on.
        /// </summary>
        public LinkedList<string> Parents
        {
            get
            {
                return _parents;
            }
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

            set
            {
                _name = value;
            }
        }

        /// <summary>
        /// The list of switches that are dependent with this one.
        /// </summary>
        public LinkedList<Property> DependentArgumentProperties
        {
            get
            {
                return _dependencies;
            }
        }

        /// <summary>
        /// The different choices for each property, and the corresponding switch
        /// </summary>
        public List<Value> Values
        {
            get
            {
                return _values;
            }
        }

        /// <summary>
        /// The prefix for each switch.
        /// </summary>
        public string Prefix
        {
            get
            {
                return _prefix;
            }

            set
            {
                _prefix = value;
            }
        }

        /// <summary>
        /// The Category for each switch.
        /// </summary>
        public string Category
        {
            get
            {
                return _category;
            }

            set
            {
                _category = value;
            }
        }

        /// <summary>
        /// The Display Name for each switch.
        /// </summary>
        public string DisplayName
        {
            get
            {
                return _displayName;
            }

            set
            {
                _displayName = value;
            }
        }

        /// <summary>
        /// The Description for each switch.
        /// </summary>
        public string Description
        {
            get
            {
                return _description;
            }

            set
            {
                _description = value;
            }
        }

        /// <summary>
        /// The arguments which apply to this property.
        /// </summary>
        public List<Argument> Arguments
        {
            get
            {
                return _arguments;
            }

            set
            {
                _arguments = value;
            }
        }

        #endregion

        /// <summary>
        /// creates a new Property with the exact same information as this one
        /// </summary>
        public Property Clone()
        {
            Property cloned = new Property();
            cloned.Type = _type;
            cloned.SwitchName = _switchName;
            cloned.ReverseSwitchName = _reverseSwitchName;
            cloned.FalseSuffix = _falseSuffix;
            cloned.TrueSuffix = _trueSuffix;
            cloned.Max = _max;
            cloned.Min = _min;
            cloned.Separator = _separator;
            cloned.DefaultValue = _defaultValue;
            cloned.Argument = _argument;
            cloned.Fallback = _fallback;
            cloned.Required = _required;
            cloned.Output = _output;
            cloned.Reversible = _reversible;
            cloned.Name = _name;
            cloned.Prefix = _prefix;
            return cloned;
        }
    }

    /// <summary>
    /// An enum value.
    /// </summary>
    internal class Value
    {
        /// <summary>
        /// The name of the value.
        /// </summary>
        private string _name = String.Empty;

        /// <summary>
        /// The switch name when this value is specified.
        /// </summary>
        private string _switchName = String.Empty;

        /// <summary>
        /// The reverse switch name.
        /// </summary>
        private string _reverseSwitchName = String.Empty;

        /// <summary>
        /// The description of the value.
        /// </summary>
        private string _description = String.Empty;

        /// <summary>
        /// The display name of the value.
        /// </summary>
        private string _displayName = String.Empty;

        /// <summary>
        /// The prefix.
        /// </summary>
        private string _prefix = null;

        /// <summary>
        /// The arguments for this value.
        /// </summary>
        private List<Argument> _arguments = new List<Argument>();

        /// <summary>
        /// The name of the property
        /// </summary>
        public string Name
        {
            get
            {
                return _name;
            }

            set
            {
                _name = value;
            }
        }

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public string SwitchName
        {
            get
            {
                return _switchName;
            }

            set
            {
                _switchName = value;
            }
        }

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public string ReverseSwitchName
        {
            get
            {
                return _reverseSwitchName;
            }

            set
            {
                _reverseSwitchName = value;
            }
        }

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public string Description
        {
            get
            {
                return _description;
            }

            set
            {
                _description = value;
            }
        }

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public string DisplayName
        {
            get
            {
                return _displayName;
            }

            set
            {
                _displayName = value;
            }
        }

        /// <summary>
        /// The prefix for each switch.
        /// </summary>
        public string Prefix
        {
            get
            {
                return _prefix;
            }

            set
            {
                _prefix = value;
            }
        }

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public List<Argument> Arguments
        {
            get
            {
                return _arguments;
            }

            set
            {
                _arguments = value;
            }
        }
    }

    /// <summary>
    /// An argument for the property.
    /// </summary>
    internal class Argument
    {
        /// <summary>
        /// The parameter to which the argument refers.
        /// </summary>
        private string _parameter = String.Empty;

        /// <summary>
        /// The argument value separator.
        /// </summary>
        private string _separator = String.Empty;

        /// <summary>
        /// True if the argument is required.
        /// </summary>
        private bool _required = false;

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public string Parameter
        {
            get
            {
                return _parameter;
            }

            set
            {
                _parameter = value;
            }
        }

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public string Separator
        {
            get
            {
                return _separator;
            }

            set
            {
                _separator = value;
            }
        }

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public bool Required
        {
            get
            {
                return _required;
            }

            set
            {
                _required = value;
            }
        }
    }
}

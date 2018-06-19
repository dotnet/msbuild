// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary> Property description class for the XamlTaskFactory parser. </summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;

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
        #region Properties

        /// <summary>
        /// The type of the switch, i.e., boolean, stringarray, etc.
        /// </summary>
        public PropertyType Type { get; set; } = PropertyType.None;

        /// <summary>
        /// Specifies if the property should be included on the command line.
        /// </summary>
        public bool IncludeInCommandLine { get; set; }

        /// <summary>
        /// Specifies whether the switch is reversible (has a false suffix) or not
        /// </summary>
        public string Reversible { get; set; } = String.Empty;

        /// <summary>
        /// The name of the switch, without the / in front of it
        /// i.e., Od for the Optimization property
        /// </summary>
        public string SwitchName { get; set; } = String.Empty;

        /// <summary>
        /// The name of the reverse switch, without the / in front of it
        /// </summary>
        public string ReverseSwitchName { get; set; } = String.Empty;

        /// <summary>
        /// The flag to append at the end of a switch when the switch is set to false
        /// i.e., for all CL switches that are reversible, the FalseSuffix is "-"
        /// </summary>
        public string FalseSuffix { get; set; } = String.Empty;

        /// <summary>
        /// The flag to append to the end of the switch when that switch is true
        /// i.e., In the OptimizeForWindows98, the switch is OPT, the FalseSuffix is
        /// :NOWIN98, and the TrueSuffix is :WIN98
        /// </summary>
        public string TrueSuffix { get; set; } = String.Empty;

        /// <summary>
        /// The max integer value an integer typed switch can have
        /// An exception should be thrown in the number the user specifies is 
        /// larger than the max
        /// </summary>
        public string Max { get; set; } = String.Empty;

        /// <summary>
        /// The minimum integer value an integer typed switch can have
        /// An exception should be thrown in the number the user specifies is 
        /// less than the minimum
        /// </summary>
        public string Min { get; set; } = String.Empty;

        /// <summary>
        /// The separator indicates the characters that go between the switch and the string
        /// in the string typed case, the characters that go between each name for the 
        /// string array case, or the characters that go between the switch and the 
        /// appendage for the boolean case.
        /// </summary>
        public string Separator { get; set; } = String.Empty;

        /// <summary>
        /// The default value for the switch to have (in the case of reversibles, true
        /// or false, in the case of files, a default file name)
        /// </summary>
        public string DefaultValue { get; set; } = String.Empty;

        /// <summary>
        /// The argument specifies which property to look for when appending a
        /// file name, and that property contains the actual file name.
        /// i.e., UsePrecompiledHeader has the argument "PrecompiledHeaderThrough"
        /// and the values "CreateUsingSpecific", "GenerateAuto", and "UseUsingSpecific"
        /// that have the switches /Yc, /YX, and /Yu.
        /// If PrecompiledHeaderThrough has the value "myfile", then the emitted switch
        /// would be /Ycmyfile, /YXmyfile, or /Yumyfile
        /// </summary>
        public string Argument { get; set; } = String.Empty;

        /// <summary>
        /// The Fallback attribute is used to specify which property to look at in the
        /// case that the argument property is not set, or if the file that the 
        /// argument property indicates is nonexistent.
        /// </summary>
        public string Fallback { get; set; } = String.Empty;

        /// <summary>
        /// This property whether or not the property is required in the project file
        /// </summary>
        public string Required { get; set; } = String.Empty;

        /// <summary>
        /// This property indicates whether the property is an output, i.e., object files
        /// </summary>
        public bool Output { get; set; }

        /// <summary>
        /// The name of the property this one is dependent on.
        /// </summary>
        public LinkedList<string> Parents { get; } = new LinkedList<string>();

        /// <summary>
        /// The name of the property
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// The list of switches that are dependent with this one.
        /// </summary>
        public LinkedList<Property> DependentArgumentProperties { get; } = new LinkedList<Property>();

        /// <summary>
        /// The different choices for each property, and the corresponding switch
        /// </summary>
        public List<Value> Values { get; } = new List<Value>();

        /// <summary>
        /// The prefix for each switch.
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// The Category for each switch.
        /// </summary>
        public string Category { get; set; } = String.Empty;

        /// <summary>
        /// The Display Name for each switch.
        /// </summary>
        public string DisplayName { get; set; } = String.Empty;

        /// <summary>
        /// The Description for each switch.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// The arguments which apply to this property.
        /// </summary>
        public List<Argument> Arguments { get; set; } = new List<Argument>();

        #endregion

        /// <summary>
        /// creates a new Property with the exact same information as this one
        /// </summary>
        public Property Clone()
        {
            var cloned = new Property
            {
                Type = Type,
                SwitchName = SwitchName,
                ReverseSwitchName = ReverseSwitchName,
                FalseSuffix = FalseSuffix,
                TrueSuffix = TrueSuffix,
                Max = Max,
                Min = Min,
                Separator = Separator,
                DefaultValue = DefaultValue,
                Argument = Argument,
                Fallback = Fallback,
                Required = Required,
                Output = Output,
                Reversible = Reversible,
                Name = Name,
                Prefix = Prefix
            };
            return cloned;
        }
    }

    /// <summary>
    /// An enum value.
    /// </summary>
    internal class Value
    {
        /// <summary>
        /// The name of the property
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public string SwitchName { get; set; } = String.Empty;

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public string ReverseSwitchName { get; set; } = String.Empty;

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public string DisplayName { get; set; } = String.Empty;

        /// <summary>
        /// The prefix for each switch.
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public List<Argument> Arguments { get; set; } = new List<Argument>();
    }

    /// <summary>
    /// An argument for the property.
    /// </summary>
    internal class Argument
    {
        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public string Parameter { get; set; } = String.Empty;

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public string Separator { get; set; } = String.Empty;

        /// <summary>
        /// The switch Name of the property
        /// </summary>
        public bool Required { get; set; }
    }
}

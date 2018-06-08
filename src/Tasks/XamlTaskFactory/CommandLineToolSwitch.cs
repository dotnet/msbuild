// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary> Tool switch description class for DataDriven tasks. </summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.Xaml
{
    /// <summary>
    /// This enumeration specifies the different types for each switch in a tool
    /// The types are used in the documentation
    /// </summary>
    public enum CommandLineToolSwitchType
    {
        /// <summary>
        /// The boolean type has a boolean value, and there are types: one that can have a flag appended on the end
        /// and one that can't
        /// e.g. GlobalOptimizations = "true" would be /Og, and GlobalOptimizations="false" would be /Og-, but
        /// WarnAsError = "true" would be /WX, while WarnAsError = "false" would be nothing.
        /// </summary>
        Boolean = 0,

        /// <summary>
        /// The integer switch is used for properties that have several different integer values,
        /// and depending on the value the property is set to, appends an integer to the end 
        /// of a certain switch
        /// e.g. WarningLevel = "0" is /W0, WarningLevel = "2" is /W2
        /// </summary>
        Integer = 1,

        /// <summary>
        /// The string switch is used for two kinds of properties.
        /// The first is the kind that has multiple values, and has a different switch for each value
        /// e.g. Optimization="disabled" is /Od, "Full" is /Ox
        /// The second is the kind that has a literal string appended to the end of a switch.
        /// This type is similar to the File type, but in this case, will never get quoted.
        /// </summary>
        String = 2,

        /// <summary>
        /// The stringarray switch is used for properties that may have more 
        /// than one string appended to the end of the switch
        /// e.g. InjectPrecompiledHeaderReference = myfile is /Ylmyfile
        /// </summary>
        StringArray = 3,

        /// <summary>
        /// The ITaskItemArray type is used for properties that pass multiple files, but
        /// want to keep the metadata. Otherwise, it is used in the same way as a StringArray type.
        /// </summary>
        ITaskItemArray = 4,
    }

    /// <summary>
    /// The class CommandLineToolSwitch holds information about the properties
    /// for each task
    /// </summary>
    public class CommandLineToolSwitch
    {
        #region Constant strings

        /// <summary>
        /// Boolean switch type
        /// </summary>
        private const string TypeBoolean = "CommandLineToolSwitchType.Boolean";

        /// <summary>
        /// Integer switch type
        /// </summary>
        private const string TypeInteger = "CommandLineToolSwitchType.Integer";

        /// <summary>
        /// ITaskItemArray switch type.
        /// </summary>
        private const string TypeITaskItemArray = "CommandLineToolSwitchType.ITaskItemArray";

        /// <summary>
        /// String array switch type.
        /// </summary>
        private const string TypeStringArray = "CommandLineToolSwitchType.StringArray";

        #endregion

        /// <summary>
        /// The value for a boolean switch.
        /// </summary>
        private bool _booleanValue = true;

        /// <summary>
        /// The value for the integer type.
        /// </summary>
        private int _number;

        /// <summary>
        /// The list of strings for a string array.
        /// </summary>
        private string[] _stringList;

        /// <summary>
        /// The list of task items for ITaskItemArray types.
        /// </summary>
        private ITaskItem[] _taskItemArray;

        /// <summary>
        /// The default constructor creates a new CommandLineToolSwitch to hold the name of
        /// the tool, the attributes, the dependent switches, and the values (if they exist)
        /// </summary>
        public CommandLineToolSwitch()
        {
            // does nothing
        }

        /// <summary>
        /// Overloaded constructor. Takes a CommandLineToolSwitchType and sets the type.
        /// </summary>
        public CommandLineToolSwitch(CommandLineToolSwitchType toolType)
        {
            Type = toolType;
        }

        #region Properties

        /// <summary>
        /// The name of the parameter
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Specifies if this switch should be included on the command-line.
        /// </summary>
        public bool IncludeInCommandLine { get; set; }

        /// <summary>
        /// The Value of the parameter
        /// </summary>
        public string Value { get; set; } = String.Empty;

        /// <summary>
        /// Flag indicating if the switch is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// The SwitchValue of the parameter
        /// </summary>
        public string SwitchValue { get; set; } = String.Empty;

        /// <summary>
        /// The SwitchValue of the parameter
        /// </summary>
        public string ReverseSwitchValue { get; set; } = String.Empty;

        /// <summary>
        /// The arguments.
        /// </summary>
        public ICollection<Tuple<string, bool>> Arguments { get; set; }

        /// <summary>
        /// The DisplayName of the parameter
        /// </summary>
        public string DisplayName { get; set; } = String.Empty;

        /// <summary>
        /// The Description of the parameter
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// The type of the switch, i.e., boolean, string, stringarray, etc.
        /// </summary>
        public CommandLineToolSwitchType Type { get; set; }

        /// <summary>
        /// Indicates whether or not the switch is emitted with a flag when false
        /// </summary>
        public bool Reversible { get; set; }

        /// <summary>
        /// True if multiple values are allowed.
        /// </summary>
        public bool AllowMultipleValues { get; set; }

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
        /// The separator indicates the characters that go between the switch and the string
        /// in the string typed case, the characters that go between each name for the 
        /// string array case, or the characters that go between the switch and the 
        /// appendage for the boolean case.
        /// </summary>
        public string Separator { get; set; } = String.Empty;

        /// <summary>
        /// The Fallback attribute is used to specify which property to look at in the
        /// case that the argument property is not set, or if the file that the 
        /// argument property indicates is nonexistent.
        /// </summary>
        public string FallbackArgumentParameter { get; set; } = String.Empty;

        /// <summary>
        /// This attribute specifies whether or not an argument attribute is required.
        /// </summary>
        public bool ArgumentRequired { get; set; }

        /// <summary>
        /// This property indicates whether or not the property is required in the project file
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// This property indicates the parent of the dependency
        /// </summary>
        public LinkedList<string> Parents { get; } = new LinkedList<string>();

        /// <summary>
        /// This property indicates the parent of the dependency
        /// </summary>
        public LinkedList<KeyValuePair<string, string>> Overrides { get; } = new LinkedList<KeyValuePair<string, string>>();

        /// <summary>
        /// The BooleanValue is used for the boolean switches, and are set to true
        /// or false, depending on what you set it to.
        /// </summary>
        public bool BooleanValue
        {
            get
            {
                ErrorUtilities.VerifyThrow(Type == CommandLineToolSwitchType.Boolean, "InvalidType", TypeBoolean);
                return _booleanValue;
            }

            set
            {
                ErrorUtilities.VerifyThrow(Type == CommandLineToolSwitchType.Boolean, "InvalidType", TypeBoolean);
                _booleanValue = value;
            }
        }

        /// <summary>
        /// The number is the number you wish to append to the end of integer switches
        /// </summary>
        public int Number
        {
            get
            {
                ErrorUtilities.VerifyThrow(Type == CommandLineToolSwitchType.Integer, "InvalidType", TypeInteger);
                return _number;
            }

            set
            {
                ErrorUtilities.VerifyThrow(Type == CommandLineToolSwitchType.Integer, "InvalidType", TypeInteger);
                _number = value;
            }
        }

        /// <summary>
        /// Returns the set of inputs to a switch
        /// </summary>
        /// <returns></returns>
        public string[] StringList
        {
            get
            {
                ErrorUtilities.VerifyThrow(Type == CommandLineToolSwitchType.StringArray, "InvalidType", TypeStringArray);
                return _stringList;
            }

            set
            {
                ErrorUtilities.VerifyThrow(Type == CommandLineToolSwitchType.StringArray, "InvalidType", TypeStringArray);
                _stringList = value;
            }
        }

        /// <summary>
        /// Returns the set of inputs to a switch that is a set of ITaskItems
        /// </summary>
        /// <returns></returns>
        public ITaskItem[] TaskItemArray
        {
            get
            {
                ErrorUtilities.VerifyThrow(Type == CommandLineToolSwitchType.ITaskItemArray, "InvalidType", TypeITaskItemArray);
                return _taskItemArray;
            }

            set
            {
                ErrorUtilities.VerifyThrow(Type == CommandLineToolSwitchType.ITaskItemArray, "InvalidType", TypeITaskItemArray);
                _taskItemArray = value;
            }
        }
        #endregion
    }

    /// <summary>
    /// Expresses a relationship between an argument and a property.
    /// </summary>
    public class PropertyRelation
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public PropertyRelation()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public PropertyRelation(string argument, string value, bool required)
        {
            Argument = argument;
            Value = value;
            Required = required;
        }

        /// <summary>
        /// The name of the argument
        /// </summary>
        public string Argument { get; set; }

        /// <summary>
        /// The value.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Flag indicating if the argument is required or not.
        /// </summary>
        public bool Required { get; set; }
    }

    /// <summary>
    /// Derived class indicating how to separate values from the specified argument.
    /// </summary>
    public class CommandLineArgumentRelation : PropertyRelation
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public CommandLineArgumentRelation(string argument, string value, bool required, string separator)
            : base(argument, value, required)
        {
            Separator = separator;
        }

        /// <summary>
        /// The separator.
        /// </summary>
        public string Separator { get; set; }
    }
}

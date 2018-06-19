// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>
// A helper class that generates a command line based on the
// specified switch descriptions and values.
// </summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.Xaml
{
    /// <summary>
    /// The list of active switches in the order they should be emitted.
    /// </summary>
    public class CommandLineGenerator
    {
        /// <summary>
        /// The list of active switches in the order they should be emitted.
        /// </summary>
        private readonly IEnumerable<string> _switchOrderList;

        /// <summary>
        /// The dictionary that holds all set switches
        /// The string is the name of the property, and the CommandLineToolSwitch holds all of the relevant information
        /// i.e., switch, boolean value, type, etc.
        /// </summary>
        private readonly Dictionary<string, CommandLineToolSwitch> _activeCommandLineToolSwitches = new Dictionary<string, CommandLineToolSwitch>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a generator that generates a command-line based on the specified Xaml file and parameters.
        /// </summary>
        public CommandLineGenerator(Rule rule, Dictionary<string, Object> parameterValues)
        {
            ErrorUtilities.VerifyThrowArgumentNull(rule, nameof(rule));
            ErrorUtilities.VerifyThrowArgumentNull(parameterValues, nameof(parameterValues));

            // Parse the Xaml file
            var parser = new TaskParser();
            bool success = parser.ParseXamlDocument(rule);
            ErrorUtilities.VerifyThrow(success, "Unable to parse specified file or contents.");

            // Generate the switch order list
            _switchOrderList = parser.SwitchOrderList;

            foreach (Property property in parser.Properties)
            {
                if (parameterValues.TryGetValue(property.Name, out object value))
                {
                    var switchToAdd = new CommandLineToolSwitch();
                    if (!String.IsNullOrEmpty(property.Reversible) && String.Equals(property.Reversible, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        switchToAdd.Reversible = true;
                    }

                    switchToAdd.IncludeInCommandLine = property.IncludeInCommandLine;
                    switchToAdd.Separator = property.Separator;
                    switchToAdd.DisplayName = property.DisplayName;
                    switchToAdd.Description = property.Description;
                    if (!String.IsNullOrEmpty(property.Required) && String.Equals(property.Required, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        switchToAdd.Required = true;
                    }

                    switchToAdd.FallbackArgumentParameter = property.Fallback;
                    switchToAdd.FalseSuffix = property.FalseSuffix;
                    switchToAdd.TrueSuffix = property.TrueSuffix;
                    if (!String.IsNullOrEmpty(property.SwitchName))
                    {
                        switchToAdd.SwitchValue = property.Prefix + property.SwitchName;
                    }

                    switchToAdd.IsValid = true;

                    // Based on the switch type, cast the value and set as appropriate
                    switch (property.Type)
                    {
                        case PropertyType.Boolean:
                            switchToAdd.Type = CommandLineToolSwitchType.Boolean;
                            switchToAdd.BooleanValue = (bool)value;
                            if (!String.IsNullOrEmpty(property.ReverseSwitchName))
                            {
                                switchToAdd.ReverseSwitchValue = property.Prefix + property.ReverseSwitchName;
                            }

                            break;

                        case PropertyType.Integer:
                            switchToAdd.Type = CommandLineToolSwitchType.Integer;
                            switchToAdd.Number = (int)value;
                            if (!String.IsNullOrEmpty(property.Min))
                            {
                                if (switchToAdd.Number < Convert.ToInt32(property.Min, System.Threading.Thread.CurrentThread.CurrentCulture))
                                {
                                    switchToAdd.IsValid = false;
                                }
                            }

                            if (!String.IsNullOrEmpty(property.Max))
                            {
                                if (switchToAdd.Number > Convert.ToInt32(property.Max, System.Threading.Thread.CurrentThread.CurrentCulture))
                                {
                                    switchToAdd.IsValid = false;
                                }
                            }

                            break;

                        case PropertyType.ItemArray:
                            switchToAdd.Type = CommandLineToolSwitchType.ITaskItemArray;
                            switchToAdd.TaskItemArray = (ITaskItem[])value;
                            break;

                        case PropertyType.None:
                            break;

                        case PropertyType.String:
                            switchToAdd.Type = CommandLineToolSwitchType.String;
                            switchToAdd.ReverseSwitchValue = property.Prefix + property.ReverseSwitchName;
                            if (property.Values.Count > 0)
                            {
                                string enumValueToSelect = (string)value;

                                switchToAdd.Value = (string)value;
                                switchToAdd.AllowMultipleValues = true;

                                // Find the matching value in the enum
                                foreach (Value enumValue in property.Values)
                                {
                                    if (String.Equals(enumValue.Name, enumValueToSelect, StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (!String.IsNullOrEmpty(enumValue.SwitchName))
                                        {
                                            switchToAdd.SwitchValue = enumValue.Prefix + enumValue.SwitchName;
                                        }
                                        else
                                        {
                                            switchToAdd = null;
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                switchToAdd.Value = (string)value;
                            }

                            break;

                        case PropertyType.StringArray:
                            switchToAdd.Type = CommandLineToolSwitchType.StringArray;
                            switchToAdd.StringList = (string[])value;
                            break;
                    }

                    if (switchToAdd != null)
                    {
                        _activeCommandLineToolSwitches[property.Name] = switchToAdd;
                    }
                }
            }
        }

        /// <summary>
        /// Creates a generator that generates a command-line based on the specified Xaml file and parameters.
        /// </summary>
        internal CommandLineGenerator(Dictionary<string, CommandLineToolSwitch> activeCommandLineToolSwitches, IEnumerable<string> switchOrderList)
        {
            _activeCommandLineToolSwitches = activeCommandLineToolSwitches;
            _switchOrderList = switchOrderList;
        }

        /// <summary>
        /// Any additional options (as a literal string) that may have been specified in the project file
        /// </summary>
        public string AdditionalOptions { get; set; } = String.Empty;

        /// <summary>
        /// The template which, if set, will be used to govern formatting of the command line(s)
        /// </summary>
        public string CommandLineTemplate { get; set; }

        /// <summary>
        /// The string to append to the end of a non-templated commandline.
        /// </summary>
        public string AlwaysAppend { get; set; }

        /// <summary>
        /// Generate the command-line
        /// </summary>
        public string GenerateCommandLine()
        {
            var commandLineBuilder = new CommandLineBuilder(true /* quote hyphens */);

            if (!String.IsNullOrEmpty(CommandLineTemplate))
            {
                GenerateTemplatedCommandLine(commandLineBuilder);
            }
            else
            {
                GenerateStandardCommandLine(commandLineBuilder, false);
            }

            return commandLineBuilder.ToString();
        }

        /// <summary>
        /// Appends a literal string containing the verbatim contents of any
        /// "AdditionalOptions" parameter. This goes last on the command
        /// line in case it needs to cancel any earlier switch.
        /// Ideally this should never be needed because the MSBuild task model
        /// is to set properties, not raw switches
        /// </summary>
        internal void BuildAdditionalArgs(CommandLineBuilder cmdLine)
        {
            // We want additional options to be last so that this can always override other flags.
            if ((cmdLine != null) && !String.IsNullOrEmpty(AdditionalOptions))
            {
                cmdLine.AppendSwitch(AdditionalOptions);
            }
        }

        /// <summary>
        /// Generates a part of the command line depending on the type
        /// </summary>
        /// <remarks>Depending on the type of the switch, the switch is emitted with the proper values appended.
        /// e.g., File switches will append file names, directory switches will append filenames with "\" on the end</remarks>
        internal void GenerateCommandsAccordingToType(CommandLineBuilder clb, CommandLineToolSwitch commandLineToolSwitch, bool recursive)
        {
            // if this property has a parent skip printing it as it was printed as part of the parent prop printing
            if (commandLineToolSwitch.Parents.Count > 0 && !recursive)
            {
                return;
            }

            switch (commandLineToolSwitch.Type)
            {
                case CommandLineToolSwitchType.Boolean:
                    EmitBooleanSwitch(clb, commandLineToolSwitch);
                    break;
                case CommandLineToolSwitchType.String:
                    EmitStringSwitch(clb, commandLineToolSwitch);
                    break;
                case CommandLineToolSwitchType.StringArray:
                    EmitStringArraySwitch(clb, commandLineToolSwitch);
                    break;
                case CommandLineToolSwitchType.Integer:
                    EmitIntegerSwitch(clb, commandLineToolSwitch);
                    break;
                case CommandLineToolSwitchType.ITaskItemArray:
                    EmitTaskItemArraySwitch(clb, commandLineToolSwitch);
                    break;
                default:
                    // should never reach this point - if it does, there's a bug somewhere.
                    ErrorUtilities.VerifyThrow(false, "InternalError");
                    break;
            }
        }

        /// <summary>
        /// Verifies that the required args are present. This function throws if we have missing required args
        /// </summary>
        internal bool VerifyRequiredArgumentsArePresent(CommandLineToolSwitch property, bool throwOnError)
        {
            return true;
        }

        /// <summary>
        /// Verifies that the dependencies are present, and if the dependencies are present, or if the property
        /// doesn't have any dependencies, the switch gets emitted
        /// </summary>
        internal bool VerifyDependenciesArePresent(CommandLineToolSwitch property)
        {
            // check the dependency
            if (property.Parents.Count > 0)
            {
                // has a dependency, now check to see whether at least one parent is set
                // if it is set, add to the command line
                // otherwise, ignore it
                bool isSet = false;
                foreach (string parentName in property.Parents)
                {
                    isSet = isSet || HasSwitch(parentName);
                }

                return isSet;
            }
            else
            {
                // no dependencies to account for
                return true;
            }
        }

        /// <summary>
        /// Returns true if the property has a value in the list of active tool switches
        /// </summary>
        internal bool IsPropertySet(string propertyName)
        {
            if (!String.IsNullOrEmpty(propertyName))
            {
                return _activeCommandLineToolSwitches.ContainsKey(propertyName);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Checks to see if the switch name is empty
        /// </summary>
        internal bool HasSwitch(string propertyName)
        {
            if (IsPropertySet(propertyName))
            {
                return !String.IsNullOrEmpty(_activeCommandLineToolSwitches[propertyName].Name);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true if the property exists (regardless of whether it is 
        /// set or not) and false otherwise. 
        /// </summary>
        internal bool PropertyExists(string propertyName)
        {
            return _switchOrderList.Contains(propertyName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Emit a switch that's an array of task items
        /// </summary>
        private static void EmitTaskItemArraySwitch(CommandLineBuilder clb, CommandLineToolSwitch commandLineToolSwitch)
        {
            if (String.IsNullOrEmpty(commandLineToolSwitch.Separator))
            {
                foreach (ITaskItem itemName in commandLineToolSwitch.TaskItemArray)
                {
                    clb.AppendSwitchIfNotNull(commandLineToolSwitch.SwitchValue, itemName.ItemSpec);
                }
            }
            else
            {
                clb.AppendSwitchIfNotNull(commandLineToolSwitch.SwitchValue, commandLineToolSwitch.TaskItemArray, commandLineToolSwitch.Separator);
            }
        }

        /// <summary>
        /// Generates the commands for the switches that may have an array of arguments
        /// The switch may be empty.
        /// </summary>
        /// <remarks>For stringarray switches (e.g., Sources), the CommandLineToolSwitchName (if it exists) is emitted
        /// along with each and every one of the file names separately (if no separator is included), or with all of the
        /// file names separated by the separator.
        /// e.g., AdditionalIncludeDirectores = "@(Files)" where Files has File1, File2, and File3, the switch
        /// /IFile1 /IFile2 /IFile3 or the switch /IFile1;File2;File3 is emitted (the latter case has a separator
        /// ";" specified)</remarks>
        private static void EmitStringArraySwitch(CommandLineBuilder clb, CommandLineToolSwitch commandLineToolSwitch)
        {
            var stringList = new List<string>(commandLineToolSwitch.StringList.Length);
            for (int i = 0; i < commandLineToolSwitch.StringList.Length; ++i)
            {
                // Make sure the file doesn't contain escaped " (\")
                string value;
                if (commandLineToolSwitch.StringList[i].StartsWith("\"", StringComparison.OrdinalIgnoreCase) && commandLineToolSwitch.StringList[i].EndsWith("\"", StringComparison.OrdinalIgnoreCase))
                {
                    value = commandLineToolSwitch.StringList[i].Substring(1, commandLineToolSwitch.StringList[i].Length - 2).Trim();
                }
                else
                {
                    value = commandLineToolSwitch.StringList[i].Trim();
                }

                if (!String.IsNullOrEmpty(value))
                {
                    stringList.Add(value);
                }
            }

            string[] arrTrimStringList = stringList.ToArray();

            if (String.IsNullOrEmpty(commandLineToolSwitch.Separator))
            {
                foreach (string fileName in arrTrimStringList)
                {
                    if (!PerformSwitchValueSubstition(clb, commandLineToolSwitch, fileName))
                    {
                        clb.AppendSwitchIfNotNull(commandLineToolSwitch.SwitchValue, fileName);
                    }
                }
            }
            else
            {
                if (!PerformSwitchValueSubstition(clb, commandLineToolSwitch, String.Join(commandLineToolSwitch.Separator, arrTrimStringList)))
                {
                    clb.AppendSwitchIfNotNull(commandLineToolSwitch.SwitchValue, arrTrimStringList, commandLineToolSwitch.Separator);
                }
            }
        }

        /// <summary>
        /// Substitute the value for the switch into the switch value where the [value] string is found, if it exists.
        /// </summary>
        private static bool PerformSwitchValueSubstition(CommandLineBuilder clb, CommandLineToolSwitch commandLineToolSwitch, string switchValue)
        {
            Regex regex = new Regex(@"\[value]", RegexOptions.IgnoreCase);
            Match match = regex.Match(commandLineToolSwitch.SwitchValue);
            if (match.Success)
            {
                string prefixToAppend = commandLineToolSwitch.SwitchValue.Substring(match.Index + match.Length, commandLineToolSwitch.SwitchValue.Length - (match.Index + match.Length));
                string valueToAppend;
                if (!switchValue.EndsWith("\\\\", StringComparison.OrdinalIgnoreCase) && switchValue.EndsWith("\\", StringComparison.OrdinalIgnoreCase) && prefixToAppend.Length > 0 && prefixToAppend[0] == '\"')
                {
                    // If the combined string would create \" then we need to escape it
                    // if the combined string would create \\" then we ignore it as as assume it is already escaped.
                    valueToAppend = commandLineToolSwitch.SwitchValue.Substring(0, match.Index) + switchValue + "\\" + prefixToAppend;
                }
                else
                {
                    valueToAppend = commandLineToolSwitch.SwitchValue.Substring(0, match.Index) + switchValue + prefixToAppend;
                }

                clb.AppendSwitch(valueToAppend);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Generates the commands for switches that have integers appended.
        /// </summary>
        /// <remarks>For integer switches (e.g., WarningLevel), the CommandLineToolSwitchName is emitted
        /// with the appropriate integer appended, as well as any arguments
        /// e.g., WarningLevel = "4" will emit /W4</remarks>
        private static void EmitIntegerSwitch(CommandLineBuilder clb, CommandLineToolSwitch commandLineToolSwitch)
        {
            if (commandLineToolSwitch.IsValid)
            {
                string numberAsString = commandLineToolSwitch.Number.ToString(System.Threading.Thread.CurrentThread.CurrentCulture);
                if (PerformSwitchValueSubstition(clb, commandLineToolSwitch, numberAsString))
                {
                    return;
                }
                else if (!String.IsNullOrEmpty(commandLineToolSwitch.Separator))
                {
                    clb.AppendSwitch(commandLineToolSwitch.SwitchValue + commandLineToolSwitch.Separator + numberAsString);
                }
                else
                {
                    clb.AppendSwitch(commandLineToolSwitch.SwitchValue + numberAsString);
                }
            }
        }

        /// <summary>
        /// Generates the switches for switches that either have literal strings appended, or have
        /// different switches based on what the property is set to.
        /// </summary>
        /// <remarks>The string switch emits a switch that depends on what the parameter is set to, with and
        /// arguments
        /// e.g., Optimization = "Full" will emit /Ox, whereas Optimization = "Disabled" will emit /Od</remarks>
        private void EmitStringSwitch(CommandLineBuilder clb, CommandLineToolSwitch commandLineToolSwitch)
        {
            if (PerformSwitchValueSubstition(clb, commandLineToolSwitch, commandLineToolSwitch.Value))
            {
                return;
            }

            String strSwitch = String.Empty;
            strSwitch += commandLineToolSwitch.SwitchValue + commandLineToolSwitch.Separator;

            String str = commandLineToolSwitch.Value;
            if (!commandLineToolSwitch.AllowMultipleValues)
            {
                str = str.Trim();
                if (str.Contains(' '))
                {
                    if (!str.StartsWith("\"", StringComparison.OrdinalIgnoreCase))
                    {
                        str = "\"" + str;
                        if (str.EndsWith(@"\", StringComparison.OrdinalIgnoreCase) && !str.EndsWith(@"\\", StringComparison.OrdinalIgnoreCase))
                        {
                            str += "\\\"";
                        }
                        else
                        {
                            str += "\"";
                        }
                    }
                }
            }
            else
            {
                strSwitch = String.Empty;
                str = commandLineToolSwitch.SwitchValue;
                string arguments = GatherArguments(commandLineToolSwitch.Name, commandLineToolSwitch.Arguments, commandLineToolSwitch.Separator);
                if (!String.IsNullOrEmpty(arguments))
                {
                    str = str + commandLineToolSwitch.Separator + arguments;
                }
            }

            clb.AppendSwitchUnquotedIfNotNull(strSwitch, str);
        }

        /// <summary>
        /// Gets the arguments required by the specified switch and collects them into a string.
        /// </summary>
        private string GatherArguments(string parentSwitch, ICollection<Tuple<string, bool>> arguments, string separator)
        {
            string retVal = String.Empty;
            if (arguments != null)
            {
                foreach (Tuple<string, bool> arg in arguments)
                {
                    if (_activeCommandLineToolSwitches.TryGetValue(arg.Item1, out CommandLineToolSwitch argSwitch))
                    {
                        if (!String.IsNullOrEmpty(retVal))
                        {
                            retVal = retVal + separator;
                        }

                        retVal = retVal + argSwitch.Value;
                    }
                    else
                    {
                        if (arg.Item2)
                        {
                            throw new ArgumentException(ResourceUtilities.FormatResourceString("Xaml.MissingRequiredArgument", parentSwitch, arg.Item1));
                        }
                    }
                }
            }

            return retVal;
        }

        /// <summary>
        /// Generates the switches that are nonreversible
        /// </summary>
        /// <remarks>A boolean switch is emitted if it is set to true. If it set to false, nothing is emitted.
        /// e.g. nologo = "true" will emit /Og, but nologo = "false" will emit nothing.</remarks>
        private static void EmitBooleanSwitch(CommandLineBuilder clb, CommandLineToolSwitch commandLineToolSwitch)
        {
            if (commandLineToolSwitch.BooleanValue)
            {
                if (!String.IsNullOrEmpty(commandLineToolSwitch.SwitchValue))
                {
                    StringBuilder val = new StringBuilder();
                    val.Insert(0, commandLineToolSwitch.Separator);
                    val.Insert(0, commandLineToolSwitch.TrueSuffix);
                    val.Insert(0, commandLineToolSwitch.SwitchValue);
                    clb.AppendSwitch(val.ToString());
                }
            }
            else
            {
                EmitReversibleBooleanSwitch(clb, commandLineToolSwitch);
            }
        }

        /// <summary>
        /// Generates the command line for switches that are reversible
        /// </summary>
        /// <remarks>A reversible boolean switch will emit a certain switch if set to true, but emit that
        /// exact same switch with a flag appended on the end if set to false.
        /// e.g., GlobalOptimizations = "true" will emit /Og, and GlobalOptimizations = "false" will emit /Og-</remarks>
        private static void EmitReversibleBooleanSwitch(CommandLineBuilder clb, CommandLineToolSwitch commandLineToolSwitch)
        {
            // if the value is set to true, append whatever the TrueSuffix is set to.
            // Otherwise, append whatever the FalseSuffix is set to.
            if (!String.IsNullOrEmpty(commandLineToolSwitch.ReverseSwitchValue))
            {
                string suffix = (commandLineToolSwitch.BooleanValue) ? commandLineToolSwitch.TrueSuffix : commandLineToolSwitch.FalseSuffix;
                StringBuilder val = new StringBuilder();
                val.Insert(0, suffix);
                val.Insert(0, commandLineToolSwitch.Separator);
                val.Insert(0, commandLineToolSwitch.TrueSuffix);
                val.Insert(0, commandLineToolSwitch.ReverseSwitchValue);
                clb.AppendSwitch(val.ToString());
            }
        }

        /// <summary>
        /// Generates the command line using the standard algorithm.
        /// </summary>
        private void GenerateStandardCommandLine(CommandLineBuilder builder, bool allOptionsMode)
        {
            // iterates through the list of set CommandLineToolSwitches
            foreach (string propertyName in _switchOrderList)
            {
                if (IsPropertySet(propertyName))
                {
                    CommandLineToolSwitch property = _activeCommandLineToolSwitches[propertyName];

                    if (allOptionsMode)
                    {
                        if (property.Type == CommandLineToolSwitchType.ITaskItemArray)
                        {
                            // If we are in all-options mode, we will ignore any "switches" which are item arrays.
                            continue;
                        }
                        else if (String.Equals(propertyName, "AdditionalOptions", StringComparison.OrdinalIgnoreCase))
                        {
                            // If we are handling the [AllOptions], then skip the AdditionalOptions, which is handled later.
                            continue;
                        }
                    }

                    // verify the dependencies
                    if (property.IncludeInCommandLine && VerifyDependenciesArePresent(property) && VerifyRequiredArgumentsArePresent(property, false))
                    {
                        GenerateCommandsAccordingToType(builder, property, false);
                    }
                }
                else if (String.Equals(propertyName, "AlwaysAppend", StringComparison.OrdinalIgnoreCase))
                {
                    builder.AppendSwitch(AlwaysAppend);
                }
            }

            if (!allOptionsMode)
            {
                // additional args should go on the end
                BuildAdditionalArgs(builder);
            }
        }

        /// <summary>
        /// Generates the command-line using the template specified.
        /// </summary>
        private void GenerateTemplatedCommandLine(CommandLineBuilder builder)
        {
            // Match all instances of [asdf], where "asdf" can be any combination of any 
            // characters *except* a [ or an ]. i.e., if "[ [ sdf ]" is passed, then we will 
            // match "[ sdf ]"
            string matchString = @"\[[^\[\]]+\]";
            Regex regex = new Regex(matchString, RegexOptions.ECMAScript);
            MatchCollection matches = regex.Matches(CommandLineTemplate);

            int indexOfEndOfLastSubstitution = 0;
            foreach (Match match in matches)
            {
                if (match.Length == 0)
                {
                    continue;
                }

                // Because we match non-greedily, in the case where we have input such as "[[[[[foo]", the match will
                // be "[foo]".  However, if there are multiple '[' in a row, we need to do some escaping logic, so we 
                // want to know what the first *consecutive* square bracket was.  
                int indexOfFirstBracketInMatch = match.Index;

                // Indexing using "indexOfFirstBracketInMatch - 1" is safe here because it will always be 
                // greater than indexOfEndOfLastSubstitution, which will always be 0 or greater. 
                while (indexOfFirstBracketInMatch > indexOfEndOfLastSubstitution && CommandLineTemplate[indexOfFirstBracketInMatch - 1].Equals('['))
                {
                    indexOfFirstBracketInMatch--;
                }

                // Append everything we know we want to add -- everything between where the last substitution ended and 
                // this match (including previous '[' that were not initially technically part of the match) begins. 
                if (indexOfFirstBracketInMatch != indexOfEndOfLastSubstitution)
                {
                    builder.AppendTextUnquoted(CommandLineTemplate.Substring(indexOfEndOfLastSubstitution, indexOfFirstBracketInMatch - indexOfEndOfLastSubstitution));
                }

                // Now replace every "[[" with a literal '['.  We can do this by simply counting the number of '[' between 
                // the first one and the start of the match, since by definition everything in between is an '['.  
                // + 1 because match.Index is also a bracket. 
                int openBracketsInARow = match.Index - indexOfFirstBracketInMatch + 1;

                if (openBracketsInARow % 2 == 0)
                {
                    // even number -- they all go away and the rest of the match is appended literally. 
                    for (int i = 0; i < openBracketsInARow / 2; i++)
                    {
                        builder.AppendTextUnquoted("[");
                    }

                    builder.AppendTextUnquoted(match.Value.Substring(1, match.Value.Length - 1));
                }
                else
                {
                    // odd number -- all but one get merged two at a time, and the rest of the match is substituted. 
                    for (int i = 0; i < (openBracketsInARow - 1) / 2; i++)
                    {
                        builder.AppendTextUnquoted("[");
                    }

                    // Determine which property the user has specified in the template.
                    string propertyName = match.Value.Substring(1, match.Value.Length - 2);
                    if (String.Equals(propertyName, "AllOptions", StringComparison.OrdinalIgnoreCase))
                    {
                        // When [AllOptions] is specified, we append all switch-type options.
                        CommandLineBuilder tempBuilder = new CommandLineBuilder(true);
                        GenerateStandardCommandLine(tempBuilder, true);
                        builder.AppendTextUnquoted(tempBuilder.ToString());
                    }
                    else if (String.Equals(propertyName, "AdditionalOptions", StringComparison.OrdinalIgnoreCase))
                    {
                        BuildAdditionalArgs(builder);
                    }
                    else if (IsPropertySet(propertyName))
                    {
                        CommandLineToolSwitch property = _activeCommandLineToolSwitches[propertyName];

                        // verify the dependencies
                        if (VerifyDependenciesArePresent(property) && VerifyRequiredArgumentsArePresent(property, false))
                        {
                            CommandLineBuilder tempBuilder = new CommandLineBuilder(true);
                            GenerateCommandsAccordingToType(tempBuilder, property, false);
                            builder.AppendTextUnquoted(tempBuilder.ToString());
                        }
                    }
                    else if (!PropertyExists(propertyName))
                    {
                        // If the thing enclosed in square brackets is not in fact a property, we 
                        // don't want to replace it. 
                        builder.AppendTextUnquoted('[' + propertyName + ']');
                    }
                }

                indexOfEndOfLastSubstitution = match.Index + match.Length;
            }

            builder.AppendTextUnquoted(CommandLineTemplate.Substring(indexOfEndOfLastSubstitution, CommandLineTemplate.Length - indexOfEndOfLastSubstitution));
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Resources;
using System.Globalization;
using System.IO;
using System.Security;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Reflection;
using System.Diagnostics;

namespace Microsoft.Build.Tasks.DataDriven
{
    /// <summary>
    /// The top class that will take care of all the tasks that wrap tools.
    /// All tasks that wrap tools will derive from this class.
    /// Holds a Dictionary of all switches that have been set
    /// </summary>
    public abstract class DataDrivenToolTask : ToolTask
    {
        /// <summary>
        /// The dictionary that holds all set switches
        /// The string is the name of the property, and the ToolSwitch holds all of the relevant information
        /// i.e., switch, boolean value, type, etc.
        /// </summary>
        private Dictionary<string, ToolSwitch> activeToolSwitches = new Dictionary<string, ToolSwitch>();

        /// <summary>
        /// The dictionary holds all of the legal values that are associated with a certain switch.
        /// For example, the key Optimization would hold another dictionary as the value, that had the string pairs
        /// "Disabled", "/Od"; "MaxSpeed", "/O1"; "MinSpace", "/O2"; "Full", "/Ox" in it.
        /// </summary>
        private Dictionary<string, Dictionary<string, string>> values = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// Any additional options (as a literal string) that may have been specified in the project file
        /// We eventually want to get rid of this
        /// </summary>
        private string additionalOptions = String.Empty;

        /// <summary>
        /// The prefix to append before all switches
        /// </summary>
        private char prefix = '/';

        /// <summary>
        /// True if we returned our commands directly from the command line generation and do not need to use the
        /// response file (because the command-line is short enough)
        /// </summary>
        private bool skipResponseFileCommandGeneration;

        protected TaskLoggingHelper logPrivate;

        /// <summary>
        /// Default constructor
        /// </summary>
        protected DataDrivenToolTask(ResourceManager taskResources)
            : base(taskResources)
        {
            logPrivate = new TaskLoggingHelper(this);
            logPrivate.TaskResources = AssemblyResources.PrimaryResources;
            logPrivate.HelpKeywordPrefix = "MSBuild.";
        }

        #region Properties

        /// <summary>
        /// The list of all the switches that have been set
        /// </summary>
        protected Dictionary<string, ToolSwitch> ActiveToolSwitches
        {
            get
            {
                return activeToolSwitches;
            }
        }
        
        /// <summary>
        /// The additional options that have been set. These are raw switches that
        /// go last on the command line.
        /// </summary>
        public string AdditionalOptions
        {
            get
            {
                return additionalOptions;
            }
            set
            {
                additionalOptions = value;
            }
        }

        /// <summary>
        /// Overridden to use UTF16, which works better than UTF8 for older versions of CL, LIB, etc. 
        /// </summary>
        protected override Encoding ResponseFileEncoding
        {
            get 
            { 
                return Encoding.Unicode; 
            }
        }

        /// <summary>
        /// Ordered list of switches
        /// </summary>
        /// <returns>ArrayList of switches in declaration order</returns>
        protected virtual ArrayList SwitchOrderList
        {
            get
            {
                return null;
            }
        }
        #endregion

        #region ToolTask Members

        /// <summary>
        /// This method is called to find the tool if ToolPath wasn't specified.
        /// We just return the name of the tool so it can be found on the path.
        /// Deriving classes can choose to do something else.
        /// </summary>
        protected override string GenerateFullPathToTool()
        {
#if WHIDBEY_BUILD
            // if we just have the file name, search for the file on the system path
            string actualPathToTool = NativeMethodsShared.FindOnPath(ToolName);

            // if we find the file
            if (actualPathToTool != null)
            {
                // point to it
                return actualPathToTool;
            }
            else
            {
                return ToolName;
            }
#else
                return ToolName;
#endif
        }

        /// <summary>
        /// Validates all of the set properties that have either a string type or an integer type
        /// </summary>
        /// <returns></returns>
        override protected bool ValidateParameters()
        {
            return !logPrivate.HasLoggedErrors && !Log.HasLoggedErrors; 
        }

#if WHIDBEY_BUILD
        /// <summary>
        /// Delete temporary file. If the delete fails for some reason (e.g. file locked by anti-virus) then
        /// the call will not throw an exception. Instead a warning will be logged, but the build will not fail.
        /// </summary>
        /// <param name="filename">File to delete</param>
        protected void DeleteTempFile(string fileName)
        {
            try
            {
                File.Delete(fileName);
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's an IO related exception.
            {
                if (ExceptionHandling.NotExpectedException(e))
                    throw;
                // Warn only -- occasionally temp files fail to delete because of virus checkers; we 
                // don't want the build to fail in such cases
                Log.LogWarningWithCodeFromResources("Shared.FailedDeletingTempFile", fileName, e.Message);
            }
        }
#endif
        #endregion

        /// <summary>
        /// For testing purposes only
        /// Returns the generated command line
        /// </summary>
        /// <returns></returns>
        internal string GetCommandLine_ForUnitTestsOnly() 
        {
            return GenerateResponseFileCommands();
        }

        protected override string GenerateCommandLineCommands()
        {
            string commands = GenerateCommands();
            if (commands.Length < 32768)
            {
                skipResponseFileCommandGeneration = true;
                return commands;
            }

            skipResponseFileCommandGeneration = false;
            return null;
        }

        /// <summary>
        /// Creates the command line and returns it as a string by:
        /// 1. Adding all switches with the default set to the active switch list
        /// 2. Customizing the active switch list (overridden in derived classes)
        /// 3. Iterating through the list and appending switches 
        /// </summary>
        /// <returns></returns>
        protected override string GenerateResponseFileCommands()
        {
            if (skipResponseFileCommandGeneration)
            {
                skipResponseFileCommandGeneration = false;
                return null;
            }
            else
            {
                return GenerateCommands();
            }
        }

        /// <summary>
        /// Verifies that the required args are present. This function throws if we have missing required args
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        protected virtual bool VerifyRequiredArgumentsArePresent(ToolSwitch property, bool bThrowOnError)
        {
            return true;
        }
        /// <summary>
        /// Verifies that the dependencies are present, and if the dependencies are present, or if the property
        /// doesn't have any dependencies, the switch gets emitted
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        protected virtual bool VerifyDependenciesArePresent(ToolSwitch property)
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
        /// A protected method to add the switches that are by default visible
        /// e.g., /nologo is true by default
        /// </summary>
        protected virtual void AddDefaultsToActiveSwitchList()
        {
            // do nothing
        }

        /// <summary>
        /// A method that will add the fallbacks to the active switch list if the actual property is not set
        /// </summary>
        protected virtual void AddFallbacksToActiveSwitchList()
        {
            // do nothing
        }

        /// <summary>
        /// To be overriden by custom code for individual tasks
        /// </summary>
        protected virtual void PostProcessSwitchList()
        {
            // do nothing
        }

        /// <summary>
        /// Generates a part of the command line depending on the type
        /// </summary>
        /// <remarks>Depending on the type of the switch, the switch is emitted with the proper values appended.
        /// e.g., File switches will append file names, directory switches will append filenames with "\" on the end</remarks>
        /// <param name="clb"></param>
        /// <param name="toolSwitch"></param>
        protected void GenerateCommandsAccordingToType(CommandLineBuilder clb, ToolSwitch toolSwitch, bool bRecursive)
        {
            // if this property has a parent skip printing it as it was printed as part of the parent prop printing
            if (toolSwitch.Parents.Count > 0 && !bRecursive)
                return;

            switch (toolSwitch.Type)
            {
                case ToolSwitchType.Boolean:
                    EmitBooleanSwitch(clb, toolSwitch);
                    break;
                case ToolSwitchType.String:
                    EmitStringSwitch(clb, toolSwitch);
                    break;
                case ToolSwitchType.StringArray:
                    EmitStringArraySwitch(clb, toolSwitch);
                    break;
                case ToolSwitchType.Integer:
                    EmitIntegerSwitch(clb, toolSwitch);
                    break;
                case ToolSwitchType.File:
                    EmitFileSwitch(clb, toolSwitch);
                    break;
                case ToolSwitchType.Directory:
                    EmitDirectorySwitch(clb, toolSwitch);
                    break;
                case ToolSwitchType.ITaskItem:
                    EmitTaskItemSwitch(clb, toolSwitch);
                    break;
                case ToolSwitchType.ITaskItemArray:
                    EmitTaskItemArraySwitch(clb, toolSwitch);
                    break;
                case ToolSwitchType.AlwaysAppend:
                    EmitAlwaysAppendSwitch(clb, toolSwitch);
                    break;
                default:
                    // should never reach this point - if it does, there's a bug somewhere.
                    ErrorUtilities.VerifyThrow(false, "InternalError");
                    break;
            }
        }

        /// <summary>
        /// Appends a literal string containing the verbatim contents of any
        /// "AdditionalOptions" parameter. This goes last on the command
        /// line in case it needs to cancel any earlier switch.
        /// Ideally this should never be needed because the MSBuild task model
        /// is to set properties, not raw switches
        /// </summary>
        /// <param name="cmdLine"></param>
        protected void BuildAdditionalArgs(CommandLineBuilder cmdLine)
        {
            // We want additional options to be last so that this can always override other flags.
            if ((cmdLine != null) && !String.IsNullOrEmpty(additionalOptions))
            {
                cmdLine.AppendSwitch(additionalOptions);
            }
        }

        /// <summary>
        /// Emit a switch that's always appended
        /// </summary>
        private static void EmitAlwaysAppendSwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            clb.AppendSwitch(toolSwitch.Name);
        }

        /// <summary>
        /// Emit a switch that's an array of task items
        /// </summary>
        private static void EmitTaskItemArraySwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            if (String.IsNullOrEmpty(toolSwitch.Separator))
            {
                foreach (ITaskItem itemName in toolSwitch.TaskItemArray)
                {
                    clb.AppendSwitchIfNotNull(toolSwitch.SwitchValue, itemName.ItemSpec);
                }
            }
            else
            {
                clb.AppendSwitchIfNotNull(toolSwitch.SwitchValue, toolSwitch.TaskItemArray, toolSwitch.Separator);
            }
        }

        /// <summary>
        /// Emit a switch that's a scalar task item
        /// </summary>
        private static void EmitTaskItemSwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            if (!String.IsNullOrEmpty(toolSwitch.Name))
            {
                clb.AppendSwitch(toolSwitch.Name + toolSwitch.Separator);
            }
        }

        /// <summary>
        /// Generates the command line for the tool.
        /// </summary>
        private string GenerateCommands()
        {
            // the next three methods are overridden by the base class
            // here it does nothing unless overridden
            AddDefaultsToActiveSwitchList();

            AddFallbacksToActiveSwitchList();

            PostProcessSwitchList();

#if WHIDBEY_BUILD
            CommandLineBuilder commandLineBuilder = new CommandLineBuilder();
#else
            CommandLineBuilder commandLineBuilder = new CommandLineBuilder(true /* quote hyphens */);
#endif

            // iterates through the list of set toolswitches
            foreach (string propertyName in SwitchOrderList)
            {
                if (IsPropertySet(propertyName))
                {
                    ToolSwitch property = activeToolSwitches[propertyName];

                    // verify the dependencies
                    if (VerifyDependenciesArePresent(property) && VerifyRequiredArgumentsArePresent(property, false))
                    {
                        GenerateCommandsAccordingToType(commandLineBuilder, property, false);
                    }
                }
                else if (String.Equals(propertyName, "AlwaysAppend", StringComparison.OrdinalIgnoreCase))
                {
                    commandLineBuilder.AppendSwitch(AlwaysAppend);
                }
            }

            // additional args should go on the end
            BuildAdditionalArgs(commandLineBuilder);
            return commandLineBuilder.ToString();
        }

        /// <summary>
        /// Checks to see if the argument is required and whether an argument exists, and returns the 
        /// argument or else fallback argument if it exists.
        /// 
        /// These are the conditions to look at:
        /// 
        /// ArgumentRequired    ArgumentParameter   FallbackArgumentParameter   Result
        /// true                isSet               NA                          The value in ArgumentParameter gets returned
        /// true                isNotSet            isSet                       The value in FallbackArgumentParamter gets returned
        /// true                isNotSet            isNotSet                    An error occurs, as argumentrequired is true
        /// false               isSet               NA                          The value in ArgumentParameter gets returned
        /// false               isNotSet            isSet                       The value in FallbackArgumentParameter gets returned
        /// false               isNotSet            isNotSet                    The empty string is returned, as there are no arguments, and no arguments are required
        /// </summary>
        /// <param name="toolSwitch"></param>
        /// <returns></returns>
        protected virtual string GetEffectiveArgumentsValues(ToolSwitch toolSwitch)
        {
            //if (!toolSwitch.ArgumentRequired && !IsPropertySet(toolSwitch.ArgumentParameter) && 
            //    !IsPropertySet(toolSwitch.FallbackArgumentParameter))
            //{
            //    return String.Empty;
            //}

            //// check to see if it has an argument
            //if (toolSwitch.ArgumentRequired)
            //{
            //    if (!IsPropertySet(toolSwitch.ArgumentParameter) && !IsPropertySet(toolSwitch.FallbackArgumentParameter))
            //    {
            //        throw new ArgumentException(logPrivate.FormatResourceString("ArgumentRequired", toolSwitch.Name));
            //    }
            //}
            //// if it gets to here, the argument or the fallback is set
            //if (IsPropertySet(toolSwitch.ArgumentParameter))
            //{
            //    return ActiveToolSwitches[toolSwitch.ArgumentParameter].ArgumentValue;
            //}
            //else
            //{
            //    return ActiveToolSwitches[toolSwitch.FallbackArgumentParameter].ArgumentValue;
            //}
            return "GetEffectiveArgumentValue not Impl";
        }

        /// <summary>
        /// Appends the directory name to the end of a switch
        /// Ensure the name ends with a slash
        /// </summary>
        /// <remarks>For directory switches (e.g., TrackerLogDirectory), the toolSwitchName (if it exists) is emitted
        /// along with the FileName which is ensured to have a trailing slash</remarks>
        /// <param name="clb"></param>
        /// <param name="toolSwitch"></param>
        private static void EmitDirectorySwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            if (!String.IsNullOrEmpty(toolSwitch.SwitchValue))
            {
                //clb.AppendSwitchIfNotNull(toolSwitch.Name + toolSwitch.Separator, EnsureTrailingSlash(toolSwitch.ArgumentValue));
                clb.AppendSwitch(toolSwitch.SwitchValue + toolSwitch.Separator);
            }
        }

        /// <summary>
        /// Generates the switches that have filenames attached to the end
        /// </summary>
        /// <remarks>For file switches (e.g., PrecompiledHeaderFile), the toolSwitchName (if it exists) is emitted
        /// along with the FileName which may or may not have quotes</remarks>
        /// e.g., PrecompiledHeaderFile = "File" will emit /FpFile
        /// <param name="clb"></param>
        /// <param name="toolSwitch"></param>
        private static void EmitFileSwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            if (!String.IsNullOrEmpty(toolSwitch.Value))
            {
                String str = toolSwitch.Value;
                str.Trim();

                if (!str.StartsWith("\""))
                {
                    str = "\"" + str;
                    if (str.EndsWith("\\") && !str.EndsWith("\\\\"))
                        str += "\\\"";
                    else
                        str += "\"";
                }

                //we want quotes always, AppendSwitchIfNotNull will add them on as needed bases 
                clb.AppendSwitchUnquotedIfNotNull(toolSwitch.SwitchValue + toolSwitch.Separator, str);
            }
        }

        /// <summary>
        /// Generates the commands for switches that have integers appended.
        /// </summary>
        /// <remarks>For integer switches (e.g., WarningLevel), the toolSwitchName is emitted
        /// with the appropriate integer appended, as well as any arguments
        /// e.g., WarningLevel = "4" will emit /W4</remarks>
        /// <param name="clb"></param>
        /// <param name="toolSwitch"></param>
        private void EmitIntegerSwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            if (toolSwitch.IsValid)
            {
                if (!String.IsNullOrEmpty(toolSwitch.Separator))
                {
                    clb.AppendSwitch(toolSwitch.SwitchValue + toolSwitch.Separator + toolSwitch.Number.ToString() + GetEffectiveArgumentsValues(toolSwitch));
                }
                else
                {
                    clb.AppendSwitch(toolSwitch.SwitchValue + toolSwitch.Number.ToString() + GetEffectiveArgumentsValues(toolSwitch));
                }
            }
        }

        /// <summary>
        /// Generates the commands for the switches that may have an array of arguments
        /// The switch may be empty.
        /// </summary>
        /// <remarks>For stringarray switches (e.g., Sources), the toolSwitchName (if it exists) is emitted
        /// along with each and every one of the file names separately (if no separator is included), or with all of the 
        /// file names separated by the separator.
        /// e.g., AdditionalIncludeDirectores = "@(Files)" where Files has File1, File2, and File3, the switch
        /// /IFile1 /IFile2 /IFile3 or the switch /IFile1;File2;File3 is emitted (the latter case has a separator 
        /// ";" specified)</remarks>
        /// <param name="clb"></param>
        /// <param name="toolSwitch"></param>
        private static void EmitStringArraySwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            string[] ArrTrimStringList = new string [toolSwitch.StringList.Length];
            for (int i=0; i<toolSwitch.StringList.Length; ++i)
            {
                //Make sure the file doesn't contain escaped " (\") 
                if (toolSwitch.StringList[i].StartsWith("\"") && toolSwitch.StringList[i].EndsWith("\""))
                {
                    ArrTrimStringList[i] = toolSwitch.StringList[i].Substring(1, toolSwitch.StringList[i].Length - 2);
                }
                else
                {
                    ArrTrimStringList[i] = toolSwitch.StringList[i];
                }
            }

            if (String.IsNullOrEmpty(toolSwitch.Separator))
            {
                foreach (string fileName in ArrTrimStringList)
                {
                    clb.AppendSwitchIfNotNull(toolSwitch.SwitchValue, fileName);
                }
            }
            else
            {
                clb.AppendSwitchIfNotNull(toolSwitch.SwitchValue, ArrTrimStringList, toolSwitch.Separator);
            }
        }

        /// <summary>
        /// Generates the switches for switches that either have literal strings appended, or have 
        /// different switches based on what the property is set to.
        /// </summary>
        /// <remarks>The string switch emits a switch that depends on what the parameter is set to, with and
        /// arguments
        /// e.g., Optimization = "Full" will emit /Ox, whereas Optimization = "Disabled" will emit /Od</remarks>
        /// <param name="clb"></param>
        /// <param name="toolSwitch"></param>
        private void EmitStringSwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            String strSwitch = String.Empty;
            strSwitch += toolSwitch.SwitchValue + toolSwitch.Separator;
            
            StringBuilder val = new StringBuilder(GetEffectiveArgumentsValues(toolSwitch));
            String str = toolSwitch.Value;

            if (!toolSwitch.MultiValues)
            {

                str.Trim();

                if (!str.StartsWith("\""))
                {
                    str = "\"" + str;
                    if (str.EndsWith("\\") && !str.EndsWith("\\\\"))
                        str += "\\\"";
                    else
                        str += "\"";
                }
                val.Insert(0, str);
            }

            if ((strSwitch.Length == 0) && (val.ToString().Length == 0))
                return;

            clb.AppendSwitchUnquotedIfNotNull(strSwitch, val.ToString());
            
        }

        /// <summary>
        /// Generates the switches that are nonreversible
        /// </summary>
        /// <remarks>A boolean switch is emitted if it is set to true. If it set to false, nothing is emitted.
        /// e.g. nologo = "true" will emit /Og, but nologo = "false" will emit nothing.</remarks>
        /// <param name="clb"></param>
        /// <param name="toolSwitch"></param>
        private void EmitBooleanSwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            if (toolSwitch.BooleanValue)
            {
                if (!String.IsNullOrEmpty(toolSwitch.SwitchValue))
                {
                    StringBuilder val = new StringBuilder(GetEffectiveArgumentsValues(toolSwitch));
                    val.Insert(0, toolSwitch.Separator);
                    val.Insert(0, toolSwitch.TrueSuffix);
                    val.Insert(0, toolSwitch.SwitchValue);
                    clb.AppendSwitch(val.ToString());
                }
            }
            else
                EmitReversibleBooleanSwitch(clb, toolSwitch);
        }

        /// <summary>
        /// Generates the command line for switches that are reversible
        /// </summary>
        /// <remarks>A reversible boolean switch will emit a certain switch if set to true, but emit that 
        /// exact same switch with a flag appended on the end if set to false.
        /// e.g., GlobalOptimizations = "true" will emit /Og, and GlobalOptimizations = "false" will emit /Og-</remarks>
        /// <param name="clb"></param>
        /// <param name="toolSwitch"></param>
        private void EmitReversibleBooleanSwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            // if the value is set to true, append whatever the TrueSuffix is set to.
            // Otherwise, append whatever the FalseSuffix is set to.
            if (!String.IsNullOrEmpty(toolSwitch.ReverseSwitchValue))
            {
                string suffix = (toolSwitch.BooleanValue) ? toolSwitch.TrueSuffix : toolSwitch.FalseSuffix;
                StringBuilder val = new StringBuilder(GetEffectiveArgumentsValues(toolSwitch));
                val.Insert(0, suffix);
                val.Insert(0, toolSwitch.Separator);
                val.Insert(0, toolSwitch.TrueSuffix);
                val.Insert(0, toolSwitch.ReverseSwitchValue);
                clb.AppendSwitch(val.ToString());
            }
        }

        /// <summary>
        /// Checks to make sure that a switch has either a '/' or a '-' prefixed.
        /// </summary>
        /// <param name="toolSwitch"></param>
        /// <returns></returns>
        private string Prefix(string toolSwitch)
        {
            if (!String.IsNullOrEmpty(toolSwitch))
            {
                if (toolSwitch[0] != prefix)
                {
                    return prefix + toolSwitch;
                }
            }
            return toolSwitch;
        }

        /// <summary>
        /// A method that will validate the integer type arguments
        /// If the min or max is set, and the value a property is set to is not within
        /// the range, the build fails
        /// </summary>
        /// <param name="switchName"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="value"></param>
        /// <returns>The valid integer passed converted to a string form</returns>
        protected bool ValidateInteger(string switchName, int min, int max, int value)
        {
            if (value < min || value > max)
            {
                logPrivate.LogErrorFromResources("ArgumentOutOfRange", switchName, value);
                return false;
            }
           
            return true;
            
        }

        /// <summary>
        /// A method for the enumerated values a property can have
        /// This method checks the value a property is set to, and finds the corresponding switch
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="switchMap"></param>
        /// <param name="value"></param>
        /// <returns>The switch that a certain value is mapped to</returns>
        protected string ReadSwitchMap(string propertyName, string[][] switchMap, string value)
        {
            if (switchMap != null)
            {
                for (int i = 0; i < switchMap.Length; ++i)
                {
                    if (String.Equals(switchMap[i][0], value, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return switchMap[i][1];
                    }
                }
                logPrivate.LogErrorFromResources("ArgumentOutOfRange", propertyName, value);
            }
            return String.Empty;
        }

        /// <summary>
        /// Returns true if the property has a value in the list of active tool switches
        /// </summary>
        protected bool IsPropertySet(string propertyName)
        {
            if (!String.IsNullOrEmpty(propertyName))
            {
                return activeToolSwitches.ContainsKey(propertyName);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true if the property is set to true.
        /// Returns false if the property is not set, or set to false.
        /// </summary>
        protected bool IsSetToTrue(string propertyName)
        {
            if (activeToolSwitches.ContainsKey(propertyName))
            {
                return activeToolSwitches[propertyName].BooleanValue;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true if the property is set to false.
        /// Returns false if the property is not set, or set to true.
        /// </summary>
        protected bool IsExplicitlySetToFalse(string propertyName)
        {
            if (activeToolSwitches.ContainsKey(propertyName))
            {
                return !activeToolSwitches[propertyName].BooleanValue;
            }
            else
            {
                return false;
            }
        }
        
        /// <summary>
        /// Checks to see if the switch name is empty
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        protected bool HasSwitch(string propertyName)
        {
            if (IsPropertySet(propertyName))
            {
                return !String.IsNullOrEmpty(activeToolSwitches[propertyName].Name);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// If the given path doesn't have a trailing slash then add one.
        /// </summary>
        /// <param name="directoryName">The path to check.</param>
        /// <returns>A path with a slash.</returns>
        protected static string EnsureTrailingSlash(string directoryName)
        {
            ErrorUtilities.VerifyThrow(directoryName != null, "InternalError");
            if (!String.IsNullOrEmpty(directoryName))
            {
                char endingCharacter = directoryName[directoryName.Length - 1];
                if (!(endingCharacter == Path.DirectorySeparatorChar
                    || endingCharacter == Path.AltDirectorySeparatorChar) )
                {
                    directoryName += Path.DirectorySeparatorChar;
                }
            }

            return directoryName;
        }

        /// <summary>
        /// The string that is always appended on the command line. Overridden by deriving classes.
        /// </summary>
        protected virtual string AlwaysAppend
        {
            get
            {
                return String.Empty;
            }
            set
            {
                // do nothing
            }
        }
    }
}

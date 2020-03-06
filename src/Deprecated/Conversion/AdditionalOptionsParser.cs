// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Conversion
{
    /// <summary>
    /// Only these switches will be migrated.
    /// This enum doesnt have any use, except to explicitly list the compiler
    /// options in AdditionalOptions project property that will be migrated
    /// </summary>
    internal enum SwitchesToMigrate
    {
        STM_CodePage,
        STM_DisableLangExtensions,
        STM_Jcpa,
        STM_LinkResource,
        STM_SecureScoping,
        STM_Win32Resource,
    };

    /// <summary>
    /// These are types of values associated with the switches
    /// </summary>
    internal enum SwitchValueType
    {
        /// <summary>
        /// Boolean value 
        /// </summary>
        SVT_Boolean,

        /// <summary>
        /// String value
        /// </summary>
        SVT_String,

        /// <summary>
        /// This switch can occur multiple times and the 
        /// final value is the ';' delimeted concat of all the
        /// individual occurrences
        /// </summary>
        SVT_MultiString,
    }

    /// <summary>
    /// This class contains the migration info for a switch
    /// that we want to migrate 
    /// </summary>
    internal sealed class CompSwitchInfo
    {
        /// <summary>
        /// This is the internal switch identifier
        /// Examples:
        /// 1. STM_SecureScoping
        /// </summary>
        internal SwitchesToMigrate Switch;

        /// <summary>
        /// This is the string passed to the compiler
        /// Examples:
        /// 1. /ss, /securescoping
        /// 2. @
        /// </summary>
        internal string[] SwitchIDs;

        /// <summary>
        /// This is the type of the value associated with the switch
        /// Examples:
        /// 1. SVT_Boolean
        /// 2. SVT_MultiString
        /// </summary>
        internal SwitchValueType SwitchValueType;

        /// <summary>
        /// This is the final value of the switch
        /// 1. true
        /// 2. "path-a;path-b\\file-b"
        /// </summary>
        internal object SwitchValue;

        /// <summary>
        /// This is the the name of property in the project file in which the
        /// value of this switch is stored
        /// </summary>
        internal string SwitchProjectPropertyName;

        /// <summary>
        /// The constructor
        /// </summary>
        internal CompSwitchInfo(
            SwitchesToMigrate switchStr,
            string[] switchIDs,
            SwitchValueType switchValueType,
            object switchValue,
            string switchProjectPropertyName
        )
        {
            this.Switch = switchStr;
            this.SwitchIDs = switchIDs;
            this.SwitchValueType = switchValueType;
            this.SwitchValue = switchValue;
            this.SwitchProjectPropertyName = switchProjectPropertyName;
        }
    }

    /// <summary>
    /// 
    /// Class:       AdditionalOptionsParser
    /// Owner:       ParthaD
    /// 
    /// This class contains the logic to parse the AdditionalOptions project 
    /// property of v7.x J# projects and add the individual options as project
    /// properties of the upgraded projects.
    /// 
    /// AdditionalOptions project property in v7.x was basically a string that
    /// was passed ditto to the compiler.
    /// It was used to hold J# compiler options that didnt have an 1-1 equivalent
    /// project property.
    /// For v8.0 and beyond, each J# compiler option has a corresponding project
    /// property.
    /// 
    /// AdditionalOptions property string is broken down into list of options.
    /// White space (only ' ' and '\t') are considered as delimiters if not wrapped
    /// inside double quotes ("). 
    /// NOTE:
    ///  1. Other unicode spaces or double quotes sequences not considered
    ///  2. Backslash (\) not considered as possible escape char for ". 
    /// 
    /// Once broken down into individual options, only a few compiler options are
    /// seached for (viz. the options for which v8.0 has new project properties)
    /// Everything else is ignored.
    /// 
    /// Refer to SwitchesToMigrade enum for the switches that are migrated.
    /// </summary>
    internal sealed class AdditionalOptionsParser
    {
        // These are all that we recognize in the AdditionalOptions    
        private CompSwitchInfo[] validCompilerSwitches = new CompSwitchInfo[] {
            #region Info on the compiler switches to be parsed from AdditionalOptions
            // /codepage:<n>
            new CompSwitchInfo(
                SwitchesToMigrate.STM_CodePage,
                new string[] { "/codepage:" },
                SwitchValueType.SVT_String,
                null,
                "CodePage"
            ),
            
            // /x:[all | net]
            new CompSwitchInfo(
                SwitchesToMigrate.STM_DisableLangExtensions,
                new string[] { "/x:" },
                SwitchValueType.SVT_String,
                null,
                "DisableLangXtns"
            ),

            // /jcpa:[package=namespace | @filename]
            new CompSwitchInfo(
                SwitchesToMigrate.STM_Jcpa,
                new string[] { "/jcpa:" },
                SwitchValueType.SVT_MultiString,
                new StringBuilder(),
                "JCPA"
            ),

            // /linkres[ource]:<resinfo>
            new CompSwitchInfo(
                SwitchesToMigrate.STM_LinkResource,
                new string[] { "/linkres:", "/linkresource:" },
                SwitchValueType.SVT_MultiString,
                new StringBuilder(),
                "LinkResource"
            ),

            // /securescoping[+|-], /ss[+|-]
            new CompSwitchInfo(
                SwitchesToMigrate.STM_SecureScoping,
                new string[] { "/securescoping", "/ss" },
                SwitchValueType.SVT_Boolean,
                null,
                "SecureScoping"
            ),
            
            // /win32res:<file>
            new CompSwitchInfo(
                SwitchesToMigrate.STM_Win32Resource,
                new string[] { "/win32res:" },
                SwitchValueType.SVT_String,
                null,
                "Win32Resource"
            )
            #endregion
        };

        /// <summary>
        /// One and only entry point to the functionality offered by this class
        /// </summary>
        public void ProcessAdditionalOptions(
            string additionalOptionsValue,
            ProjectPropertyGroupElement configPropertyGroup
        )
        {
            // Trivial case
            if (null == additionalOptionsValue)
            {
                return;
            }

            // Tokenize the additional options first
            string[] compSwitchList;
            compSwitchList = TokenizeAdditionalOptionsValue(additionalOptionsValue);
            
            // Extract the switch arguments
            foreach (string compSwitch in compSwitchList)
            {
                foreach (CompSwitchInfo compSwitchInfo in validCompilerSwitches)
                {
                    if (ExtractSwitchInfo(compSwitchInfo, compSwitch))
                    {
                        break;
                    }
                }
            }
            
            // Finally populate the project file and we'r done!
            PopulatePropertyGroup(configPropertyGroup);
        }
        
        /// <summary>
        /// This will tokenize the given string using ' ' and '\t' as delimiters
        /// The delimiters are escaped inside a pair of quotes
        /// If there is an unbalanced quote, EOL is treated as the closing quotes
        /// </summary>
        private string[] TokenizeAdditionalOptionsValue(string additionalOptionsValue)
        {
            ArrayList tokens = new ArrayList();
            
            bool inQuotes = false;
            StringBuilder option = new StringBuilder();
            foreach (char c in additionalOptionsValue)
            {
                switch (c)
                {
                    case '\t': case ' ':
                        if (inQuotes)
                        {
                            option.Append(c);
                        }
                        else
                        {
                            if (0 != option.Length)
                            {
                                tokens.Add(option.ToString());
                                option.Length = 0;
                            }
                        }
                    break;

                    case '"':
                        inQuotes = !inQuotes;
                    break;

                    default:
                        option.Append(c);
                    break;
                }
            }

            // Ignore everything unbalanced quotes
            if (!inQuotes)
            {
                tokens.Add(option.ToString());
            }

            return (string[])tokens.ToArray(typeof(string));
        }

        /// <summary>
        /// If compSwitch is the compSwitchInfo compiler switch, then extract the switch args
        /// Return
        /// - true: if this is the switch (even if the switch args have error)
        /// - false: this is not the switch
        /// </summary>
        private bool ExtractSwitchInfo(CompSwitchInfo compSwitchInfo, string compSwitch)
        {
            string matchedID = null;
            // First see if we have a match...
            for (int i=0; i<compSwitchInfo.SwitchIDs.Length; i++)
            {
                if (compSwitch.StartsWith(compSwitchInfo.SwitchIDs[i], StringComparison.Ordinal))
                {
                    matchedID = compSwitchInfo.SwitchIDs[i];
                    break;
                }
            }
            // No no... we arent dealing with the correct switchInfo
            if (null == matchedID)
            {
                return false;
            }

            // Now we can get to extracting the switch arguments
            object switchVal = null;
            switch (compSwitchInfo.SwitchValueType)
            {
                case SwitchValueType.SVT_Boolean:
                    if (matchedID.Length == compSwitch.Length)
                    {
                        switchVal = true;
                    }
                    else if ((matchedID.Length + 1) == compSwitch.Length)
                    {
                        if ('+' == compSwitch[matchedID.Length])
                        {
                            switchVal = true;
                        }
                        else if ('-' == compSwitch[matchedID.Length])
                        {
                            switchVal = false;
                        }
                    }
                    if (null != switchVal)
                    {
                        compSwitchInfo.SwitchValue = switchVal;
                    }
                    else
                    {
                        Debug.Assert(false, "Cannot parse boolean switch: " + compSwitch);
                    }
                break;

                case SwitchValueType.SVT_String:
                    if (matchedID.Length < compSwitch.Length)
                    {
                        switchVal = compSwitch.Substring(matchedID.Length);
                    }
                    if (null != switchVal)
                    {
                        compSwitchInfo.SwitchValue = switchVal;
                    }
                    else
                    {
                        Debug.Assert(false, "Cannot parse string switch: " + compSwitch);
                    }
                break;

                case SwitchValueType.SVT_MultiString:
                    Debug.Assert(
                        null != compSwitchInfo.SwitchValue, 
                        "Non null switch value expected for a multistring switch: " + matchedID
                    );

                    if (matchedID.Length < compSwitch.Length)
                    {
                        switchVal = compSwitch.Substring(matchedID.Length);
                    }
                    if (null != switchVal)
                    {
                        ((StringBuilder)(compSwitchInfo.SwitchValue)).Append(switchVal);
                        ((StringBuilder)(compSwitchInfo.SwitchValue)).Append(";");
                    }
                    else
                    {
                        Debug.Assert(false, "Cannot parse multistring switch: " + compSwitch);
                    }
                break;

                default:
                    Debug.Assert(false, "Unknown switch value type");
                break;
            }

            return true;
        }

        /// <summary>
        /// Populate the property group with the individual options
        /// </summary>
        private void PopulatePropertyGroup(ProjectPropertyGroupElement configPropertyGroup)
        {
            string propertyName;

            foreach (CompSwitchInfo compSwitchInfo in validCompilerSwitches)
            {
                propertyName = compSwitchInfo.SwitchProjectPropertyName;

                // No need to remove the already existing property node
                // since the switches we are dealing with couldnt have been
                // set anywhere else in the property pages except the additional
                // options

                switch (compSwitchInfo.SwitchValueType)
                {
                    case SwitchValueType.SVT_Boolean:
                        if (null != compSwitchInfo.SwitchValue)
                        {
                            configPropertyGroup.AddProperty(
                                propertyName, 
                                compSwitchInfo.SwitchValue.ToString().ToLower(CultureInfo.InvariantCulture)
                            );
                        }
                    break;

                    case SwitchValueType.SVT_String:
                        if (null != compSwitchInfo.SwitchValue)
                        {
                            configPropertyGroup.AddProperty(
                                propertyName,
                                compSwitchInfo.SwitchValue.ToString()
                            );
                        }
                    break;

                    case SwitchValueType.SVT_MultiString:
                        Debug.Assert(null != compSwitchInfo.SwitchValue, "Expected non null value for multistring switch");
                        if (0 != ((StringBuilder)(compSwitchInfo.SwitchValue)).Length)
                        {
                            configPropertyGroup.AddProperty(
                                propertyName,
                                compSwitchInfo.SwitchValue.ToString()
                            );
                        }
                    break;

                    default:
                        Debug.Assert(false, "Unknown switch value type");
                    break;
                }
            }
        }
    }
}

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A small intermediate class between ToolTask and classes using it in XMakeTasks, implementing functionality
    /// that we didn't want to expose in Utilities
    /// </summary>
    /// <remarks>
    /// This class has to be public because the tasks that derive from it are public.
    /// Ideally we would like this class to be internal, but C# does not allow a base class
    /// to be less accessible than its derived classes.
    /// </remarks>
    public abstract class ToolTaskExtension : ToolTask
    {
        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        internal ToolTaskExtension() :
            base(AssemblyResources.PrimaryResources, "MSBuild.")
        {
            _logExtension = new TaskLoggingHelperExtension(
                this,
                AssemblyResources.PrimaryResources,
                AssemblyResources.SharedResources,
                "MSBuild.");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets an instance of a TaskLoggingHelperExtension class containing task logging methods.
        /// </summary>
        /// <value>The logging helper object.</value>
        new public TaskLoggingHelper Log
        {
            get
            {
                return _logExtension;
            }
        }

        // the logging helper
        private TaskLoggingHelperExtension _logExtension;

        /// <summary>
        /// Whether this ToolTaskExtension has logged any errors
        /// </summary>
        protected override bool HasLoggedErrors
        {
            get
            {
                return (Log.HasLoggedErrors || base.HasLoggedErrors);
            }
        }

        /// <summary>
        /// Gets the collection of parameters used by the derived task class.
        /// </summary>
        /// <value>Parameter bag.</value>
        protected internal Hashtable Bag
        {
            get
            {
                return _bag;
            }
        }

        private Hashtable _bag = new Hashtable();

        #endregion

        #region Methods

        /// <summary>
        /// Get a bool parameter and return a default if its not present
        /// in the hash table.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="parameterName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        protected internal bool GetBoolParameterWithDefault(string parameterName, bool defaultValue)
        {
            object obj = _bag[parameterName];
            return (obj == null) ? defaultValue : (bool)obj;
        }

        /// <summary>
        /// Get an int parameter and return a default if its not present
        /// in the hash table.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="parameterName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        protected internal int GetIntParameterWithDefault(string parameterName, int defaultValue)
        {
            object obj = _bag[parameterName];
            return (obj == null) ? defaultValue : (int)obj;
        }

        /// <summary>
        /// Returns the command line switch used by the tool executable to specify the response file
        /// Will only be called if the task returned a non empty string from GetResponseFileCommands
        /// Called after ValidateParameters, SkipTaskExecution and GetResponseFileCommands
        /// </summary>
        /// <param name="responseFilePath">full path to the temporarily created response file</param>
        /// <returns></returns>
        override protected string GenerateResponseFileCommands()
        {
            CommandLineBuilderExtension commandLineBuilder = new CommandLineBuilderExtension();
            AddResponseFileCommands(commandLineBuilder);
            return commandLineBuilder.ToString();
        }

        /// <summary>
        /// Returns a string with those switches and other information that can't go into a response file and
        /// must go directly onto the command line.
        /// Called after ValidateParameters and SkipTaskExecution
        /// </summary>
        /// <returns></returns>
        override protected string GenerateCommandLineCommands()
        {
            CommandLineBuilderExtension commandLineBuilder = new CommandLineBuilderExtension();
            AddCommandLineCommands(commandLineBuilder);
            return commandLineBuilder.ToString();
        }

        /// <summary>
        /// Fills the provided CommandLineBuilderExtension with those switches and other information that can go into a response file.
        /// </summary>
        /// <param name="commandLine"></param>
        protected internal virtual void AddResponseFileCommands(CommandLineBuilderExtension commandLine)
        {
        }

        /// <summary>
        /// Fills the provided CommandLineBuilderExtension with those switches and other information that can't go into a response file and
        /// must go directly onto the command line.
        /// </summary>
        /// <param name="commandLine"></param>
        /// <returns>true, if successful</returns>
        protected internal virtual void AddCommandLineCommands(CommandLineBuilderExtension commandLine)
        {
        }

        #endregion
    }
}

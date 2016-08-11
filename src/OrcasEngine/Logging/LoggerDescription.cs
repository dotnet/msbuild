// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Text;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is used to contain information about a logger as a collection of values that
    /// can be used to instantiate the logger and can be serialized to be passed between different
    /// processes.
    /// </summary>
    public class LoggerDescription
    {
        #region Constructor

        internal LoggerDescription()
        {
        }

        /// <summary>
        /// Creates a logger description from given data
        /// </summary>
        public LoggerDescription
        (
            string loggerClassName,
            string loggerAssemblyName,
            string loggerAssemblyFile,
            string loggerSwitchParameters,
            LoggerVerbosity verbosity
        )
        {
            this.loggerClassName = loggerClassName;
            this.loggerAssembly = new AssemblyLoadInfo(loggerAssemblyName, loggerAssemblyFile);
            this.loggerSwitchParameters = loggerSwitchParameters;
            this.verbosity = verbosity;
        }

        #endregion

        #region Properties

        /// <summary>
        /// This property exposes the logger id which identifies each distributed logger uniquiely
        /// </summary>
        internal int LoggerId
        {
            get
            {
                return this.loggerId;
            }
            set
            {
                this.loggerId = value;
            }
        }

        /// <summary>
        /// This property generates the logger name by appending together the class name and assembly name
        /// </summary>
        internal string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(this.loggerClassName) &&
                    !string.IsNullOrEmpty(this.loggerAssembly.AssemblyFile))
                {
                    return this.loggerClassName + ":" + this.loggerAssembly.AssemblyFile;
                }
                else if ( !string.IsNullOrEmpty(this.loggerClassName) )
                {
                    return this.loggerClassName;
                }
                else
                {
                    return this.loggerAssembly.AssemblyFile;
                }
            }
        }

        /// <summary>
        /// Returns the string of logger parameters, null if there are none
        /// </summary>
        public string LoggerSwitchParameters
        {
            get
            {
                return loggerSwitchParameters;
            }
        }

        /// <summary>
        /// Return the verbosity for this logger (from command line all loggers get same verbosity)
        /// </summary>
        public LoggerVerbosity Verbosity
        {
            get
            {
                return this.verbosity;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Create an IForwardingLogger out of the data in this description. This method may throw a variety of
        /// reflection exceptions if the data is invalid. It is the resposibility of the caller to handle these
        /// exceptions if desired.
        /// </summary>
        /// <returns></returns>
        internal IForwardingLogger CreateForwardingLogger()
        {
            return (IForwardingLogger)CreateLogger(true);
        }

        /// <summary>
        /// Create an ILogger out of the data in this description. This method may throw a variety of
        /// reflection exceptions if the data is invalid. It is the resposibility of the caller to handle these
        /// exceptions if desired.
        /// </summary>
        /// <returns></returns>
        internal ILogger CreateLogger()
        {
            return CreateLogger(false);
        }

        /// <summary>
        /// Loads a logger from its assembly, instantiates it, and handles errors.
        /// </summary>
        /// <returns>Instantiated logger.</returns>
        private ILogger CreateLogger(bool forwardingLogger)
        {
            ILogger logger = null;

            try
            {
                if (forwardingLogger)
                {
                    // load the logger from its assembly
                    LoadedType loggerClass = (new TypeLoader(forwardingLoggerClassFilter)).Load(loggerClassName, loggerAssembly);

                    if (loggerClass != null)
                    {
                        // instantiate the logger
                        logger = (IForwardingLogger)Activator.CreateInstance(loggerClass.Type);
                    }
                }
                else
                {
                    // load the logger from its assembly
                    LoadedType loggerClass = (new TypeLoader(loggerClassFilter)).Load(loggerClassName, loggerAssembly);

                    if (loggerClass != null)
                    {
                        // instantiate the logger
                        logger = (ILogger)Activator.CreateInstance(loggerClass.Type);
                    }
                }
            }
            catch (TargetInvocationException e)
            {
                // At this point, the interesting stack is the internal exception;
                // the outer exception is System.Reflection stuff that says nothing
                // about the nature of the logger failure.
                Exception innerException = e.InnerException;

                if (innerException is LoggerException)
                {
                    // Logger failed politely during construction. In order to preserve
                    // the stack trace at which the error occured we wrap the original
                    // exception instead of throwing.
                    LoggerException l = ((LoggerException)innerException);
                    throw new LoggerException(l.Message, innerException, l.ErrorCode, l.HelpKeyword);
                }
                else
                {
                    throw;
                }
            }

            return logger;
        }

        /// <summary>
        /// Used for finding loggers when reflecting through assemblies.
        /// </summary>
        private static readonly TypeFilter forwardingLoggerClassFilter = new TypeFilter(IsForwardingLoggerClass);

        /// <summary>
        /// Used for finding loggers when reflecting through assemblies.
        /// </summary>
        private static readonly TypeFilter loggerClassFilter = new TypeFilter(IsLoggerClass);

        /// <summary>
        /// Checks if the given type is a logger class.
        /// </summary>
        /// <remarks>This method is used as a TypeFilter delegate.</remarks>
        /// <returns>true, if specified type is a logger</returns>
        private static bool IsForwardingLoggerClass(Type type, object unused)
        {
            return (type.IsClass &&
                !type.IsAbstract &&
                (type.GetInterface("IForwardingLogger") != null));
        }

        /// <summary>
        /// Checks if the given type is a logger class.
        /// </summary>
        /// <remarks>This method is used as a TypeFilter delegate.</remarks>
        /// <returns>true, if specified type is a logger</returns>
        private static bool IsLoggerClass(Type type, object unused)
        {
            return (type.IsClass &&
                !type.IsAbstract &&
                (type.GetInterface("ILogger") != null));
        }

        /// <summary>
        /// Converts the path to the logger assembly to a full path
        /// </summary>
        internal void ConvertPathsToFullPaths()
        {
            if (loggerAssembly.AssemblyFile != null)
            {
                loggerAssembly = 
                    new AssemblyLoadInfo(loggerAssembly.AssemblyName, Path.GetFullPath(loggerAssembly.AssemblyFile));
            }
        }

        #endregion

        #region Data
        private string loggerClassName;
        private string loggerSwitchParameters;
        private AssemblyLoadInfo loggerAssembly; 
        private LoggerVerbosity verbosity;
        private int loggerId;
        #endregion

        #region CustomSerializationToStream
        internal void WriteToStream(BinaryWriter writer)
        {
            #region LoggerClassName
            if (loggerClassName == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(loggerClassName);
            }
            #endregion
            #region LoggerSwitchParameters
            if (loggerSwitchParameters == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(loggerSwitchParameters);
            }
            #endregion
            #region LoggerAssembly
            if (loggerAssembly == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                if (loggerAssembly.AssemblyFile == null)
                {
                    writer.Write((byte)0);
                }
                else
                {
                    writer.Write((byte)1);
                    writer.Write(loggerAssembly.AssemblyFile);
                }

                if (loggerAssembly.AssemblyName == null)
                {
                    writer.Write((byte)0);
                }
                else
                {
                    writer.Write((byte)1);
                    writer.Write(loggerAssembly.AssemblyName);
                }
            }
            #endregion
            writer.Write((Int32)verbosity);
            writer.Write((Int32)loggerId);
        }

        internal void CreateFromStream(BinaryReader reader)
        {
            #region LoggerClassName
            if (reader.ReadByte() ==0)
            {
                loggerClassName = null;
            }
            else
            {
                loggerClassName = reader.ReadString();
            }
            #endregion 
            #region LoggerSwitchParameters
            if (reader.ReadByte() == 0)
            {
                loggerSwitchParameters = null;
            }
            else
            {
                loggerSwitchParameters = reader.ReadString();
            }
            #endregion
            #region LoggerAssembly
            if (reader.ReadByte() == 0)
            {
                loggerAssembly = null;
            }
            else
            {

                string assemblyName = null;
                string assemblyFile = null;

                if (reader.ReadByte() != 0)
                {
                    assemblyFile = reader.ReadString();
                }

                if (reader.ReadByte() != 0)
                {
                    assemblyName = reader.ReadString();
                }

                loggerAssembly = new AssemblyLoadInfo(assemblyName, assemblyFile);
            }
            #endregion
            verbosity = (LoggerVerbosity)reader.ReadInt32();
            loggerId = reader.ReadInt32();
        }
        #endregion
    }
}

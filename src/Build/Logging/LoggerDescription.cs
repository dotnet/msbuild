// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Text;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.BackEnd;

using InternalLoggerException = Microsoft.Build.Exceptions.InternalLoggerException;
using System.Linq;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// This class is used to contain information about a logger as a collection of values that
    /// can be used to instantiate the logger and can be serialized to be passed between different
    /// processes.
    /// </summary>
    public class LoggerDescription : ITranslatable
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
        ) : this(loggerClassName,
            loggerAssemblyName,
            loggerAssemblyFile,
            loggerSwitchParameters,
            verbosity,
            isOptional: false)
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
            LoggerVerbosity verbosity,
            bool isOptional
        )
        {
            _loggerClassName = loggerClassName;

            if (loggerAssemblyFile != null && !Path.IsPathRooted(loggerAssemblyFile))
            {
                loggerAssemblyFile = FileUtilities.NormalizePath(loggerAssemblyFile);
            }

            _loggerAssembly = AssemblyLoadInfo.Create(loggerAssemblyName, loggerAssemblyFile);
            _loggerSwitchParameters = loggerSwitchParameters;
            _verbosity = verbosity;
            _isOptional = isOptional;
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
                return _loggerId;
            }
            set
            {
                _loggerId = value;
            }
        }

        /// <summary>
        /// This property generates the logger name by appending together the class name and assembly name
        /// </summary>
        internal string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(_loggerClassName) &&
                    !string.IsNullOrEmpty(_loggerAssembly.AssemblyFile))
                {
                    return _loggerClassName + ":" + _loggerAssembly.AssemblyFile;
                }
                else if (!string.IsNullOrEmpty(_loggerClassName))
                {
                    return _loggerClassName;
                }
                else
                {
                    return _loggerAssembly.AssemblyFile;
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
                return _loggerSwitchParameters;
            }
        }

        public bool IsOptional
        {
            get
            {
                return _isOptional;
            }
        }

        /// <summary>
        /// Return the verbosity for this logger (from command line all loggers get same verbosity)
        /// </summary>
        public LoggerVerbosity Verbosity
        {
            get
            {
                return _verbosity;
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
            IForwardingLogger forwardingLogger = null;
            try
            {
                forwardingLogger = (IForwardingLogger)CreateLogger(true);

                // Check if the class was not found in the assembly
                if (forwardingLogger == null)
                {
                    InternalLoggerException.Throw(null, null, "LoggerNotFoundError", true, this.Name);
                }
            }
            catch (Exception e /* Wrap all other exceptions in a more meaningful exception*/)
            {
                // Two of the possible exceptions are already in reasonable exception types
                if (e is LoggerException /* Polite logger Failure*/ || e is InternalLoggerException /* LoggerClass not found*/)
                {
                    throw;
                }
                else
                {
                    if (ExceptionHandling.IsCriticalException(e))
                    {
                        throw;
                    }

                    InternalLoggerException.Throw(e, null, "LoggerCreationError", true, Name);
                }
            }

            return forwardingLogger;
        }

        /// <summary>
        /// Create an ILogger out of the data in this description. This method may throw a variety of
        /// reflection exceptions if the data is invalid. It is the resposibility of the caller to handle these
        /// exceptions if desired.
        /// </summary>
        /// <returns></returns>
        public ILogger CreateLogger()
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
                    LoadedType loggerClass = (new TypeLoader(s_forwardingLoggerClassFilter)).Load(_loggerClassName, _loggerAssembly);

                    if (loggerClass != null)
                    {
                        // instantiate the logger
                        logger = (IForwardingLogger)Activator.CreateInstance(loggerClass.Type);
                    }
                }
                else
                {
                    // load the logger from its assembly
                    LoadedType loggerClass = (new TypeLoader(s_loggerClassFilter)).Load(_loggerClassName, _loggerAssembly);

                    if (loggerClass != null)
                    {
                        // instantiate the logger
                        logger = (ILogger)Activator.CreateInstance(loggerClass.Type);
                    }
                }
            }
            catch (InvalidCastException e)
            {
                // The logger when trying to load has hit an invalid case, this is usually due to the framework assembly being a different version
                string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("LoggerInstantiationFailureErrorInvalidCast", _loggerClassName, _loggerAssembly.AssemblyLocation, e.Message);
                throw new LoggerException(message, e.InnerException);
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
        private static readonly Func<Type, object, bool> s_forwardingLoggerClassFilter = IsForwardingLoggerClass;

        /// <summary>
        /// Used for finding loggers when reflecting through assemblies.
        /// </summary>
        private static readonly Func<Type, object, bool> s_loggerClassFilter = IsLoggerClass;

        /// <summary>
        /// Checks if the given type is a logger class.
        /// </summary>
        /// <remarks>This method is used as a Type Filter delegate.</remarks>
        /// <returns>true, if specified type is a logger</returns>
        private static bool IsForwardingLoggerClass(Type type, object unused)
        {
            return (type.GetTypeInfo().IsClass &&
                !type.GetTypeInfo().IsAbstract &&
#if FEATURE_TYPE_GETINTERFACE
                (type.GetTypeInfo().GetInterface("IForwardingLogger") != null));
#else
                (type.GetInterfaces().Any(interfaceType => interfaceType.Name == "IForwardingLogger")));
#endif
        }

        /// <summary>
        /// Checks if the given type is a logger class.
        /// </summary>
        /// <remarks>This method is used as a TypeFilter delegate.</remarks>
        /// <returns>true, if specified type is a logger</returns>
        private static bool IsLoggerClass(Type type, object unused)
        {
            return (type.GetTypeInfo().IsClass &&
                !type.GetTypeInfo().IsAbstract &&
#if FEATURE_TYPE_GETINTERFACE
                (type.GetTypeInfo().GetInterface("ILogger") != null));
#else
                (type.GetInterfaces().Any(interfaceType => interfaceType.Name == "ILogger")));
#endif
        }

        /// <summary>
        /// Converts the path to the logger assembly to a full path
        /// </summary>
        internal void ConvertPathsToFullPaths()
        {
            if (_loggerAssembly.AssemblyFile != null)
            {
                _loggerAssembly =
                    AssemblyLoadInfo.Create(_loggerAssembly.AssemblyName, Path.GetFullPath(_loggerAssembly.AssemblyFile));
            }
        }

        #endregion

        #region Data
        private string _loggerClassName;
        private string _loggerSwitchParameters;
        private AssemblyLoadInfo _loggerAssembly;
        private LoggerVerbosity _verbosity;
        private int _loggerId;
        private bool _isOptional;
        #endregion

        #region CustomSerializationToStream
        internal void WriteToStream(BinaryWriter writer)
        {
            writer.WriteOptionalString(_loggerClassName);
            writer.WriteOptionalString(_loggerSwitchParameters);

            if (_loggerAssembly == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);

                writer.WriteOptionalString(_loggerAssembly.AssemblyFile);
                writer.WriteOptionalString(_loggerAssembly.AssemblyName);
            }

            writer.Write((Int32)_verbosity);
            writer.Write((Int32)_loggerId);
        }

        internal void CreateFromStream(BinaryReader reader)
        {
            _loggerClassName = reader.ReadByte() == 0 ? null : reader.ReadString();
            _loggerSwitchParameters = reader.ReadByte() == 0 ? null : reader.ReadString();

            if (reader.ReadByte() == 0)
            {
                _loggerAssembly = null;
            }
            else
            {
                string assemblyFile = reader.ReadByte() == 0 ? null : reader.ReadString();
                string assemblyName = reader.ReadByte() == 0 ? null : reader.ReadString();

                _loggerAssembly = AssemblyLoadInfo.Create(assemblyName, assemblyFile);
            }

            _verbosity = (LoggerVerbosity)reader.ReadInt32();
            _loggerId = reader.ReadInt32();
        }
        #endregion

        #region INodePacketTranslatable Members

        void ITranslatable.Translate(ITranslator translator)
        {
            translator.Translate(ref _loggerClassName);
            translator.Translate(ref _loggerSwitchParameters);
            translator.Translate(ref _loggerAssembly, AssemblyLoadInfo.FactoryForTranslation);
            translator.TranslateEnum(ref _verbosity, (int)_verbosity);
            translator.Translate(ref _loggerId);
        }

        static internal LoggerDescription FactoryForTranslation(ITranslator translator)
        {
            LoggerDescription description = new LoggerDescription();
            ((ITranslatable)description).Translate(translator);
            return description;
        }

        #endregion
    }
}

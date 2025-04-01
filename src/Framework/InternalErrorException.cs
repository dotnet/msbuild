// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using Microsoft.Build.Framework.BuildException;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This exception is to be thrown whenever an assumption we have made in the code turns out to be false. Thus, if this
    /// exception ever gets thrown, it is because of a bug in our own code, not because of something the user or project author
    /// did wrong.
    /// </summary>
    [Serializable]
    internal sealed class InternalErrorException : BuildExceptionBase
    {
        /// <summary>
        /// Default constructor.
        /// SHOULD ONLY BE CALLED BY DESERIALIZER.
        /// SUPPLY A MESSAGE INSTEAD.
        /// </summary>
        internal InternalErrorException() : base()
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this exception using the given message.
        /// </summary>
        internal InternalErrorException(
            String message) :
            base("MSB0001: Internal MSBuild Error: " + message)
        {
            ConsiderDebuggerLaunch(message, null);
        }

        /// <summary>
        /// Creates an instance of this exception using the given message and inner exception.
        /// Adds the inner exception's details to the exception message because most bug reporters don't bother
        /// to provide the inner exception details which is typically what we care about.
        /// </summary>
        internal InternalErrorException(
            String message,
            Exception innerException) :
            this(message, innerException, false)
        { }

        internal static InternalErrorException CreateFromRemote(string message, Exception innerException)
        {
            return new InternalErrorException(message, innerException, true /* calledFromDeserialization */);
        }

        private InternalErrorException(string message, Exception innerException, bool calledFromDeserialization)
            : base(
                calledFromDeserialization
                    ? message
                    : "MSB0001: Internal MSBuild Error: " + message + (innerException == null
                        ? String.Empty
                        : $"\n=============\n{innerException}\n\n"),
                innerException)
        {
            if (!calledFromDeserialization)
            {
                ConsiderDebuggerLaunch(message, innerException);
            }
        }

        #region Serialization (update when adding new class members)

        /// <summary>
        /// Private constructor used for (de)serialization. The constructor is private as this class is sealed
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        private InternalErrorException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            // Do nothing: no fields
        }

        // Base implementation of GetObjectData() is sufficient; we have no fields
        #endregion

        #region ConsiderDebuggerLaunch
        /// <summary>
        /// A fatal internal error due to a bug has occurred. Give the dev a chance to debug it, if possible.
        ///
        /// Will in all cases launch the debugger, if the environment variable "MSBUILDLAUNCHDEBUGGER" is set.
        ///
        /// In DEBUG build, will always launch the debugger, unless we are in razzle (_NTROOT is set) or in NUnit,
        /// or MSBUILDDONOTLAUNCHDEBUGGER is set (that could be useful in suite runs).
        /// We don't launch in retail or LKG so builds don't jam; they get a callstack, and continue or send a mail, etc.
        /// We don't launch in NUnit as tests often intentionally cause InternalErrorExceptions.
        ///
        /// Because we only call this method from this class, just before throwing an InternalErrorException, there is
        /// no danger that this suppression will cause a bug to only manifest itself outside NUnit
        /// (which would be most unfortunate!). Do not make this non-private.
        ///
        /// Unfortunately NUnit can't handle unhandled exceptions like InternalErrorException on anything other than
        /// the main test thread. However, there's still a callstack displayed before it quits.
        ///
        /// If it is going to launch the debugger, it first does a Debug.Fail to give information about what needs to
        /// be debugged -- the exception hasn't been thrown yet. This automatically displays the current callstack.
        /// </summary>
        private static void ConsiderDebuggerLaunch(string message, Exception innerException)
        {
            string innerMessage = (innerException == null) ? String.Empty : innerException.ToString();

            if (Environment.GetEnvironmentVariable("MSBUILDLAUNCHDEBUGGER") != null)
            {
                LaunchDebugger(message, innerMessage);
                return;
            }

#if DEBUG
            if (!RunningTests() && Environment.GetEnvironmentVariable("MSBUILDDONOTLAUNCHDEBUGGER") == null
                && Environment.GetEnvironmentVariable("_NTROOT") == null)
            {
                LaunchDebugger(message, innerMessage);
                return;
            }
#endif
        }

        private static void LaunchDebugger(string message, string innerMessage)
        {
#if FEATURE_DEBUG_LAUNCH
            Debug.Fail(message, innerMessage);
            Debugger.Launch();
#else
            Console.WriteLine("MSBuild Failure: " + message);
            if (!string.IsNullOrEmpty(innerMessage))
            {
                Console.WriteLine(innerMessage);
            }
            Console.WriteLine("Waiting for debugger to attach to process: " + Process.GetCurrentProcess().Id);
            while (!Debugger.IsAttached)
            {
                System.Threading.Thread.Sleep(100);
            }
#endif
        }
        #endregion

        private static bool RunningTests() => BuildEnvironmentState.s_runningTests;
    }
}

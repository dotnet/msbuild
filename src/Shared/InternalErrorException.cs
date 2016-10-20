// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary> Exception to be thrown whenever an assumption we have made 
//           in the code turns out to be false.</summary>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
#if FEATURE_BINARY_SERIALIZATION
using System.Runtime.Serialization;
#endif

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This exception is to be thrown whenever an assumption we have made in the code turns out to be false. Thus, if this
    /// exception ever gets thrown, it is because of a bug in our own code, not because of something the user or project author
    /// did wrong.
    /// 
    /// !~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~
    /// WARNING: When this file is shared into multiple assemblies each assembly will view this as a different type.
    ///          Don't throw this exception from one assembly and catch it in another.
    /// !~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~!~
    ///     
    /// </summary>
#if FEATURE_BINARY_SERIALIZATION
    [Serializable]
#endif
    internal sealed class InternalErrorException : Exception
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
        internal InternalErrorException
        (
            String message
        ) :
            base("MSB0001: Internal MSBuild Error: " + message)
        {
            ConsiderDebuggerLaunch(message, null);
        }

        /// <summary>
        /// Creates an instance of this exception using the given message and inner exception.
        /// Adds the inner exception's details to the exception message because most bug reporters don't bother
        /// to provide the inner exception details which is typically what we care about.
        /// </summary>
        internal InternalErrorException
        (
            String message,
            Exception innerException
        ) :
            base("MSB0001: Internal MSBuild Error: " + message + (innerException == null ? String.Empty : ("\n=============\n" + innerException.ToString() + "\n\n")), innerException)
        {
            ConsiderDebuggerLaunch(message, innerException);
        }

#if FEATURE_BINARY_SERIALIZATION
        #region Serialization (update when adding new class members)

        /// <summary>
        /// Private constructor used for (de)serialization. The constructor is private as this class is sealed
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
        private InternalErrorException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            // Do nothing: no fields
        }

        // Base implementation of GetObjectData() is sufficient; we have no fields
        #endregion
#endif

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

        private static bool RunningTests()
        {
            // Copied logic from BuildEnvironmentHelper. Removed reference for single use due
            // to additional dependencies. Update both if needed.
            string[] testRunners =
            {
                "XUNIT", "NUNIT", "MSTEST", "VSTEST", "TASKRUNNER", "VSTESTHOST", "QTAGENT32",
                "CONCURRENT", "RESHARPER", "MDHOST", "TE.PROCESSHOST"
            };

            string[] testAssemblies =
            {
                "Microsoft.Build.Tasks.UnitTests", "Microsoft.Build.Engine.UnitTests",
                "Microsoft.Build.Utilities.UnitTests", "Microsoft.Build.CommandLine.UnitTests",
                "Microsoft.Build.Engine.OM.UnitTests", "Microsoft.Build.Framework.UnitTests"
            };

#if FEATURE_GET_COMMANDLINE
            var processNameCommandLine = Environment.GetCommandLineArgs()[0];
#else
            string processNameCommandLine = null;
#endif
            var processNameCurrentProcess = Process.GetCurrentProcess().MainModule.FileName;

            // First check if we're running in a known test runner.
            if (IsProcessInList(processNameCommandLine, testRunners) ||
                IsProcessInList(processNameCurrentProcess, testRunners))
            {
#if FEATURE_APPDOMAIN
                // If we are, then ensure we're running MSBuild's tests by seeing if any of our assemblies are loaded.
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (testAssemblies.Any(item => item.Equals(assembly.GetName().Name, StringComparison.InvariantCultureIgnoreCase)))
                        return true;
                }
#else
                return true;
#endif
            }

            return false;
        }

        private static bool IsProcessInList(string processName, string[] processList)
        {
            return processList.Any(s => Path.GetFileNameWithoutExtension(processName)?.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}

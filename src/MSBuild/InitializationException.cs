﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

#if FEATURE_SECURITY_PERMISSIONS
using System.Security.Permissions;
#endif

using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// This exception is used to flag failures in application initialization, either due to invalid parameters on the command
    /// line, or because the application was invoked in an invalid context.
    /// </summary>
    /// <remarks>
    /// Unlike the CommandLineSwitchException, this exception is NOT thrown for syntax errors in switches.
    /// </remarks>
    [Serializable]
    internal sealed class InitializationException : Exception
    {
        /// <summary>
        /// This constructor initializes the exception message.
        /// </summary>
        /// <param name="message"></param>
        private InitializationException(
            string message) :
            base(message)
        {
            // do nothing
        }

        /// <summary>
        /// This constructor initializes the exception message and saves the switch that caused the initialization failure.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="invalidSwitch">Can be null.</param>
        private InitializationException(
            string message,
            string invalidSwitch) :
            this(message)
        {
            this.invalidSwitch = invalidSwitch;
        }

        /// <summary>
        /// Serialization constructor
        /// </summary>
#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        private InitializationException(
            SerializationInfo info,
            StreamingContext context) :
            base(info, context)
        {
            ErrorUtilities.VerifyThrowArgumentNull(info, nameof(info));

            invalidSwitch = info.GetString("invalidSwitch");
        }

        /// <summary>
        /// Gets the error message and the invalid switch, or only the error message if no invalid switch is set.
        /// </summary>
        public override string Message
        {
            get
            {
                if (invalidSwitch == null)
                {
                    return base.Message;
                }
                else
                {
                    return base.Message + Environment.NewLine + ResourceUtilities.FormatResourceStringStripCodeAndKeyword("InvalidSwitchIndicator", invalidSwitch);
                }
            }
        }

        // the invalid switch causing this exception (can be null)
        private string invalidSwitch;

        /// <summary>
        /// Serialize the contents of the class.
        /// </summary>
#if FEATURE_SECURITY_PERMISSIONS
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
#endif
#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("invalidSwitch", invalidSwitch, typeof(string));
        }

        /// <summary>
        /// Throws the exception if the specified condition is not met.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="messageResourceName"></param>
        internal static void VerifyThrow(bool condition, string messageResourceName)
        {
            VerifyThrow(condition, messageResourceName, null);
        }

        /// <summary>
        /// Throws the exception if the specified condition is not met.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="messageResourceName"></param>
        /// <param name="invalidSwitch"></param>
        internal static void VerifyThrow(bool condition, string messageResourceName, string invalidSwitch)
        {
            if (!condition)
            {
                Throw(messageResourceName, invalidSwitch, null, false);
            }
#if DEBUG
            else
            {
                ResourceUtilities.VerifyResourceStringExists(messageResourceName);
            }
#endif
        }

        /// <summary>
        /// Throws the exception using the given exception context.
        /// </summary>
        /// <param name="messageResourceName"></param>
        /// <param name="invalidSwitch"></param>
        /// <param name="e"></param>
        /// <param name="showStackTrace"></param>
        internal static void Throw(string messageResourceName, string invalidSwitch, Exception e, bool showStackTrace)
        {
            string errorMessage = AssemblyResources.GetString(messageResourceName);

            ErrorUtilities.VerifyThrow(errorMessage != null, "The resource string must exist.");

            if (showStackTrace && e != null)
            {
                errorMessage += Environment.NewLine + e.ToString();
            }
            else
            {
                // the exception message can contain a format item i.e. "{0}" to hold the given exception's message
                errorMessage = ResourceUtilities.FormatString(errorMessage, (e == null) ? String.Empty : e.Message);
            }

            InitializationException.Throw(errorMessage, invalidSwitch);
        }

        /// <summary>
        /// Throws the exception if the specified condition is not met.
        /// </summary>
        internal static void VerifyThrow(bool condition, string messageResourceName, string invalidSwitch, params object[] args)
        {
            if (!condition)
            {
                string errorMessage = AssemblyResources.GetString(messageResourceName);

                ErrorUtilities.VerifyThrow(errorMessage != null, "The resource string must exist.");

                errorMessage = ResourceUtilities.FormatString(errorMessage, args);

                InitializationException.Throw(errorMessage, invalidSwitch);
            }
        }

        /// <summary>
        /// Throws the exception using the given exception context.
        /// </summary>
        /// <param name="messageResourceName"></param>
        /// <param name="invalidSwitch"></param>
        /// <param name="e"></param>
        /// <param name="showStackTrace"></param>
        internal static void Throw(string message, string invalidSwitch)
        {
            ErrorUtilities.VerifyThrow(message != null, "The string must exist.");
            throw new InitializationException(message, invalidSwitch);
        }
    }
}

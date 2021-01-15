// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.Permissions;
#endif

using Microsoft.Build.Shared;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Exceptions
{
    /// <summary>
    /// This exception is used to wrap an unhandled exception from a logger. This exception aborts the build, and it can only be
    /// thrown by the MSBuild engine.
    /// </summary>
    // WARNING: marking a type [Serializable] without implementing ISerializable imposes a serialization contract -- it is a
    // promise to never change the type's fields i.e. the type is immutable; adding new fields in the next version of the type
    // without following certain special FX guidelines, can break both forward and backward compatibility
    [Serializable]
    public sealed class InternalLoggerException : Exception
    {
        #region Unusable constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <remarks>
        /// This constructor only exists to satisfy .NET coding guidelines. Use the rich constructor instead.
        /// </remarks>
        /// <exception cref="InvalidOperationException"></exception>
        public InternalLoggerException()
        {
            ErrorUtilities.VerifyThrowInvalidOperation(false, "InternalLoggerExceptionOnlyThrownByEngine");
        }

        /// <summary>
        /// Creates an instance of this exception using the specified error message.
        /// </summary>
        /// <remarks>
        /// This constructor only exists to satisfy .NET coding guidelines. Use the rich constructor instead.
        /// </remarks>
        /// <param name="message"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public InternalLoggerException(string message)
            : base(message)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(false, "InternalLoggerExceptionOnlyThrownByEngine");
        }

        /// <summary>
        /// Creates an instance of this exception using the specified error message and inner exception.
        /// </summary>
        /// <remarks>
        /// This constructor only exists to satisfy .NET coding guidelines. Use the rich constructor instead.
        /// </remarks>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public InternalLoggerException(string message, Exception innerException)
            : base(message, innerException)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(false, "InternalLoggerExceptionOnlyThrownByEngine");
        }

        #endregion

        /// <summary>
        /// Creates an instance of this exception using rich error information.
        /// Internal for unit testing
        /// </summary>
        /// <remarks>This is the only usable constructor.</remarks>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        /// <param name="e">Can be null.</param>
        /// <param name="errorCode"></param>
        /// <param name="helpKeyword"></param>
        /// <param name="initializationException"></param>
        internal InternalLoggerException
        (
            string message,
            Exception innerException,
            BuildEventArgs e,
            string errorCode,
            string helpKeyword,
            bool initializationException
         )
            : base(message, innerException)
        {
            ErrorUtilities.VerifyThrow(!string.IsNullOrEmpty(message), "Need error message.");
            ErrorUtilities.VerifyThrow(innerException != null || initializationException, "Need the logger exception.");
            ErrorUtilities.VerifyThrow(!string.IsNullOrEmpty(errorCode), "Must specify the error message code.");
            ErrorUtilities.VerifyThrow(!string.IsNullOrEmpty(helpKeyword), "Must specify the help keyword for the IDE.");
            
            this.e = e;
            this.errorCode = errorCode;
            this.helpKeyword = helpKeyword;
            this.initializationException = initializationException;
        }

        #region Serialization (update when adding new class members)

        /// <summary>
        /// Protected constructor used for (de)serialization.
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        private InternalLoggerException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            e = (BuildEventArgs)info.GetValue("e", typeof(BuildEventArgs));
            errorCode = info.GetString("errorCode");
            helpKeyword = info.GetString("helpKeyword");
            initializationException = info.GetBoolean("initializationException");
        }

        /// <summary>
        /// ISerializable method which we must override since Exception implements this interface
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
#if FEATURE_SECURITY_PERMISSIONS
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
#endif
        override public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("e", e);
            info.AddValue("errorCode", errorCode);
            info.AddValue("helpKeyword", helpKeyword);
            info.AddValue("initializationException", initializationException);
        }

        /// <summary>
        /// Provide default values for optional members
        /// </summary>
        [OnDeserializing] // Will happen before the object is deserialized
        private void SetDefaultsBeforeSerialization(StreamingContext sc)
        {
            initializationException = false;
        }

        /// <summary>
        /// Don't actually have anything to do in the method, but the method is required when implementing an optional field
        /// </summary>
        [OnDeserialized]
        private void SetValueAfterDeserialization(StreamingContext sx)
        {
            // Have nothing to do
        }
        #endregion

        #region Properties

        /// <summary>
        /// Gets the details of the build event (if any) that was being logged.
        /// </summary>
        /// <value>The build event args, or null.</value>
        public BuildEventArgs BuildEventArgs
        {
            get
            {
                return e;
            }
        }

        /// <summary>
        /// Gets the error code associated with this exception's message (not the inner exception).
        /// </summary>
        /// <value>The error code string.</value>
        public string ErrorCode
        {
            get
            {
                return errorCode;
            }
        }

        /// <summary>
        /// Gets the F1-help keyword associated with this error, for the host IDE.
        /// </summary>
        /// <value>The keyword string.</value>
        public string HelpKeyword
        {
            get
            {
                return helpKeyword;
            }
        }

        /// <summary>
        /// True if the exception occurred during logger initialization
        /// </summary>
        public bool InitializationException
        {
            get
            {
                return initializationException;
            }
        }

        #endregion

        /// <summary>
        /// Throws an instance of this exception using rich error information.
        /// </summary>
        /// <param name="innerException"></param>
        /// <param name="e">Can be null.</param>
        /// <param name="messageResourceName"></param>
        /// <param name="initializationException"></param>
        /// <param name="messageArgs"></param>
        internal static void Throw
        (
            Exception innerException,
            BuildEventArgs e,
            string messageResourceName,
            bool initializationException,
            params string[] messageArgs
        )
        {
            ErrorUtilities.VerifyThrow(messageResourceName != null, "Need error message.");

            string errorCode;
            string helpKeyword;
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out errorCode, out helpKeyword, messageResourceName, messageArgs);

            throw new InternalLoggerException(message, innerException, e, errorCode, helpKeyword, initializationException);
        }

        // the event that was being logged when a logger failed (can be null)
        private BuildEventArgs e;
        // the error code for this exception's message (not the inner exception)
        private string errorCode;
        // the F1-help keyword for the host IDE
        private string helpKeyword;

        // This flag is set to indicate that the exception occurred during logger initialization
        [OptionalField(VersionAdded = 2)]
        private bool initializationException;
    }
}

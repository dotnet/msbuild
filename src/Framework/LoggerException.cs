// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.Permissions; // for SecurityPermissionAttribute
#endif

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Exception that should be thrown by a logger when it cannot continue.
    /// Allows a logger to force the build to stop in an explicit way, when, for example, it 
    /// receives invalid parameters, or cannot write to disk.
    /// </summary>
    /// <remarks>
    /// WARNING: marking a type [Serializable] without implementing ISerializable imposes a serialization contract -- it is a
    /// promise to never change the type's fields i.e. the type is immutable; adding new fields in the next version of the type
    /// without following certain special FX guidelines, can break both forward and backward compatibility
    /// </remarks>
    [Serializable]
    public class LoggerException : Exception
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <remarks>
        /// This constructor only exists to satisfy .NET coding guidelines. Use the rich constructor instead.
        /// </remarks>
        public LoggerException()
        {
            // do nothing
            // if message is null, the base class provides a default string.
        }

        /// <summary>
        /// Creates an instance of this exception using the specified error message.
        /// </summary>
        /// <param name="message">Message string</param>
        public LoggerException(string message)
            : base(message, null)
        {
            // We do no verification of these parameters.
            // if message is null, the base class provides a default string.
        }

        /// <summary>
        /// Creates an instance of this exception using the specified error message and inner exception.
        /// </summary>
        /// <param name="message">Message string</param>
        /// <param name="innerException">Inner exception. Can be null</param>
        public LoggerException(string message, Exception innerException)
            : base(message, innerException)
        {
            // We do no verification of these parameters. Any can be null;
            // if message is null, the base class provides a default string.
        }

        /// <summary>
        /// Creates an instance of this exception using rich error information.
        /// </summary>
        /// <param name="message">Message string</param>
        /// <param name="innerException">Inner exception. Can be null</param>
        /// <param name="errorCode">Error code</param>
        /// <param name="helpKeyword">Help keyword for host IDE. Can be null</param>
        public LoggerException(string message, Exception innerException, string errorCode, string helpKeyword)
            : this(message, innerException)
        {
            // We do no verification of these parameters. Any can be null.
            this.errorCode = errorCode;
            this.helpKeyword = helpKeyword;
        }

        #region Serialization (update when adding new class members)

        /// <summary>
        /// Protected constructor used for (de)serialization. 
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
        /// <param name="info">Serialization info</param>
        /// <param name="context">Streaming context</param>
        protected LoggerException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            errorCode = info.GetString("errorCode");
            helpKeyword = info.GetString("helpKeyword");
        }

        /// <summary>
        /// ISerializable method which we must override since Exception implements this interface
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
        /// <param name="info">Serialization info</param>
        /// <param name="context">Streaming context</param>
#if FEATURE_SECURITY_PERMISSIONS
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
#endif
        override public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("errorCode", errorCode);
            info.AddValue("helpKeyword", helpKeyword);
        }

        #endregion

        #region Properties

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

        #endregion

        // the error code for this exception's message (not the inner exception)
        private string errorCode;
        // the F1-help keyword for the host IDE
        private string helpKeyword;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework.BuildException;
using Microsoft.Build.Shared;
using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.Permissions;
#endif

#nullable disable

namespace Microsoft.Build.Exceptions
{
    /// <summary>
    /// Exception subclass that ToolsetReaders should throw.
    /// </summary>
    [Serializable]
    public class InvalidToolsetDefinitionException : BuildExceptionBase
    {
        /// <summary>
        /// The MSBuild error code corresponding with this exception.
        /// </summary>
        private string errorCode = null;

        /// <summary>
        /// Basic constructor.
        /// </summary>
        public InvalidToolsetDefinitionException()
            : base()
        {
        }

        /// <summary>
        /// Basic constructor.
        /// </summary>
        /// <param name="message"></param>
        public InvalidToolsetDefinitionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Basic constructor.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public InvalidToolsetDefinitionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Basic constructor.
        /// </summary>
#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        protected InvalidToolsetDefinitionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ErrorUtilities.VerifyThrowArgumentNull(info, nameof(info));

            errorCode = info.GetString("errorCode");
        }

        /// <summary>
        /// Constructor that takes an MSBuild error code
        /// </summary>
        /// <param name="message"></param>
        /// <param name="errorCode"></param>
        public InvalidToolsetDefinitionException(string message, string errorCode)
            : base(message)
        {
            this.errorCode = errorCode;
        }

        /// <summary>
        /// Constructor that takes an MSBuild error code
        /// </summary>
        /// <param name="message"></param>
        /// <param name="errorCode"></param>
        /// <param name="innerException"></param>
        public InvalidToolsetDefinitionException(string message, string errorCode, Exception innerException)
            : base(message, innerException)
        {
            this.errorCode = errorCode;
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
#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ErrorUtilities.VerifyThrowArgumentNull(info, nameof(info));

            base.GetObjectData(info, context);

            info.AddValue("errorCode", errorCode);
        }

        protected override IDictionary<string, string> FlushCustomState()
        {
            return new Dictionary<string, string>()
            {
                { nameof(errorCode), errorCode }
            };
        }

        protected override void InitializeCustomState(IDictionary<string, string> state)
        {
            errorCode = state[nameof(errorCode)];
        }

        /// <summary>
        /// The MSBuild error code corresponding with this exception, or
        /// null if none was specified.
        /// </summary>
        public string ErrorCode
        {
            get
            {
                return errorCode;
            }
        }

        #region Static Throw Helpers

        /// <summary>
        /// Throws an InvalidToolsetDefinitionException.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments
        /// is expensive, because memory is allocated for the array of arguments -- do
        /// not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <param name="resourceName"></param>
        /// <param name="args"></param>
        internal static void Throw(
            string resourceName,
            params string[] args)
        {
            Throw(null, resourceName, args);
        }

        /// <summary>
        /// Throws an InvalidToolsetDefinitionException including a specified inner exception,
        /// which may be interesting to hosts.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments
        /// is expensive, because memory is allocated for the array of arguments -- do
        /// not call this method repeatedly in performance-critical scenarios
        /// </summary>
        internal static void Throw(
            Exception innerException,
            string resourceName,
            params string[] args)
        {
            ResourceUtilities.VerifyResourceStringExists(resourceName);
            string errorCode;
            string helpKeyword;
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out errorCode, out helpKeyword, resourceName, (object[])args);

            throw new InvalidToolsetDefinitionException(message, errorCode, innerException);
        }

        #endregion
    }
}

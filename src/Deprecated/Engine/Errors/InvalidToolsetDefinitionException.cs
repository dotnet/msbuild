// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;

using Microsoft.Build.BuildEngine.Shared;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
    /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
    /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
    /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
    /// 
    /// Exception subclass that ToolsetReaders should throw.
    /// </summary>
    /// <remarks>
    /// <format type="text/markdown"><![CDATA[
    /// ## Remarks
    /// > [!WARNING]
    /// > This class (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
    /// > <xref:Microsoft.Build.Construction>
    /// > <xref:Microsoft.Build.Evaluation>
    /// > <xref:Microsoft.Build.Execution>
    /// ]]></format>
    /// </remarks>
    [Serializable]
    public class InvalidToolsetDefinitionException : Exception
    {
        /// <summary>
        /// The MSBuild error code corresponding with this exception.
        /// </summary>
        private string errorCode = null;

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Basic constructor.
        /// </summary>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public InvalidToolsetDefinitionException()
            : base()
        {
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Basic constructor.
        /// </summary>
        /// <param name="message"></param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public InvalidToolsetDefinitionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Basic constructor.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public InvalidToolsetDefinitionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Basic constructor.
        /// </summary>
        /// <param name="serializationInfo"></param>
        /// <param name="streamingContext"></param>
        protected InvalidToolsetDefinitionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ErrorUtilities.VerifyThrowArgumentNull(info, nameof(info));

            this.errorCode = info.GetString("errorCode");
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// Constructor that takes an MSBuild error code
        /// </summary>
        /// <param name="message"></param>
        /// <param name="errorCode"></param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public InvalidToolsetDefinitionException(string message, string errorCode)
            : base(message)
        {
            this.errorCode = errorCode;
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Constructor that takes an MSBuild error code
        /// </summary>
        /// <param name="message"></param>
        /// <param name="errorCode"></param>
        /// <param name="innerException"></param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public InvalidToolsetDefinitionException(string message, string errorCode, Exception innerException)
            : base(message, innerException)
        {
            this.errorCode = errorCode;
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// 
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// ISerializable method which we must override since Exception implements this interface
        /// If we ever add new members to this class, we'll need to update this.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ErrorUtilities.VerifyThrowArgumentNull(info, nameof(info));

            base.GetObjectData(info, context);

            info.AddValue("errorCode", errorCode);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// The MSBuild error code corresponding with this exception, or
        /// null if none was specified.
        /// </summary>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
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
        internal static void Throw
        (
            string resourceName,
            params object[] args
        )
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
        /// <param name="resourceName"></param>
        /// <param name="args"></param>
        internal static void Throw
        (
            Exception innerException,
            string resourceName,
            params object[] args
        )
        {
#if DEBUG
            ResourceUtilities.VerifyResourceStringExists(resourceName);
#endif
            string errorCode;
            string helpKeyword;
            string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, resourceName, args);

            throw new InvalidToolsetDefinitionException(message, errorCode, innerException);
        }

        #endregion
    }
}

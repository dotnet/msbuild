// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    /// <summary>
    /// A response to an HTTP request
    /// </summary>
    public interface IHttpResponse
    {
        /// <summary>
        /// Gets the status code the server returned
        /// </summary>
        HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets the body of the response
        /// </summary>
        Task<Stream> GetResponseBodyAsync();

        IEnumerable<string> GetHeader(string name);
    }
}

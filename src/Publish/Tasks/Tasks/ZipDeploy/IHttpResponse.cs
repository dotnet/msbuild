using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy.Http
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

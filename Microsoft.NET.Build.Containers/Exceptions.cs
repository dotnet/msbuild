using System.Runtime.Serialization;

namespace Microsoft.NET.Build.Containers;

public class DockerLoadException : Exception
{
    public DockerLoadException()
    {
    }

    public DockerLoadException(string? message) : base(message)
    {
    }

    public DockerLoadException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    protected DockerLoadException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}

public class ContainerHttpException : Exception
{
    private const string errorPrefix = "Containerize: error CONTAINER004:";
    string? jsonResponse;
    string? uri;
    public ContainerHttpException(string message, string? targetUri, string? jsonResp) 
            : base($"{errorPrefix} {message}\nURI: {targetUri ?? "None."}\nJson Response: {jsonResp ?? "None."}" )
    {
        jsonResponse = jsonResp;
        uri = targetUri;
    }
}

namespace Microsoft.NET.Build.Containers;
public class ContainerHttpException : Exception
{
    private const string ErrorPrefix = "Containerize: error CONTAINER004:";
    string? jsonResponse;
    string? uri;
    public ContainerHttpException(string message, string? targetUri, string? jsonResp)
            : base($"{ErrorPrefix} {message}\nURI: {targetUri ?? "Unknown"}\nJson Response: {jsonResp ?? "None."}")
    {
        jsonResponse = jsonResp;
        uri = targetUri;
    }
}
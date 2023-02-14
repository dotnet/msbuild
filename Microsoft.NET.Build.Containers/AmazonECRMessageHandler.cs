// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// A delegating handler that handles the special error handling needed for Amazon ECR.
/// 
/// When pushing images to ECR if the target container repository does not exist ECR ends
/// the connection causing an IOException with a generic "The response ended prematurely."
/// error message. The handler catches the generic error and provides a more informed error
/// message to let the user know they need to create the repository.
/// </summary>
internal sealed class AmazonECRMessageHandler : DelegatingHandler
{
    public AmazonECRMessageHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException e) when (e.InnerException is IOException ioe && ioe.Message.Equals("The response ended prematurely.", StringComparison.OrdinalIgnoreCase))
        {
            var message = "Request to Amazon Elastic Container Registry failed prematurely. This is often caused when the target repository does not exist in the registry.";
            throw new ContainerHttpException(message, request.RequestUri?.ToString(), null);
        }
        catch
        {
            throw;
        }
    }
}

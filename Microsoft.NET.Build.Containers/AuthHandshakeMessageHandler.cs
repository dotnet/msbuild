using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Valleysoft.DockerCredsProvider;

using Microsoft.NET.Build.Containers.Credentials;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// A delegating handler that performs the Docker auth handshake as described <see href="https://docs.docker.com/registry/spec/auth/token/">in their docs</see> if a request isn't authenticated
/// </summary>
public partial class AuthHandshakeMessageHandler : DelegatingHandler
{
    private record AuthInfo(Uri Realm, string Service, string? Scope);

    /// <summary>
    /// the www-authenticate header must have realm, service, and scope information, so this method parses it into that shape if present
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="authInfo"></param>
    /// <returns></returns>
    private static bool TryParseAuthenticationInfo(HttpResponseMessage msg, [NotNullWhen(true)] out string? scheme, [NotNullWhen(true)] out AuthInfo? authInfo)
    {
        authInfo = null;
        scheme = null;

        var authenticateHeader = msg.Headers.WwwAuthenticate;
        if (!authenticateHeader.Any())
        {
            return false;
        }

        AuthenticationHeaderValue header = authenticateHeader.First();
        if (header is { Scheme: "Bearer" or "Basic", Parameter: string bearerArgs })
        {
            scheme = header.Scheme;
            Dictionary<string, string> keyValues = new();
            foreach (Match match in BearerParameterSplitter().Matches(bearerArgs))
            {
                keyValues.Add(match.Groups["key"].Value, match.Groups["value"].Value);
            }

            if (keyValues.TryGetValue("realm", out string? realm) && keyValues.TryGetValue("service", out string? service))
            {
                string? scope = null;
                keyValues.TryGetValue("scope", out scope);
                authInfo = new AuthInfo(new Uri(realm), service, scope);
                return true;
            }
        }

        return false;
    }

    public AuthHandshakeMessageHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

    /// <summary>
    /// Response to a request to get a token using some auth.
    /// </summary>
    /// <remarks>
    /// <see href="https://docs.docker.com/registry/spec/auth/token/#token-response-fields"/>
    /// </remarks>
    private record TokenResponse(string? token, string? access_token, int? expires_in, DateTimeOffset? issued_at)
    {
        public string ResolvedToken => token ?? access_token ?? throw new ArgumentException("Token response had neither token nor access_token.");
    }

    /// <summary>
    /// Uses the authentication information from a 401 response to perform the authentication dance for a given registry.
    /// Credentials for the request are retrieved from the credential provider, then used to acquire a token.
    /// That token is cached for some duration on a per-host basis.
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="service"></param>
    /// <param name="scope"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<AuthenticationHeaderValue?> GetAuthenticationAsync(string scheme, Uri uri, string service, string? scope, CancellationToken cancellationToken)
    {
        // Allow overrides for auth via environment variables
        string? credU = Environment.GetEnvironmentVariable(ContainerHelpers.HostObjectUser);
        string? credP = Environment.GetEnvironmentVariable(ContainerHelpers.HostObjectPass);

        // fetch creds for the host
        DockerCredentials? privateRepoCreds;

        if (!string.IsNullOrEmpty(credU) && !string.IsNullOrEmpty(credP))
        {
            privateRepoCreds = new DockerCredentials(credU, credP);
        }
        else
        {
            try
            {
                privateRepoCreds = await CredsProvider.GetCredentialsAsync(uri.Host);
            }
            catch (Exception e)
            {
                throw new CredentialRetrievalException(uri.Host, e);
            }
        }
        
        if (scheme is "Basic")
        {
            var basicAuth = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{privateRepoCreds.Username}:{privateRepoCreds.Password}")));
            return AuthHeaderCache.AddOrUpdate(uri, basicAuth);
        }
        else if (scheme is "Bearer")
        {
            // use those creds when calling the token provider
            var header = privateRepoCreds.Username == "<token>"
                            ? new AuthenticationHeaderValue("Bearer", privateRepoCreds.Password)
                            : new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{privateRepoCreds.Username}:{privateRepoCreds.Password}")));
            var builder = new UriBuilder(uri);
            var queryDict = System.Web.HttpUtility.ParseQueryString("");
            queryDict["service"] = service;
            if (scope is string s)
            {
                queryDict["scope"] = s;
            }
            builder.Query = queryDict.ToString();
            var message = new HttpRequestMessage(HttpMethod.Get, builder.ToString());
            message.Headers.Authorization = header;

            var tokenResponse = await base.SendAsync(message, cancellationToken);
            tokenResponse.EnsureSuccessStatusCode();

            TokenResponse? token = JsonSerializer.Deserialize<TokenResponse>(tokenResponse.Content.ReadAsStream());
            if (token is null)
            {
                throw new ArgumentException("Could not deserialize token from JSON");
            }

            // save the retrieved token in the cache
            var bearerAuth = new AuthenticationHeaderValue("Bearer", token.ResolvedToken);
            return AuthHeaderCache.AddOrUpdate(uri, bearerAuth);
        }
        else
        {
            return null;
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            throw new ArgumentException("No RequestUri specified", nameof(request));
        }

        // attempt to use cached token for the request if available
        if (AuthHeaderCache.TryGet(request.RequestUri, out AuthenticationHeaderValue? cachedAuthentication))
        {
            request.Headers.Authorization = cachedAuthentication;
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response is { StatusCode: HttpStatusCode.OK })
        {
            return response;
        }
        else if (response is { StatusCode: HttpStatusCode.Unauthorized } && TryParseAuthenticationInfo(response, out string? scheme, out AuthInfo? authInfo))
        {
            if (await GetAuthenticationAsync(scheme, authInfo.Realm, authInfo.Service, authInfo.Scope, cancellationToken) is AuthenticationHeaderValue authentication)
            {
                request.Headers.Authorization = AuthHeaderCache.AddOrUpdate(request.RequestUri, authentication);
                return await base.SendAsync(request, cancellationToken);
            }
            return response;
        }
        else
        {
            return response;
        }
    }

    [GeneratedRegex("(?<key>\\w+)=\"(?<value>[^\"]*)\"(?:,|$)")]
    private static partial Regex BearerParameterSplitter();
}

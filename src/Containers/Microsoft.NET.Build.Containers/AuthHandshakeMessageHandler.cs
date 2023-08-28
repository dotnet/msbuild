// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Credentials;
using Microsoft.NET.Build.Containers.Resources;
using Valleysoft.DockerCredsProvider;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// A delegating handler that performs the Docker auth handshake as described <see href="https://docs.docker.com/registry/spec/auth/token/">in their docs</see> if a request isn't authenticated
/// </summary>
internal sealed partial class AuthHandshakeMessageHandler : DelegatingHandler
{
    private const int MaxRequestRetries = 5; // Arbitrary but seems to work ok for chunked uploads to ghcr.io

    /// <summary>
    /// Unique identifier that is used to tag requests from this library to external registries.
    /// </summary>
    /// <remarks>
    /// Valid characters for this clientID are in the unicode range <see href="https://wintelguy.com/unicode_character_lookup.pl/?str=20-7E">20-7E</see>
    /// </remarks>
    private const string ClientID = "netsdkcontainers";

    private sealed record AuthInfo(string Realm, string? Service, string? Scope);

    private readonly string _registryName;
    private readonly ILogger _logger;
    private static ConcurrentDictionary<string, AuthenticationHeaderValue?> _authenticationHeaders = new();

    public AuthHandshakeMessageHandler(string registryName, HttpMessageHandler innerHandler, ILogger logger) : base(innerHandler)
    {
        _registryName = registryName;
        _logger = logger;
    }

    /// <summary>
    /// the www-authenticate header must have realm, service, and scope information, so this method parses it into that shape if present
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="bearerAuthInfo"></param>
    /// <returns></returns>
    private static bool TryParseAuthenticationInfo(HttpResponseMessage msg, [NotNullWhen(true)] out string? scheme, out AuthInfo? bearerAuthInfo)
    {
        bearerAuthInfo = null;
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
            var keyValues = ParseBearerArgs(bearerArgs);

            var result = scheme switch
            {
                "Bearer" => TryParseBearerAuthInfo(keyValues, out bearerAuthInfo),
                "Basic" => TryParseBasicAuthInfo(keyValues, msg.RequestMessage!.RequestUri!, out bearerAuthInfo),
                _ => false
            };
            return result;
        }
        return false;

        static bool TryParseBearerAuthInfo(Dictionary<string, string> authValues, [NotNullWhen(true)] out AuthInfo? authInfo) {
            if (authValues.TryGetValue("realm", out string? realm))
            {
                string? service = null;
                authValues.TryGetValue("service", out service);
                string? scope = null;
                authValues.TryGetValue("scope", out scope);
                authInfo = new AuthInfo(realm, service, scope);
                return true;
            }
            else {
                authInfo = null;
                return false;
            }
        }

        static bool TryParseBasicAuthInfo(Dictionary<string, string> authValues, Uri requestUri, out AuthInfo? authInfo) {
            authInfo = null;
            return true;
        }

        static Dictionary<string, string> ParseBearerArgs(string bearerHeaderArgs)
        {
            Dictionary<string, string> keyValues = new();
            foreach (Match match in BearerParameterSplitter().Matches(bearerHeaderArgs))
            {
                keyValues.Add(match.Groups["key"].Value, match.Groups["value"].Value);
            }
            return keyValues;
        }
    }

    /// <summary>
    /// Response to a request to get a token using some auth.
    /// </summary>
    /// <remarks>
    /// <see href="https://docs.docker.com/registry/spec/auth/token/#token-response-fields"/>
    /// </remarks>
    private sealed record TokenResponse(string? token, string? access_token, int? expires_in, DateTimeOffset? issued_at)
    {
        public string ResolvedToken => token ?? access_token ?? throw new ArgumentException(Resource.GetString(nameof(Strings.InvalidTokenResponse)));
        public DateTimeOffset ResolvedExpiration {
            get {
                var issueTime = this.issued_at ?? DateTimeOffset.UtcNow; // per spec, if no issued_at use the current time
                var validityDuration = this.expires_in ?? 60; // per spec, if no expires_in use 60 seconds
                var expirationTime = issueTime.AddSeconds(validityDuration);
                return expirationTime;
            }
        }
    }

    /// <summary>
    /// Uses the authentication information from a 401 response to perform the authentication dance for a given registry.
    /// Credentials for the request are retrieved from the credential provider, then used to acquire a token.
    /// That token is cached for some duration determined by the authentication mechanism on a per-host basis.
    /// </summary>
    private async Task<(AuthenticationHeaderValue, DateTimeOffset)?> GetAuthenticationAsync(string registry, string scheme, AuthInfo? bearerAuthInfo, CancellationToken cancellationToken)
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
            privateRepoCreds = await GetLoginCredentials(registry).ConfigureAwait(false);
        }

        if (scheme is "Basic")
        {
            var authValue = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{privateRepoCreds.Username}:{privateRepoCreds.Password}")));
            return new (authValue, DateTimeOffset.MaxValue);
        }
        else if (scheme is "Bearer")
        {
            Debug.Assert(bearerAuthInfo is not null);

            var authenticationValueAndDuration = await TryOAuthPostAsync(privateRepoCreds, bearerAuthInfo, cancellationToken).ConfigureAwait(false);
            if (authenticationValueAndDuration is not null)
            {
                return authenticationValueAndDuration;
            }

            authenticationValueAndDuration = await TryTokenGetAsync(privateRepoCreds, bearerAuthInfo, cancellationToken).ConfigureAwait(false);
            return authenticationValueAndDuration;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Implements the Docker OAuth2 Authentication flow as documented at <see href="https://docs.docker.com/registry/spec/auth/oauth/"/>.
    /// </summary
    private async Task<(AuthenticationHeaderValue, DateTimeOffset)?> TryOAuthPostAsync(DockerCredentials privateRepoCreds, AuthInfo bearerAuthInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri uri = new(bearerAuthInfo.Realm);

        _logger.LogTrace("Attempting to authenticate on {uri} using POST.", uri);
        Dictionary<string, string?> parameters = new()
        {
            ["client_id"] = ClientID,
        };
        if (!string.IsNullOrWhiteSpace(privateRepoCreds.IdentityToken))
        {
            parameters["grant_type"] = "refresh_token";
            parameters["refresh_token"] = privateRepoCreds.IdentityToken;
        }
        else
        {
            parameters["grant_type"] = "password";
            parameters["username"] = privateRepoCreds.Username;
            parameters["password"] = privateRepoCreds.Password;
        }
        if (bearerAuthInfo.Service is not null)
        {
            parameters["service"] = bearerAuthInfo.Service;
        }
        if (bearerAuthInfo.Scope is not null)
        {
            parameters["scope"] = bearerAuthInfo.Scope;
        };
        HttpRequestMessage postMessage = new(HttpMethod.Post, uri)
        {
            Content = new FormUrlEncodedContent(parameters)
        };

        HttpResponseMessage postResponse = await base.SendAsync(postMessage, cancellationToken).ConfigureAwait(false);
        if (!postResponse.IsSuccessStatusCode)
        {
            await postResponse.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
            //return null to try HTTP GET instead
            return null;
        }
        _logger.LogTrace("Received '{statuscode}'.", postResponse.StatusCode);
        TokenResponse? tokenResponse = JsonSerializer.Deserialize<TokenResponse>(postResponse.Content.ReadAsStream(cancellationToken));
        if (tokenResponse is { } tokenEnvelope)
        {
            var authValue = new AuthenticationHeaderValue("Bearer", tokenResponse.ResolvedToken);
            return (authValue, tokenResponse.ResolvedExpiration);
        }
        else
        {
            _logger.LogTrace(Resource.GetString(nameof(Strings.CouldntDeserializeJsonToken)));
            // logging and returning null to try HTTP GET instead
            return null;
        }
    }

    /// <summary>
    /// Implements the Docker Token Authentication flow as documented at <see href="https://docs.docker.com/registry/spec/auth/token/"/>
    /// </summary>
    private async Task<(AuthenticationHeaderValue, DateTimeOffset)?> TryTokenGetAsync(DockerCredentials privateRepoCreds, AuthInfo bearerAuthInfo, CancellationToken cancellationToken)
    {
            // this doesn't seem to be called out in the spec, but actual username/password auth information should be converted into Basic auth here,
            // even though the overall Scheme we're authenticating for is Bearer
            var header = privateRepoCreds.Username == "<token>"
                            ? new AuthenticationHeaderValue("Bearer", privateRepoCreds.Password)
                            : new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{privateRepoCreds.Username}:{privateRepoCreds.Password}")));
            var builder = new UriBuilder(new Uri(bearerAuthInfo.Realm));

            _logger.LogTrace("Attempting to authenticate on {uri} using GET.", bearerAuthInfo.Realm);
            var queryDict = System.Web.HttpUtility.ParseQueryString("");
            if (bearerAuthInfo.Service is string svc)
            {
                queryDict["service"] = svc;
            }
            if (bearerAuthInfo.Scope is string s)
            {
                queryDict["scope"] = s;
            }
            builder.Query = queryDict.ToString();
            var message = new HttpRequestMessage(HttpMethod.Get, builder.ToString());
            message.Headers.Authorization = header;

            var tokenResponse = await base.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                throw new UnableToAccessRepositoryException(registryName);
            }

            TokenResponse? token = JsonSerializer.Deserialize<TokenResponse>(tokenResponse.Content.ReadAsStream(cancellationToken));
            if (token is null)
            {
                throw new ArgumentException(Resource.GetString(nameof(Strings.CouldntDeserializeJsonToken)));
            }
            return (new AuthenticationHeaderValue("Bearer", token.ResolvedToken), token.ResolvedExpiration);
    }

    private static async Task<DockerCredentials> GetLoginCredentials(string registry)
    {
        // For authentication with Docker Hub, 'docker login' uses 'https://index.docker.io/v1/' as the registry key.
        // And 'podman login docker.io' uses 'docker.io'.
        // Try the key used by 'docker' first, and then fall back to the regular case for 'podman'.
        if (registry == ContainerHelpers.DockerRegistryAlias)
        {
            try
            {
                return await CredsProvider.GetCredentialsAsync("https://index.docker.io/v1/").ConfigureAwait(false);
            }
            catch
            { }
        }

        try
        {
            return await CredsProvider.GetCredentialsAsync(registry).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            throw new CredentialRetrievalException(registry, e);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            throw new ArgumentException(Resource.GetString(nameof(Strings.NoRequestUriSpecified)), nameof(request));
        }

        if (_authenticationHeaders.TryGetValue(_registryName, out AuthenticationHeaderValue? header))
        {
            request.Headers.Authorization = header;
        }

        int retryCount = 0;

        while (retryCount < MaxRequestRetries)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response is { StatusCode: HttpStatusCode.OK })
                {
                    return response;
                }
                else if (response is { StatusCode: HttpStatusCode.Unauthorized } && TryParseAuthenticationInfo(response, out string? scheme, out AuthInfo? authInfo))
                {
                    if (await GetAuthenticationAsync(_registryName, scheme, authInfo, cancellationToken).ConfigureAwait(false) is (AuthenticationHeaderValue authHeader, DateTimeOffset expirationTime))
                    {
                        _authenticationHeaders[_registryName] = authHeader;
                        request.Headers.Authorization = authHeader;
                        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    }
                    return response;
                }
                else
                {
                    return response;
                }
            }
            catch (HttpRequestException e) when (e.InnerException is IOException ioe && ioe.InnerException is SocketException se)
            {
                retryCount += 1;
                _logger.LogInformation("Encountered a SocketException with message \"{message}\". Pausing before retry.", se.Message);
                _logger.LogTrace("Exception details: {ex}", se);
                await Task.Delay(TimeSpan.FromSeconds(1.0 * Math.Pow(2, retryCount)), cancellationToken).ConfigureAwait(false);

                // retry
                continue;
            }
        }

        throw new ApplicationException(Resource.GetString(nameof(Strings.TooManyRetries)));
    }

    [GeneratedRegex("(?<key>\\w+)=\"(?<value>[^\"]*)\"(?:,|$)")]
    private static partial Regex BearerParameterSplitter();
}

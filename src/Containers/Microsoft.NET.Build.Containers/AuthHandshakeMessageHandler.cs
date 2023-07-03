// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Valleysoft.DockerCredsProvider;

using Microsoft.NET.Build.Containers.Credentials;
using System.Net.Sockets;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// A delegating handler that performs the Docker auth handshake as described <see href="https://docs.docker.com/registry/spec/auth/token/">in their docs</see> if a request isn't authenticated
/// </summary>
internal sealed partial class AuthHandshakeMessageHandler : DelegatingHandler
{
    private const int MaxRequestRetries = 5; // Arbitrary but seems to work ok for chunked uploads to ghcr.io

    private sealed record AuthInfo(string Realm, string? Service, string? Scope);

    private readonly string _registryName;

    private AuthenticationHeaderValue? _authenticationHeader = null;

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

    public AuthHandshakeMessageHandler(string registryName, HttpMessageHandler innerHandler) : base(innerHandler)
    {
        _registryName = registryName;
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
    private async Task<AuthenticationHeaderValue?> GetAuthenticationAsync(string registry, string scheme, AuthInfo? bearerAuthInfo, CancellationToken cancellationToken)
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
                privateRepoCreds = await CredsProvider.GetCredentialsAsync(registry).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw new CredentialRetrievalException(registry, e);
            }
        }

        if (scheme is "Basic")
        {
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{privateRepoCreds.Username}:{privateRepoCreds.Password}")));
        }
        else if (scheme is "Bearer")
        {
            Debug.Assert(bearerAuthInfo is not null);

            // use those creds when calling the token provider
            var header = privateRepoCreds.Username == "<token>"
                            ? new AuthenticationHeaderValue("Bearer", privateRepoCreds.Password)
                            : new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{privateRepoCreds.Username}:{privateRepoCreds.Password}")));
            var builder = new UriBuilder(new Uri(bearerAuthInfo.Realm));
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
            tokenResponse.EnsureSuccessStatusCode();

            TokenResponse? token = JsonSerializer.Deserialize<TokenResponse>(tokenResponse.Content.ReadAsStream(cancellationToken));
            if (token is null)
            {
                throw new ArgumentException(Resource.GetString(nameof(Strings.CouldntDeserializeJsonToken)));
            }

            return new AuthenticationHeaderValue("Bearer", token.ResolvedToken);
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
            throw new ArgumentException(Resource.GetString(nameof(Strings.NoRequestUriSpecified)), nameof(request));
        }

        if (_authenticationHeader is not null)
        {
            request.Headers.Authorization = _authenticationHeader;
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
                    if (await GetAuthenticationAsync(_registryName, scheme, authInfo, cancellationToken).ConfigureAwait(false) is AuthenticationHeaderValue authentication)
                    {
                        Volatile.Write(ref _authenticationHeader, authentication);
                        request.Headers.Authorization = authentication;
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

                // TODO: log in a way that is MSBuild-friendly
                Console.WriteLine($"Encountered a SocketException with message \"{se.Message}\". Pausing before retry.");

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

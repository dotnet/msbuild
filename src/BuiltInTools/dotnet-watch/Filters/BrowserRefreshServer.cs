// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class BrowserRefreshServer : IAsyncDisposable
    {
        private readonly byte[] ReloadMessage = Encoding.UTF8.GetBytes("Reload");
        private readonly byte[] WaitMessage = Encoding.UTF8.GetBytes("Wait");
        private readonly JsonSerializerOptions _jsonSerializerOptions = new (JsonSerializerDefaults.Web);
        private readonly List<WebSocket> _clientSockets = new();
        private readonly IReporter _reporter;
        private readonly TaskCompletionSource _taskCompletionSource;
        private IHost _refreshServer;

        public BrowserRefreshServer(IReporter reporter)
        {
            _reporter = reporter;
            _taskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public async ValueTask<string> StartAsync(CancellationToken cancellationToken)
        {
            var hostName = Environment.GetEnvironmentVariable("DOTNET_WATCH_AUTO_RELOAD_WS_HOSTNAME") ?? "127.0.0.1";

            _refreshServer = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseKestrel();
                    builder.UseUrls($"http://{hostName}:0");

                    builder.Configure(app =>
                    {
                        app.UseWebSockets();
                        app.Run(WebSocketRequest);
                    });
                })
                .Build();

            await _refreshServer.StartAsync(cancellationToken);

            var serverUrl = _refreshServer.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()
                .Addresses
                .First();

            return serverUrl.Replace("http://", "ws://");
        }

        private async Task WebSocketRequest(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            _clientSockets.Add(await context.WebSockets.AcceptWebSocketAsync());
            await _taskCompletionSource.Task;
        }

        public ValueTask SendJsonSerlialized<TValue>(TValue value, CancellationToken cancellationToken = default)
        {
            var jsonSerialized = JsonSerializer.SerializeToUtf8Bytes(value, _jsonSerializerOptions);
            return SendMessage(jsonSerialized, cancellationToken);
        }

        public async ValueTask SendMessage(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _clientSockets.Count; i++)
            {
                var clientSocket = _clientSockets[i];
                if (clientSocket.CloseStatus.HasValue)
                {
                    continue;
                }

                try
                {
                    await clientSocket.SendAsync(messageBytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                }
                catch (Exception ex)
                {
                    _reporter.Verbose($"Refresh server error: {ex}");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            for (var i = 0; i < _clientSockets.Count; i++)
            {
                var clientSocket = _clientSockets[i];
                await clientSocket.CloseOutputAsync(WebSocketCloseStatus.Empty, null, default);
                clientSocket.Dispose();
            }

            if (_refreshServer != null)
            {
                _refreshServer.Dispose();
            }

            _taskCompletionSource.TrySetResult();
        }

        public ValueTask ReloadAsync(CancellationToken cancellationToken) => SendMessage(ReloadMessage, cancellationToken);

        public ValueTask SendWaitMessageAsync(CancellationToken cancellationToken) => SendMessage(WaitMessage, cancellationToken);
    }
}

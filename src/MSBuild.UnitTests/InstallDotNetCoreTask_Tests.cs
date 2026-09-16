// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using MSBuild.Bootstrap.Utils.Tasks;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class InstallDotNetCoreTask_Tests
    {
        private readonly ITestOutputHelper _output;

        public InstallDotNetCoreTask_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task DownloadScriptAsyncRetriesTransientTransportFailure()
        {
            const string scriptContent = "script content";

            using TestEnvironment testEnvironment = TestEnvironment.Create(_output);
            TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);
            string scriptPath = Path.Combine(folder.Path, "dotnet-install.ps1");
            int requestCount = 0;

            MockEngine engine = new MockEngine(_output);
            using MockHttpMessageHandler handler = new MockHttpMessageHandler((message, token) =>
            {
                requestCount++;
                if (requestCount == 1)
                {
                    throw CreateConnectionResetException();
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(scriptContent),
                    RequestMessage = message
                };
            });
            InstallDotNetCoreTask task = CreateTask(engine, handler);

            await DownloadScriptAsync(task, scriptPath);

            requestCount.ShouldBe(2);
            File.ReadAllText(scriptPath).ShouldBe(scriptContent);
            engine.Errors.ShouldBe(0, engine.Log);
            engine.Log.ShouldContain("Retrying attempt 2 of 3");
        }

        [Fact]
        public async Task DownloadScriptAsyncLogsExhaustedTransientTransportFailure()
        {
            using TestEnvironment testEnvironment = TestEnvironment.Create(_output);
            TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);
            string scriptPath = Path.Combine(folder.Path, "dotnet-install.ps1");
            int requestCount = 0;

            MockEngine engine = new MockEngine(_output);
            using MockHttpMessageHandler handler = new MockHttpMessageHandler((message, token) =>
            {
                requestCount++;
                throw CreateConnectionResetException();
            });
            InstallDotNetCoreTask task = CreateTask(engine, handler);

            await DownloadScriptAsync(task, scriptPath);

            requestCount.ShouldBe(3);
            File.Exists(scriptPath).ShouldBeFalse(scriptPath);
            engine.Errors.ShouldBe(1, engine.Log);
            engine.Log.ShouldContain("failed after 3 attempts");
            engine.Log.ShouldContain("connection reset test");
        }

        [Fact]
        public async Task DownloadScriptAsyncDoesNotRetryPermanentTlsFailure()
        {
            using TestEnvironment testEnvironment = TestEnvironment.Create(_output);
            TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);
            string scriptPath = Path.Combine(folder.Path, "dotnet-install.ps1");
            int requestCount = 0;

            MockEngine engine = new MockEngine(_output);
            using MockHttpMessageHandler handler = new MockHttpMessageHandler((message, token) =>
            {
                requestCount++;
                throw new HttpRequestException("The SSL connection could not be established, see inner exception.", new AuthenticationException("certificate test"));
            });
            InstallDotNetCoreTask task = CreateTask(engine, handler);

            await DownloadScriptAsync(task, scriptPath);

            requestCount.ShouldBe(1);
            File.Exists(scriptPath).ShouldBeFalse(scriptPath);
            engine.Errors.ShouldBe(1, engine.Log);
            engine.Log.ShouldContain("certificate test");
            engine.Log.ShouldNotContain("Retrying attempt");
        }

        private static InstallDotNetCoreTask CreateTask(MockEngine engine, HttpMessageHandler httpMessageHandler)
        {
            InstallDotNetCoreTask task = new InstallDotNetCoreTask
            {
                BuildEngine = engine,
                DotNetInstallBaseUrl = "https://example.test/"
            };

            typeof(InstallDotNetCoreTask)
                .GetProperty("HttpMessageHandler", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(task, httpMessageHandler);

            return task;
        }

        private static Task DownloadScriptAsync(InstallDotNetCoreTask task, string scriptPath)
        {
            return (Task)typeof(InstallDotNetCoreTask)
                .GetMethod("DownloadScriptAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(task, new object[] { "dotnet-install.ps1", scriptPath })!;
        }

        private static HttpRequestException CreateConnectionResetException()
        {
            return new HttpRequestException(
                "The SSL connection could not be established, see inner exception.",
                new IOException("Unable to read data from the transport connection: connection reset test.", new SocketException((int)SocketError.ConnectionReset)));
        }

        private sealed class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _func;

            public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> func)
            {
                _func = func;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_func(request, cancellationToken));
            }
        }
    }
}

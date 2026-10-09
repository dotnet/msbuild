// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
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
        public async Task DownloadScriptAsyncBoundsHttpRequestExceptionRetries()
        {
            using TestEnvironment testEnvironment = TestEnvironment.Create(_output);
            TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);
            string scriptPath = Path.Combine(folder.Path, "dotnet-install.ps1");
            int requestCount = 0;

            MockEngine engine = new MockEngine(_output);
            using MockHttpMessageHandler handler = new MockHttpMessageHandler((message, token) =>
            {
                requestCount++;
                throw new HttpRequestException("request failure test");
            });
            InstallDotNetCoreTask task = CreateTask(engine, handler);

            await DownloadScriptAsync(task, scriptPath);

            requestCount.ShouldBe(3);
            File.Exists(scriptPath).ShouldBeFalse(scriptPath);
            engine.Errors.ShouldBe(1, engine.Log);
            engine.Log.ShouldContain("Retrying attempt 2 of 3");
            engine.Log.ShouldContain("failed after 3 attempts");
            engine.Log.ShouldContain("request failure test");
        }

        [Fact]
        public async Task DownloadScriptAsyncDoesNotRetryHttpStatusFailure()
        {
            using TestEnvironment testEnvironment = TestEnvironment.Create(_output);
            TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);
            string scriptPath = Path.Combine(folder.Path, "dotnet-install.ps1");
            int requestCount = 0;

            MockEngine engine = new MockEngine(_output);
            using MockHttpMessageHandler handler = new MockHttpMessageHandler((message, token) =>
            {
                requestCount++;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    RequestMessage = message
                };
            });
            InstallDotNetCoreTask task = CreateTask(engine, handler);

            await DownloadScriptAsync(task, scriptPath);

            requestCount.ShouldBe(1);
            File.Exists(scriptPath).ShouldBeFalse(scriptPath);
            engine.Errors.ShouldBe(1, engine.Log);
            engine.Log.ShouldContain("Status code: InternalServerError.");
            engine.Log.ShouldNotContain("Retrying attempt");
        }

        [Fact]
        public void PinnedBootstrapSdkReusesInstalledVersionWithoutNetwork()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            MockEngine engine = new(_output);
            using MockHttpMessageHandler handler = new((_, _) => throw new InvalidOperationException("Unexpected network request."));
            InstallDotNetCoreTask task = CreateInstallationTask(env, engine, handler, daily: false);
            Directory.CreateDirectory(Path.Combine(task.InstallDir, "sdk", task.Version));

            task.Execute().ShouldBeTrue(engine.Log);

            task.InstalledSdkVersion.ShouldBe(task.Version);
            File.Exists(Path.Combine(task.DotNetInstallScriptRootPath, "arguments.txt")).ShouldBeFalse();
            engine.Errors.ShouldBe(0, engine.Log);
        }

        [Fact]
        public void PinnedBootstrapSdkInvokesInstallerWithExplicitVersion()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            MockEngine engine = new(_output);
            using MockHttpMessageHandler handler = new((_, _) => throw new InvalidOperationException("Unexpected network request."));
            InstallDotNetCoreTask task = CreateInstallationTask(env, engine, handler, daily: false);

            task.Execute().ShouldBeTrue(engine.Log);

            task.InstalledSdkVersion.ShouldBe(task.Version);
            ReadInstallerArguments(task).ShouldBe([task.Version, "", "", task.InstallDir.TrimEnd(Path.DirectorySeparatorChar)]);
        }

        [Fact]
        public void DailyBootstrapSdkDiscoversMainChannelInsteadOfPinnedOrReleaseChannel()
        {
            const string buildsTable = """
                [win-x64-zip-release]: https://aka.ms/dotnet/10.0.4xx/daily/dotnet-sdk-win-x64.zip
                [win-x64-zip-main]: https://aka.ms/dotnet/12.0.1xx/daily/dotnet-sdk-win-x64.zip
                [linux-targz-main]: https://aka.ms/dotnet/12.0.1xx/daily/dotnet-sdk-linux-x64.tar.gz
                """;

            using TestEnvironment env = TestEnvironment.Create(_output);
            MockEngine engine = new(_output);
            int requests = 0;
            using MockHttpMessageHandler handler = new((request, _) =>
            {
                request.RequestUri!.AbsolutePath.ShouldEndWith("/dotnet/dotnet/main/docs/builds-table.md");
                requests++;
                return CreateResponse(request, buildsTable);
            });
            InstallDotNetCoreTask task = CreateInstallationTask(env, engine, handler, channel: "");
            Directory.CreateDirectory(Path.Combine(task.InstallDir, "sdk", task.Version));

            task.Execute().ShouldBeTrue(engine.Log);

            requests.ShouldBe(1);
            task.Version.ShouldBe("11.0.100-pinned.1");
            task.InstalledSdkVersion.ShouldBe("12.0.100-daily.1");
            ReadInstallerArguments(task).ShouldBe(["", "12.0.1xx", "daily", task.InstallDir.TrimEnd(Path.DirectorySeparatorChar)]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DailyBootstrapSdkOverrideSkipsDiscoveryAndReportsInstalledOrCachedVersion(bool alreadyInstalled)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            MockEngine engine = new(_output);
            using MockHttpMessageHandler handler = new((_, _) => throw new InvalidOperationException("Unexpected main-channel discovery."));
            InstallDotNetCoreTask task = CreateInstallationTask(env, engine, handler);
            if (alreadyInstalled)
            {
                Directory.CreateDirectory(Path.Combine(task.InstallDir, "sdk", "12.0.100-daily.1"));
            }

            task.Execute().ShouldBeTrue(engine.Log);

            task.InstalledSdkVersion.ShouldBe("12.0.100-daily.1");
            ReadInstallerArguments(task).ShouldBe(["", "12.0.1xx", "daily", task.InstallDir.TrimEnd(Path.DirectorySeparatorChar)]);
        }

        [Fact]
        public void DailyBootstrapSdkChecksLatestOnEveryAcquisition()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            MockEngine engine = new(_output);
            int requests = 0;
            using MockHttpMessageHandler handler = new((request, _) =>
            {
                requests++;
                return CreateResponse(request, "[win-x64-zip-main]: https://aka.ms/dotnet/12.0.1xx/daily/dotnet-sdk-win-x64.zip");
            });
            InstallDotNetCoreTask task = CreateInstallationTask(env, engine, handler, channel: "");

            task.Execute().ShouldBeTrue(engine.Log);
            task.Execute().ShouldBeTrue(engine.Log);
            File.WriteAllText(Path.Combine(task.DotNetInstallScriptRootPath, "latest-sdk.version"), "12.0.100-daily.2");
            task.Execute().ShouldBeTrue(engine.Log);

            requests.ShouldBe(3);
            File.ReadAllLines(Path.Combine(task.DotNetInstallScriptRootPath, "arguments.txt")).Length.ShouldBe(3);
            task.InstalledSdkVersion.ShouldBe("12.0.100-daily.2");
            Directory.Exists(Path.Combine(task.InstallDir, "sdk", "12.0.100-daily.1")).ShouldBeTrue();
            Directory.Exists(Path.Combine(task.InstallDir, "sdk", "12.0.100-daily.2")).ShouldBeTrue();
            engine.Log.ShouldContain("is already installed");
        }

        [Theory]
        [InlineData("[win-x64-zip-release]: https://aka.ms/dotnet/12.0.1xx/daily/dotnet-sdk-win-x64.zip")]
        [InlineData("[win-x64-zip-main]: https://aka.ms/dotnet/invalid/daily/dotnet-sdk-win-x64.zip")]
        [InlineData("[win-x64-zip-main]: https://aka.ms/dotnet/12.0.1xx/daily/dotnet-sdk-win-x64.zip\n[linux-targz-main]: https://aka.ms/dotnet/13.0.1xx/daily/dotnet-sdk-linux-x64.tar.gz")]
        public void DailyBootstrapSdkRejectsMissingInvalidOrInconsistentMainMetadata(string buildsTable)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            MockEngine engine = new(_output);
            using MockHttpMessageHandler handler = new((request, _) => CreateResponse(request, buildsTable));
            InstallDotNetCoreTask task = CreateInstallationTask(env, engine, handler, channel: "");

            task.Execute().ShouldBeFalse();

            task.InstalledSdkVersion.ShouldBeEmpty();
            engine.Errors.ShouldBe(1, engine.Log);
            engine.Log.ShouldContain("BootstrapSdkChannel");
            File.Exists(Path.Combine(task.DotNetInstallScriptRootPath, "arguments.txt")).ShouldBeFalse();
        }

        [Fact]
        public void DailyBootstrapSdkReportsDiscoveryTransportFailureWithoutPinnedFallback()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            MockEngine engine = new(_output);
            int requests = 0;
            using MockHttpMessageHandler handler = new((_, _) =>
            {
                requests++;
                throw CreateConnectionResetException();
            });
            InstallDotNetCoreTask task = CreateInstallationTask(env, engine, handler, channel: "");
            Directory.CreateDirectory(Path.Combine(task.InstallDir, "sdk", task.Version));

            task.Execute().ShouldBeFalse();

            requests.ShouldBe(3);
            task.InstalledSdkVersion.ShouldBeEmpty();
            engine.Errors.ShouldBe(1, engine.Log);
            engine.Log.ShouldContain("BootstrapSdkChannel");
        }

        [Theory]
        [InlineData("LTS")]
        [InlineData("main")]
        [InlineData("12.0.1xx;other=value")]
        [InlineData("../12.0.1xx")]
        public void DailyBootstrapSdkRejectsInvalidChannelOverrides(string channel)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            MockEngine engine = new(_output);
            using MockHttpMessageHandler handler = new((_, _) => throw new InvalidOperationException("Unexpected network request."));
            InstallDotNetCoreTask task = CreateInstallationTask(env, engine, handler, channel: channel);

            task.Execute().ShouldBeFalse();

            task.InstalledSdkVersion.ShouldBeEmpty();
            engine.Errors.ShouldBe(1, engine.Log);
            File.Exists(Path.Combine(task.DotNetInstallScriptRootPath, "arguments.txt")).ShouldBeFalse();
        }

        [Theory]
        [InlineData("exit 7")]
        [InlineData("exit 0")]
        [InlineData("echo 'dotnet-install: Installed version is 12.0.100-daily.1'")]
        public void DailyBootstrapSdkRejectsFailedUnreportedOrIncompleteInstallations(string script)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            MockEngine engine = new(_output);
            using MockHttpMessageHandler handler = new((_, _) => throw new InvalidOperationException("Unexpected network request."));
            InstallDotNetCoreTask task = CreateInstallationTask(env, engine, handler);
            File.WriteAllText(GetInstallerScriptPath(task), script);

            task.Execute().ShouldBeFalse();

            task.InstalledSdkVersion.ShouldBeEmpty();
            engine.Errors.ShouldBe(1, engine.Log);
        }

        [Theory]
        [InlineData(true, null, "-Version 11.0.100-pinned.1", "-InstallDir")]
        [InlineData(false, null, "--version 11.0.100-pinned.1", "--install-dir")]
        [InlineData(true, "12.0.1xx", "-Channel \"12.0.1xx\" -Quality daily", "-InstallDir")]
        [InlineData(false, "12.0.1xx", "--channel \"12.0.1xx\" --quality daily", "--install-dir")]
        public void BootstrapSdkArgumentsQuotePathsAndDoNotMixDailyWithVersion(bool isWindows, string? channel, string sdkArguments, string installSwitch)
        {
            MethodInfo method = typeof(InstallDotNetCoreTask).GetMethod("GenerateScriptArguments", BindingFlags.Static | BindingFlags.NonPublic)!;
            string arguments = (string)method.Invoke(null, [isWindows, "script path with spaces", "install path with spaces", "11.0.100-pinned.1", channel])!;

            arguments.ShouldContain("\"script path with spaces\"");
            arguments.ShouldContain(sdkArguments);
            arguments.ShouldContain($"{installSwitch} \"install path with spaces\"");
            if (channel is not null)
            {
                arguments.ShouldNotContain("11.0.100-pinned.1");
            }
        }

        private static HttpResponseMessage CreateResponse(HttpRequestMessage request, string content)
            => new(HttpStatusCode.OK) { Content = new StringContent(content), RequestMessage = request };

        private static string[] ReadInstallerArguments(InstallDotNetCoreTask task)
            => File.ReadAllLines(Path.Combine(task.DotNetInstallScriptRootPath, "arguments.txt"))[0].Split('|');

        private static string GetInstallerScriptPath(InstallDotNetCoreTask task)
            => Path.Combine(task.DotNetInstallScriptRootPath, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet-install.ps1" : "dotnet-install.sh");

        private static InstallDotNetCoreTask CreateInstallationTask(TestEnvironment env, MockEngine engine, HttpMessageHandler handler, bool daily = true, string channel = "12.0.1xx")
        {
            string root = env.CreateFolder(subfolder: "bootstrap with spaces").Path;
            InstallDotNetCoreTask task = CreateTask(engine, handler);
            task.InstallDir = Path.Combine(root, "installation") + Path.DirectorySeparatorChar;
            task.DotNetInstallScriptRootPath = Path.Combine(root, "scripts with spaces");
            task.Version = "11.0.100-pinned.1";
            task.UseDailySdk = daily;
            task.Channel = channel;
            Directory.CreateDirectory(task.DotNetInstallScriptRootPath);
            File.WriteAllText(Path.Combine(task.DotNetInstallScriptRootPath, "latest-sdk.version"), "12.0.100-daily.1");
            File.WriteAllText(GetInstallerScriptPath(task), RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? """
                  param([string]$Version, [string]$Channel, [string]$Quality, [string]$InstallDir)
                  [IO.File]::AppendAllText((Join-Path $PSScriptRoot 'arguments.txt'), "$Version|$Channel|$Quality|$InstallDir`n")
                  $sdkVersion = $Version
                  if (-not $sdkVersion) { $sdkVersion = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'latest-sdk.version')).Trim() }
                  $sdkDir = Join-Path (Join-Path $InstallDir 'sdk') $sdkVersion
                  if (Test-Path $sdkDir) {
                    Write-Output "dotnet-install: .NET Core SDK with version '$sdkVersion' is already installed."
                  } else {
                    New-Item -ItemType Directory -Path $sdkDir -Force | Out-Null
                    Write-Output "dotnet-install: Installed version is $sdkVersion"
                  }
                  """
                : """
                  #!/usr/bin/env bash
                  version=""
                  channel=""
                  quality=""
                  install_dir=""
                  while [[ $# -gt 0 ]]; do
                    case "$1" in
                      --version) version="$2"; shift 2 ;;
                      --channel) channel="$2"; shift 2 ;;
                      --quality) quality="$2"; shift 2 ;;
                      --install-dir) install_dir="$2"; shift 2 ;;
                      *) exit 9 ;;
                    esac
                  done
                  script_root="$(dirname "$0")"
                  printf '%s|%s|%s|%s\n' "$version" "$channel" "$quality" "$install_dir" >> "$script_root/arguments.txt"
                  sdk_version="$version"
                  if [[ -z "$sdk_version" ]]; then sdk_version="$(cat "$script_root/latest-sdk.version")"; fi
                  sdk_dir="$install_dir/sdk/$sdk_version"
                  if [[ -d "$sdk_dir" ]]; then
                    echo "dotnet-install: .NET SDK with version '$sdk_version' is already installed."
                  else
                    mkdir -p "$sdk_dir"
                    echo "dotnet-install: Installed version is $sdk_version"
                  fi
                  """);
            return task;
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
                .Invoke(task, ["dotnet-install.ps1", scriptPath])!;
        }

        private static HttpRequestException CreateConnectionResetException()
        {
            return new HttpRequestException(
                "The SSL connection could not be established, see inner exception.",
                new IOException("Unable to read data from the transport connection: connection reset test."));
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

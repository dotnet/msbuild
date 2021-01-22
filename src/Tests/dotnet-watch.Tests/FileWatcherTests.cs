// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class FileWatcherTests
    {
        public FileWatcherTests(ITestOutputHelper output)
        {
            _output = output;
            _testAssetManager = new TestAssetsManager(output);
        }

        private readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
        private readonly TimeSpan NegativeTimeout = TimeSpan.FromSeconds(5);
        private readonly ITestOutputHelper _output;
        private readonly TestAssetsManager _testAssetManager;

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NewFile(bool usePolling)
        {
            if (!usePolling && OperatingSystem.IsMacOS())
            {
                // Skip on MacOS https://github.com/dotnet/aspnetcore/issues/29141
                return;
            }

            var dir = _testAssetManager.CreateTestDirectory().Path;

            using var watcher = FileWatcherFactory.CreateWatcher(dir, usePolling);

            var changedEv = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var filesChanged = new HashSet<string>();

            watcher.OnFileChange += (_, f) =>
            {
                filesChanged.Add(f);
                changedEv.TrySetResult();
            };
            watcher.EnableRaisingEvents = true;

            var testFileFullPath = Path.Combine(dir, "foo");
            File.WriteAllText(testFileFullPath, string.Empty);

            await changedEv.Task.TimeoutAfter(DefaultTimeout);
            Assert.Equal(testFileFullPath, filesChanged.Single());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ChangeFile(bool usePolling)
        {
            if (!usePolling && OperatingSystem.IsMacOS())
            {
                // Skip on MacOS https://github.com/dotnet/aspnetcore/issues/29141
                return;
            }

            var dir = _testAssetManager.CreateTestDirectory().Path;

            var testFileFullPath = Path.Combine(dir, "foo");
            File.WriteAllText(testFileFullPath, string.Empty);

            var watcher = FileWatcherFactory.CreateWatcher(dir, usePolling);

            var changedEv = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var filesChanged = new HashSet<string>();

            EventHandler<string> handler = null;
            handler = (_, f) =>
            {
                watcher.EnableRaisingEvents = false;
                watcher.OnFileChange -= handler;

                filesChanged.Add(f);
                changedEv.TrySetResult();
            };

            watcher.OnFileChange += handler;
            watcher.EnableRaisingEvents = true;

            if (usePolling)
            {
                // On Unix the file write time is in 1s increments;
                // if we don't wait, there's a chance that the polling
                // watcher will not detect the change
                await Task.Delay(1000);
            }
            File.WriteAllText(testFileFullPath, string.Empty);

            await changedEv.Task.TimeoutAfter(DefaultTimeout);
            Assert.Equal(testFileFullPath, filesChanged.Single());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MoveFile(bool usePolling)
        {
            if (!usePolling && OperatingSystem.IsMacOS())
            {
                // Skip on MacOS https://github.com/dotnet/aspnetcore/issues/29141
                return;
            }

            var dir = _testAssetManager.CreateTestDirectory().Path;
            var srcFile = Path.Combine(dir, "foo");
            var dstFile = Path.Combine(dir, "foo2");

            File.WriteAllText(srcFile, string.Empty);

            using var watcher = FileWatcherFactory.CreateWatcher(dir, usePolling);

            var changedEv = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var filesChanged = new HashSet<string>();

            EventHandler<string> handler = null;
            handler = (_, f) =>
            {
                filesChanged.Add(f);

                if (filesChanged.Count >= 2)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.OnFileChange -= handler;

                    changedEv.TrySetResult(0);
                }
            };

            watcher.OnFileChange += handler;
            watcher.EnableRaisingEvents = true;

            File.Move(srcFile, dstFile);

            await changedEv.Task.TimeoutAfter(DefaultTimeout);
            Assert.Contains(srcFile, filesChanged);
            Assert.Contains(dstFile, filesChanged);
        }

        [Fact]
        public async Task FileInSubdirectory()
        {
            var dir = _testAssetManager.CreateTestDirectory().Path;

            var subdir = Path.Combine(dir, "subdir");
            Directory.CreateDirectory(subdir);

            var testFileFullPath = Path.Combine(subdir, "foo");
            File.WriteAllText(testFileFullPath, string.Empty);

            using var watcher = FileWatcherFactory.CreateWatcher(dir, usePollingWatcher: true);

            var changedEv = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var filesChanged = new HashSet<string>();

            EventHandler<string> handler = null;
            handler = (_, f) =>
            {
                filesChanged.Add(f);

                if (filesChanged.Count >= 2)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.OnFileChange -= handler;
                    changedEv.TrySetResult(0);
                }
            };

            watcher.OnFileChange += handler;
            watcher.EnableRaisingEvents = true;

            // On Unix the file write time is in 1s increments;
            // if we don't wait, there's a chance that the polling
            // watcher will not detect the change
            await Task.Delay(1000);

            File.WriteAllText(testFileFullPath, string.Empty);

            await changedEv.Task.TimeoutAfter(DefaultTimeout);
            Assert.Contains(subdir, filesChanged);
            Assert.Contains(testFileFullPath, filesChanged);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NoNotificationIfDisabled(bool usePolling)
        {
            if (!usePolling && OperatingSystem.IsMacOS())
            {
                // Skip on MacOS https://github.com/dotnet/aspnetcore/issues/29141
                return;
            }

            var dir = _testAssetManager.CreateTestDirectory().Path;

            using var watcher = FileWatcherFactory.CreateWatcher(dir, usePolling);

            var changedEv = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.OnFileChange += (_, f) => changedEv.TrySetResult(0);

            // Disable
            watcher.EnableRaisingEvents = false;

            var testFileFullPath = Path.Combine(dir, "foo");

            if (usePolling)
            {
                // On Unix the file write time is in 1s increments;
                // if we don't wait, there's a chance that the polling
                // watcher will not detect the change
                await Task.Delay(1000);
            }
            File.WriteAllText(testFileFullPath, string.Empty);

            await Assert.ThrowsAsync<TimeoutException>(() => changedEv.Task.TimeoutAfter(NegativeTimeout));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DisposedNoEvents(bool usePolling)
        {
            if (!usePolling && OperatingSystem.IsMacOS())
            {
                // Skip on MacOS https://github.com/dotnet/aspnetcore/issues/29141
                return;
            }

            var dir = _testAssetManager.CreateTestDirectory().Path;
            var changedEv = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var watcher = FileWatcherFactory.CreateWatcher(dir, usePolling))
            {
                watcher.OnFileChange += (_, f) => changedEv.TrySetResult();
                watcher.EnableRaisingEvents = true;
            }

            var testFileFullPath = Path.Combine(dir, "foo");

            if (usePolling)
            {
                // On Unix the file write time is in 1s increments;
                // if we don't wait, there's a chance that the polling
                // watcher will not detect the change
                await Task.Delay(1000);
            }
            File.WriteAllText(testFileFullPath, string.Empty);

            await Assert.ThrowsAsync<TimeoutException>(() => changedEv.Task.TimeoutAfter(NegativeTimeout));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MultipleFiles(bool usePolling)
        {
            if (!usePolling && OperatingSystem.IsMacOS())
            {
                // Skip on MacOS https://github.com/dotnet/aspnetcore/issues/29141
                return;
            }

            var dir = _testAssetManager.CreateTestDirectory().Path;

            File.WriteAllText(Path.Combine(dir, "foo1"), string.Empty);
            File.WriteAllText(Path.Combine(dir, "foo2"), string.Empty);
            File.WriteAllText(Path.Combine(dir, "foo3"), string.Empty);
            File.WriteAllText(Path.Combine(dir, "foo4"), string.Empty);

            // On Unix the native file watcher may surface events from
            // the recent past. Delay to avoid those.
            // On Unix the file write time is in 1s increments;
            // if we don't wait, there's a chance that the polling
            // watcher will not detect the change
            await Task.Delay(1250);

            var testFileFullPath = Path.Combine(dir, "foo3");

            using var watcher = FileWatcherFactory.CreateWatcher(dir, usePolling);

            var changedEv = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var filesChanged = new HashSet<string>();

            EventHandler<string> handler = null;
            handler = (_, f) =>
            {
                watcher.EnableRaisingEvents = false;
                watcher.OnFileChange -= handler;
                filesChanged.Add(f);
                changedEv.TrySetResult(0);
            };

            watcher.OnFileChange += handler;
            watcher.EnableRaisingEvents = true;

            File.WriteAllText(testFileFullPath, string.Empty);

            await changedEv.Task.TimeoutAfter(DefaultTimeout);
            Assert.Equal(testFileFullPath, filesChanged.Single());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MultipleTriggers(bool usePolling)
        {
            if (!usePolling && OperatingSystem.IsMacOS())
            {
                // Skip on MacOS https://github.com/dotnet/aspnetcore/issues/29141
                return;
            }

            var dir = _testAssetManager.CreateTestDirectory().Path;

            using var watcher = FileWatcherFactory.CreateWatcher(dir, usePolling);

            watcher.EnableRaisingEvents = true;

            for (var i = 0; i < 5; i++)
            {
                await AssertFileChangeRaisesEvent(dir, watcher);
            }

            watcher.EnableRaisingEvents = false;
        }

        private async Task AssertFileChangeRaisesEvent(string directory, IFileSystemWatcher watcher)
        {
            var changedEv = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var expectedPath = Path.Combine(directory, Path.GetRandomFileName());
            EventHandler<string> handler = (object _, string f) =>
            {
                _output.WriteLine("File changed: " + f);
                try
                {
                    if (string.Equals(f, expectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        changedEv.TrySetResult(0);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // There's a known race condition here:
                    // even though we tell the watcher to stop raising events and we unsubscribe the handler
                    // there might be in-flight events that will still process. Since we dispose the reset
                    // event, this code will fail if the handler executes after Dispose happens.
                }
            };

            File.AppendAllText(expectedPath, " ");

            watcher.OnFileChange += handler;
            try
            {
                // On Unix the file write time is in 1s increments;
                // if we don't wait, there's a chance that the polling
                // watcher will not detect the change
                await Task.Delay(1000);
                File.AppendAllText(expectedPath, " ");
                await changedEv.Task.TimeoutAfter(DefaultTimeout);
            }
            finally
            {
                watcher.OnFileChange -= handler;
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DeleteSubfolder(bool usePolling)
        {
            if (!usePolling && OperatingSystem.IsMacOS())
            {
                // Skip on MacOS https://github.com/dotnet/aspnetcore/issues/29141
                return;
            }

            var dir = _testAssetManager.CreateTestDirectory().Path;

            var subdir = Path.Combine(dir, "subdir");
            Directory.CreateDirectory(subdir);

            var f1 = Path.Combine(subdir, "foo1");
            var f2 = Path.Combine(subdir, "foo2");
            var f3 = Path.Combine(subdir, "foo3");

            File.WriteAllText(f1, string.Empty);
            File.WriteAllText(f2, string.Empty);
            File.WriteAllText(f3, string.Empty);

            using var watcher = FileWatcherFactory.CreateWatcher(dir, usePolling);

            var changedEv = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var filesChanged = new HashSet<string>();

            EventHandler<string> handler = null;
            handler = (_, f) =>
            {
                filesChanged.Add(f);

                if (filesChanged.Count >= 4)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.OnFileChange -= handler;
                    changedEv.TrySetResult();
                }
            };

            watcher.OnFileChange += handler;
            watcher.EnableRaisingEvents = true;

            Directory.Delete(subdir, recursive: true);

            await changedEv.Task.TimeoutAfter(DefaultTimeout);

            Assert.Contains(f1, filesChanged);
            Assert.Contains(f2, filesChanged);
            Assert.Contains(f3, filesChanged);
            Assert.Contains(subdir, filesChanged);
        }
    }
}

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Tools.Internal;
using Moq;
using Xunit;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class StaticFileHandlerTest
    {
        [Fact]
        public async ValueTask TryHandleFileAction_WritesUpdateCssMessage()
        {
            // Arrange
            var server = new Mock<BrowserRefreshServer>(NullReporter.Singleton);
            byte[] writtenBytes = null;
            server.Setup(s => s.SendMessage(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback((byte[] bytes, CancellationToken cts) =>
                {
                    writtenBytes = bytes;
                });
            var fileContentHandler = new StaticFileHandler(NullReporter.Singleton);
            var context = new DotNetWatchContext
            {
                BrowserRefreshServer = server.Object,
            };
            var file = new FileItem { FilePath = "Test.css", IsStaticFile = true, StaticWebAssetPath = "content/Test.css" };

            // Act
            var result = await fileContentHandler.TryHandleFileChange(context, file, default);

            // Assert
            Assert.True(result);
            Assert.NotNull(writtenBytes);
            var deserialized = JsonSerializer.Deserialize<UpdateStaticFileMessage>(writtenBytes, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.Equal("UpdateStaticFile", deserialized.Type);
            Assert.Equal("content/Test.css", deserialized.Path);
        }

        [Fact]
        public async ValueTask TryHandleFileAction_CausesBrowserRefreshForNonCssFile()
        {
            // Arrange
            var server = new Mock<BrowserRefreshServer>(NullReporter.Singleton);
            byte[] writtenBytes = null;
            server.Setup(s => s.SendMessage(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Callback((byte[] bytes, CancellationToken cts) =>
                {
                    writtenBytes = bytes;
                });
            var fileContentHandler = new StaticFileHandler(NullReporter.Singleton);
            var context = new DotNetWatchContext
            {
                BrowserRefreshServer = server.Object,
            };
            var file = new FileItem { FilePath = "Test.js", IsStaticFile = true, StaticWebAssetPath = "Test.js" };

            // Act
            var result = await fileContentHandler.TryHandleFileChange(context, file, default);

            // Assert
            Assert.True(result);
            Assert.NotNull(writtenBytes);
            var deserialized = JsonSerializer.Deserialize<UpdateStaticFileMessage>(writtenBytes, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.Equal("UpdateStaticFile", deserialized.Type);
            Assert.Equal("content/Test.js", deserialized.Path);
        }

        private record UpdateStaticFileMessage(string Type, string Path);

    }
}

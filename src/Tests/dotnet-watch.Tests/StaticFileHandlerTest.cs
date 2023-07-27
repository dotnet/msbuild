// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Extensions.Tools.Internal;
using Moq;

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

            var file = new FileItem { FilePath = "Test.css", IsStaticFile = true, StaticWebAssetPath = "content/Test.css" };

            // Act
            var result = await fileContentHandler.TryHandleFileChange(server.Object, file, default);

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
            var file = new FileItem { FilePath = "Test.js", IsStaticFile = true, StaticWebAssetPath = "Test.js" };

            // Act
            var result = await fileContentHandler.TryHandleFileChange(server.Object, file, default);

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

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    public class WebSocketScriptInjectionTest
    {
        [Fact]
        public async Task TryInjectLiveReloadScriptAsync_DoesNotInjectMarkup_IfInputDoesNotContainBodyTag()
        {
            // Arrange
            var stream = new MemoryStream();
            var input = Encoding.UTF8.GetBytes("<div>this is not a real body tag.</div>");

            // Act
            var result = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(stream, input);

            // Assert
            Assert.False(result);
            Assert.Equal(input, stream.ToArray());
        }

        [Theory]
        [MemberData(nameof(ClosingBodyTagVariations))]
        public async Task TryInjectLiveReloadScriptAsync_InjectsMarkupIfBodyTagAppearsInTheMiddle(string closingBodyTag)
        {
            // Arrange
            var expected =
$@"<footer>
    This is the footer
</footer>
{WebSocketScriptInjection.InjectedScript}{closingBodyTag}
</html>";
            var stream = new MemoryStream();
            var input = Encoding.UTF8.GetBytes(
$@"<footer>
    This is the footer
</footer>
{closingBodyTag}
</html>");

            // Act
            var result = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(stream, input);

            // Assert
            Assert.True(result);
            var output = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal(expected, output, ignoreLineEndingDifferences: true);
        }

        [Theory]
        [MemberData(nameof(ClosingBodyTagVariations))]
        public async Task TryInjectLiveReloadScriptAsync_WithOffsetBodyTagAppearsInMiddle(string closingBodyTag)
        {
            // Arrange
            var expected = $"</table>{WebSocketScriptInjection.InjectedScript}{closingBodyTag}";
            var stream = new MemoryStream();
            var input = Encoding.UTF8.GetBytes($"unused</table>{closingBodyTag}");

            // Act
            var result = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(stream, input.AsMemory(6));

            // Assert
            Assert.True(result);
            var output = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(ClosingBodyTagVariations))]
        public async Task TryInjectLiveReloadScriptAsync_WithOffsetBodyTagAppearsAtStartOfOffset(string closingBodyTag)
        {
            // Arrange
            var expected = $"{WebSocketScriptInjection.InjectedScript}{closingBodyTag}";
            var stream = new MemoryStream();
            var input = Encoding.UTF8.GetBytes($"unused{closingBodyTag}");

            // Act
            var result = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(stream, input.AsMemory(6));

            // Assert
            Assert.True(result);
            var output = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(ClosingBodyTagVariations))]
        public async Task TryInjectLiveReloadScriptAsync_InjectsMarkupIfBodyTagAppearsAtTheStartOfOutput(string closingBodyTag)
        {
            // Arrange
            var expected = $"{WebSocketScriptInjection.InjectedScript}{closingBodyTag}</html>";
            var stream = new MemoryStream();
            var input = Encoding.UTF8.GetBytes($"{closingBodyTag}</html>");

            // Act
            var result = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(stream, input);

            // Assert
            Assert.True(result);
            var output = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(ClosingBodyTagVariations))]
        public async Task TryInjectLiveReloadScriptAsync_InjectsMarkupIfBodyTagAppearsByItself(string closingBodyTag)
        {
            // Arrange
            var expected = $"{WebSocketScriptInjection.InjectedScript}{closingBodyTag}";
            var stream = new MemoryStream();
            var input = Encoding.UTF8.GetBytes(closingBodyTag);

            // Act
            var result = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(stream, input);

            // Assert
            Assert.True(result);
            var output = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(ClosingBodyTagVariations))]
        public async Task TryInjectLiveReloadScriptAsync_MultipleBodyTags(string closingBodyTag)
        {
            // Arrange
            var expected = $"<p>{closingBodyTag}some text</p>{WebSocketScriptInjection.InjectedScript}{closingBodyTag}";
            var stream = new MemoryStream();
            var input = Encoding.UTF8.GetBytes($"abc<p>{closingBodyTag}some text</p>{closingBodyTag}").AsMemory(3);

            // Act
            var result = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(stream, input);

            // Assert
            Assert.True(result);
            var output = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal(expected, output);
        }

        public static IEnumerable<object[]> ClosingBodyTagVariations => new[]
        {
            new[] { "</body>" },
            new[] { "</BoDy>" },
            new[] { "</  BODY>" },
            new[] { "</ bodY  >" },
            new[] { "</\n\n Body\t >" },
        };
    }
}

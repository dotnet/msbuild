using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Microsoft.DotNet.Tools.Compiler;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class PreprocessorTests
    {
        private string Preprocess(string text, IDictionary<string, string> parameters)
        {
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            {
                using (var output = new MemoryStream())
                {
                    PPFilePreprocessor.Preprocess(input, output, parameters);
                    return Encoding.UTF8.GetString(output.ToArray());
                }
            }
        }

        [Theory]
        [InlineData("$a$", "AValue")]
        [InlineData("$a", "$a")]
        [InlineData("a$", "a$")]
        [InlineData("$$a$", "$a$")]
        [InlineData("$a$$b$", "AValueBValue")]
        [InlineData("$$a$$$$b$$", "$a$$b$")]

        public void ProcessesCorrectly(string input, string output)
        {
            var parameters = new Dictionary<string, string>()
            {
                { "a", "AValue" },
                { "b", "BValue" }
            };
            var result = Preprocess(input, parameters);
            result.Should().Be(output);
        }

        [Fact]
        public void ThrowsOnParameterNotFound()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Preprocess("$a$", new Dictionary<string, string>()));
            ex.Message.Should().Contain("a");
        }
    }
}

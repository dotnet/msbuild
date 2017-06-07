// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.IO;
using System.Text;
using Microsoft.Build.Construction;
using Xunit;

namespace Microsoft.Build.UnitTests.Construction
{
    /// <summary>
    /// Tests for the ElementLocation class
    /// </summary>
    public class ProjectRootElement_Tests
    {
        [Theory]
        [InlineData("", true)]
        [InlineData("", false)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>", true)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>", false)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>
", true)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>
", false)]
        public void IsEmptyXmlFileReturnsTrue(string contents, bool useByteOrderMark)
        {
            string path = useByteOrderMark ?
                ObjectModelHelpers.CreateFileInTempProjectDirectory(Guid.NewGuid().ToString("N"), contents, Encoding.UTF8) :
                ObjectModelHelpers.CreateFileInTempProjectDirectory(Guid.NewGuid().ToString("N"), contents);

            Assert.True(ProjectRootElement.IsEmptyXmlFile(path));
        }

        [Theory]
        [InlineData("<Foo/>", true)]
        [InlineData("Foo/>", false)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Foo/>", true)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Foo/>", false)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>
bar", true)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>
bar", false)]
        public void IsEmptyXmlFileReturnsFalse(string contents, bool useByteOrderMark)
        {
            string path = useByteOrderMark ?
                ObjectModelHelpers.CreateFileInTempProjectDirectory(Guid.NewGuid().ToString("N"), contents, Encoding.UTF8) :
                ObjectModelHelpers.CreateFileInTempProjectDirectory(Guid.NewGuid().ToString("N"), contents);

            Assert.False(ProjectRootElement.IsEmptyXmlFile(path));
        }
    }
}

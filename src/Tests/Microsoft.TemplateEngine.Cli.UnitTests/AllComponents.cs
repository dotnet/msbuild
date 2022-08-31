// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class AllComponents
    {
        [Fact]
        public void TestAllComponents()
        {
            var assemblyCatalog = new AssemblyComponentCatalog(new[] { typeof(Components).Assembly });

            var expectedTypeNames = assemblyCatalog.Select(pair => pair.Item1.FullName + ";" + pair.Item2.GetType().FullName).OrderBy(name => name);
            var actualTypeNames = Components.AllComponents.Select(t => t.Type.FullName + ";" + t.Instance.GetType().FullName).OrderBy(name => name);

            Assert.Equal(expectedTypeNames, actualTypeNames);
        }
    }
}

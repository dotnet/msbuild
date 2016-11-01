using System;
using Xunit;

namespace TestNamespace
{
    public class VSTestXunitTests
    {
        [Fact]
        public void VSTestXunitPassTest()
        {
        }

        [Fact]
        public void VSTestXunitFailTest()
        {
            Assert.Equal(1, 2);
        }
    }
}

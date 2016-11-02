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

#if DESKTOP
        [Fact]
        public void VSTestXunitPassTestDesktop()
        {
        }
#else
        [Fact]
        public void VSTestXunitFailTestNetCoreApp()
        {
            Assert.Equal(1, 2);
        }
#endif
    }
}

using System;
using Xunit;
using sample05;

namespace sample05.Test
{
    public class sample05_UnitTest
    {
        [Fact]
        public void sample05_Test()
        {
            var name = Sample.GetName();
            Assert.Equal("Console and unit test demo",name);
        }
    }
}

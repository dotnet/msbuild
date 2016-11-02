using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestNamespace
{
    [TestClass]
    public class VSTestTests
    {
        [TestMethod]
        public void VSTestPassTest()
        {
        }

        [TestMethod]
        public void VSTestFailTest()
        {
            Assert.Fail();
        }
    }
}
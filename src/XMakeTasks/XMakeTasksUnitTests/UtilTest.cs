using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace XMakeTasksUnitTests
{
    
    
    /// <summary>
    ///This is a test class for UtilTest and is intended
    ///to contain all UtilTest Unit Tests
    ///</summary>
    [TestClass()]
    public class UtilTest
    {


        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion


        /// <summary>
        ///A test for GetClrVersion
        ///</summary>
        [TestMethod()]
        [DeploymentItem("Microsoft.Build.Tasks.v4.0.dll")]
        public void GetClrVersionTest()
        {
            string targetFrameworkVersion = "v3.5";
            string expected = "2.0.50727.0"; 
            string actual;
            actual = Util_Accessor.GetClrVersion(targetFrameworkVersion);
            Assert.AreEqual(expected, actual);

            targetFrameworkVersion = "3.5";
            actual = Util_Accessor.GetClrVersion(targetFrameworkVersion);
            Assert.AreEqual(expected, actual);

            System.Version currentVersion = System.Environment.Version;
            System.Version clr4Version = new System.Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build, 0);
            
            targetFrameworkVersion = "v4.0";
            actual = Util_Accessor.GetClrVersion(targetFrameworkVersion);
            expected = clr4Version.ToString();
            Assert.AreEqual(expected, actual);

            targetFrameworkVersion = "v4.2";
            actual = Util_Accessor.GetClrVersion(targetFrameworkVersion);
            expected = clr4Version.ToString();
            Assert.AreEqual(expected, actual);
        }
    }
}

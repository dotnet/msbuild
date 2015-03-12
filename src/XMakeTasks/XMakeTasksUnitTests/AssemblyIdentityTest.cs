using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
namespace XMakeTasksUnitTests
{
    
    
    /// <summary>
    ///This is a test class for AssemblyIdentityTest and is intended
    ///to contain all AssemblyIdentityTest Unit Tests
    ///</summary>
    [TestClass()]
    public class AssemblyIdentityTest
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
        ///A test for IsFrameworkAssembly
        ///</summary>
        [TestMethod()]
        public void IsFrameworkAssemblyTest()
        {
            bool actual;
            IList<string> listOfInstalledFrameworks = FrameworkMultiTargeting.GetSupportedTargetFrameworks();

            // if 2.0 is installed on this computer, we will test IsFrameworkAssembly for 2.0 assemblies.
            if (hasVersion(listOfInstalledFrameworks, "Version=v2.0"))
            {
                //if (hasVersion(listOfInstalledFrameworks
                // Test 2.0 CLR binary
                // "Microsoft.Build.Engine" Version="2.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" FileVersion="2.0.50727.3026" InGAC="true" />
                AssemblyIdentity clr2Binary = new AssemblyIdentity("Microsoft.Build.Engine", "2.0.0.0", "b03f5f7f11d50a3a", "neutral", "MSIL");
                actual = clr2Binary.IsFrameworkAssembly;
                Assert.IsTrue(actual);
            }

            if (hasVersion(listOfInstalledFrameworks, "Version=v3.0"))
            {
                // Test 3.0 CLR binary
                // AssemblyName="System.ServiceModel" Version="3.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGAC="false" IsRedistRoot="true" />
                AssemblyIdentity clr3Binary = new AssemblyIdentity("System.ServiceModel", "3.0.0.0", "b77a5c561934e089", "neutral", "MSIL");
                actual = clr3Binary.IsFrameworkAssembly;
                Assert.IsTrue(actual);
            }

            if (hasVersion(listOfInstalledFrameworks, "Version=v3.5"))
            {
                // Test 3.5 CLR binary
                // AssemblyName="Microsoft.Build.Tasks.v3.5" Version="3.5.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGAC="false" />
                AssemblyIdentity clr35Binary = new AssemblyIdentity("Microsoft.Build.Tasks.v3.5", "3.5.0.0", "b03f5f7f11d50a3a", "neutral", "MSIL");
                actual = clr35Binary.IsFrameworkAssembly;
                Assert.IsTrue(actual);
            }

            if (hasVersion(listOfInstalledFrameworks, "Version=v4.0"))
            {
                // Test 4.0 CLR binary
                // AssemblyName="Microsoft.VisualBasic" Version="10.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" FileVersion="4.0.41117.0" InGAC="true" />
                AssemblyIdentity clr4Binary = new AssemblyIdentity("Microsoft.VisualBasic", "10.0.0.0", "b03f5f7f11d50a3a", "neutral", "MSIL");
                actual = clr4Binary.IsFrameworkAssembly;
                Assert.IsTrue(actual);
            }
        }

        private bool hasVersion(IList<string> listOfInstalledFrameworks, string p)
        {
            foreach (string fx in listOfInstalledFrameworks)
            {
                if (fx.Contains(p))
                    return true;
            }

            return false;
        }
    }
}

using Microsoft.Build.CommandLine;

using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class InitializationException_Tests
    {
        /// <summary>
        /// Verify ISerializable is implemented correctly
        /// </summary>
        [Test]
        public void SerializeDeserialize()
        {
            try 
            {
                InitializationException.Throw("message", "invalidSwitch");
            }
            catch(InitializationException e)
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(memStream, e);
                    memStream.Position = 0;

                    InitializationException e2 = (InitializationException)formatter.Deserialize(memStream);

                    Assert.AreEqual(e.Message, e2.Message);
                }
            }
        }

        /// <summary>
        /// Verify ISerializable is implemented correctly
        /// </summary>
        [Test]
        public void SerializeDeserialize2()
        {
            try
            {
                InitializationException.Throw("message", null);
            }
            catch (InitializationException e)
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(memStream, e);
                    memStream.Position = 0;

                    InitializationException e2 = (InitializationException)formatter.Deserialize(memStream);

                    Assert.AreEqual(e.Message, e2.Message);
                }
            }
        }
    }
}

using Microsoft.Build.CommandLine;

using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class CommandLineSwitchException_Tests
    {
        /// <summary>
        /// Verify ISerializable is implemented correctly
        /// </summary>
        [Test]
        public void SerializeDeserialize()
        {
            try
            {
                CommandLineSwitchException.Throw("InvalidNodeNumberValueIsNegative", "commandLineArg");
            }
            catch (CommandLineSwitchException e)
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(memStream, e);
                    memStream.Position = 0;

                    CommandLineSwitchException e2 = (CommandLineSwitchException)formatter.Deserialize(memStream);

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
                CommandLineSwitchException.Throw("InvalidNodeNumberValueIsNegative", null);
            }
            catch (CommandLineSwitchException e)
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(memStream, e);
                    memStream.Position = 0;

                    CommandLineSwitchException e2 = (CommandLineSwitchException)formatter.Deserialize(memStream);

                    Assert.AreEqual(e.Message, e2.Message);
                }
            }
        }
    }
}

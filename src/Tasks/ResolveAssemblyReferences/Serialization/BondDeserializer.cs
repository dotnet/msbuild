using System.IO;

using Bond;
using Bond.Protocols;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Serialization
{
    public class BondDeserializer<T> where T: new()
    {
        public static void Initialize()
        {
            var output = new OutputBuffer();
            var writer = new SimpleBinaryWriter<OutputBuffer>(output);
            Serialize.To(writer, new T());

            var input = new InputBuffer(output.Data);
            var reader = new SimpleBinaryReader<InputBuffer>(input);
            Deserialize<T>.From(reader);
        }

        public static T Deserialize(Stream stream)
        {
            var input = new InputStream(stream);
            var reader = new SimpleBinaryReader<InputBuffer>(input);
            return Deserialize<T>.From(reader);
        }
    }
}

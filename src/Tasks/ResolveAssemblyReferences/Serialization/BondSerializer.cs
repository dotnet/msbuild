using System.IO;

using Bond.Protocols;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Serialization
{
    public class BondSerializer<T> where T: new()
    {
        public static void Initialize()
        {
            var output = new OutputBuffer();
            var writer = new SimpleBinaryWriter<OutputBuffer>(output);
            Bond.Serialize.To(writer, new T());
        }

        public static void Serialize(Stream stream, T obj)
        {
            var output = new OutputStream(stream);
            var writer = new SimpleBinaryWriter<OutputBuffer>(output);
            Bond.Serialize.To(writer, obj);
            output.Flush();
        }
    }
}

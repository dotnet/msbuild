using System.Globalization;
using System.IO;
using System.Text;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// StringWriter class that allows Encoding to be specified. In the standard StringWriter
    /// class only UTF16 is allowed.
    /// </summary>
    internal class EncodingStringWriter : StringWriter
    {
        /// <summary>
        /// Default ctor (Encoding = UTF8)
        /// </summary>
        public EncodingStringWriter() : this(null)
        { }

        public EncodingStringWriter(Encoding encoding) : base(CultureInfo.InvariantCulture)
        {
            Encoding = encoding ?? Encoding.UTF8;
        }

        /// <summary>
        /// Overload to specify encoding.
        /// </summary>
        public override Encoding Encoding { get; }
    }
}

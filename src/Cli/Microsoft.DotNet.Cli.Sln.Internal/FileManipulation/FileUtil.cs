// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// FileUtil.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@xamarin.com>
//
// Copyright (c) 2015 Xamarin, Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

namespace Microsoft.DotNet.Cli.Sln.Internal.FileManipulation
{
    static internal class FileUtil
    {
        internal static TextFormatInfo GetTextFormatInfo(string file)
        {
            var info = new TextFormatInfo();

            string newLine = null;
            Encoding encoding;

            using (FileStream fs = File.OpenRead(file))
            {
                byte[] buf = new byte[1024];
                int nread, i;

                if ((nread = fs.Read(buf, 0, buf.Length)) <= 0)
                {
                    return info;
                }

                if (TryParse(buf, nread, out encoding))
                {
                    i = encoding.GetPreamble().Length;
                }
                else
                {
                    encoding = null;
                    i = 0;
                }

                do
                {
                    while (i < nread)
                    {
                        if (buf[i] == '\r')
                        {
                            newLine = "\r\n";
                            break;
                        }
                        else if (buf[i] == '\n')
                        {
                            newLine = "\n";
                            break;
                        }

                        i++;
                    }

                    if (newLine == null)
                    {
                        if ((nread = fs.Read(buf, 0, buf.Length)) <= 0)
                        {
                            newLine = "\n";
                            break;
                        }

                        i = 0;
                    }
                } while (newLine == null);

                info.EndsWithEmptyLine = fs.Seek(-1, SeekOrigin.End) > 0 && fs.ReadByte() == (int)'\n';
                info.NewLine = newLine;
                info.Encoding = encoding;
                return info;
            }
        }

        private static bool TryParse(byte[] buffer, int available, out Encoding encoding)
        {
            if (buffer.Length >= 2)
            {
                for (int i = 0; i < table.Length; i++)
                {
                    bool matched = true;

                    if (available < table[i].GetPreamble().Length)
                    {
                        continue;
                    }

                    for (int j = 0; j < table[i].GetPreamble().Length; j++)
                    {
                        if (buffer[j] != table[i].GetPreamble()[j])
                        {
                            matched = false;
                            break;
                        }
                    }

                    if (matched)
                    {
                        encoding = table[i];
                        return true;
                    }
                }
            }

            encoding = null;

            return false;
        }

        private static readonly Encoding[] table = new[] {
            Encoding.UTF8,
            Encoding.UTF32,
            Encoding.ASCII,
        };
    }

    internal class TextFormatInfo
    {
        public TextFormatInfo()
        {
            NewLine = Environment.NewLine;
            Encoding = null;
            EndsWithEmptyLine = true;
        }

        public string NewLine { get; set; }
        public Encoding Encoding { get; set; }
        public bool EndsWithEmptyLine { get; set; }
    }
}

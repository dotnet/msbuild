//
// SlnFile.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@xamarin.com>
//
// Copyright (c) 2016 Xamarin, Inc (http://www.xamarin.com)
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Collections;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Reflection;
using Microsoft.DotNet.Cli.Sln.Internal.FileManipulation;

namespace Microsoft.DotNet.Cli.Sln.Internal
{
    public class SlnFile
    {
        SlnProjectCollection projects = new SlnProjectCollection();
        SlnSectionCollection sections = new SlnSectionCollection();
        SlnPropertySet metadata = new SlnPropertySet(true);
        int prefixBlankLines = 1;
        TextFormatInfo format = new TextFormatInfo { NewLine = "\r\n" };

        public string FormatVersion { get; set; }
        public string ProductDescription { get; set; }

        public string VisualStudioVersion
        {
            get { return metadata.GetValue("VisualStudioVersion"); }
            set { metadata.SetValue("VisualStudioVersion", value); }
        }

        public string MinimumVisualStudioVersion
        {
            get { return metadata.GetValue("MinimumVisualStudioVersion"); }
            set { metadata.SetValue("MinimumVisualStudioVersion", value); }
        }

        public SlnFile()
        {
            projects.ParentFile = this;
            sections.ParentFile = this;
        }

        /// <summary>
        /// Gets the sln format version of the provided solution file
        /// </summary>
        /// <returns>The file version.</returns>
        /// <param name="file">File.</param>
        public static string GetFileVersion(string file)
        {
            string strVersion;
            using (var reader = new StreamReader(new FileStream(file, FileMode.Open)))
            {
                var strInput = reader.ReadLine();
                if (strInput == null)
                    return null;

                var match = slnVersionRegex.Match(strInput);
                if (!match.Success)
                {
                    strInput = reader.ReadLine();
                    if (strInput == null)
                        return null;
                    match = slnVersionRegex.Match(strInput);
                    if (!match.Success)
                        return null;
                }

                strVersion = match.Groups[1].Value;
                return strVersion;
            }
        }

        static Regex slnVersionRegex = new Regex(@"Microsoft Visual Studio Solution File, Format Version (\d?\d.\d\d)");

        /// <summary>
        /// The directory to be used as base for converting absolute paths to relative
        /// </summary>
        public FilePath BaseDirectory
        {
            get { return FileName.ParentDirectory; }
        }

        /// <summary>
        /// Gets the solution configurations section.
        /// </summary>
        /// <value>The solution configurations section.</value>
        public SlnPropertySet SolutionConfigurationsSection
        {
            get { return sections.GetOrCreateSection("SolutionConfigurationPlatforms", SlnSectionType.PreProcess).Properties; }
        }

        /// <summary>
        /// Gets the project configurations section.
        /// </summary>
        /// <value>The project configurations section.</value>
        public SlnPropertySetCollection ProjectConfigurationsSection
        {
            get { return sections.GetOrCreateSection("ProjectConfigurationPlatforms", SlnSectionType.PostProcess).NestedPropertySets; }
        }

        public SlnSectionCollection Sections
        {
            get { return sections; }
        }

        public SlnProjectCollection Projects
        {
            get { return projects; }
        }

        public FilePath FileName { get; set; }

        public void Read(string file)
        {
            FileName = file;
            format = FileUtil.GetTextFormatInfo(file);

            using (var sr = new StreamReader(new FileStream(file, FileMode.Open)))
                Read(sr);
        }

        public void Read(TextReader reader)
        {
            string line;
            int curLineNum = 0;
            bool globalFound = false;
            bool productRead = false;

            while ((line = reader.ReadLine()) != null)
            {
                curLineNum++;
                line = line.Trim();
                if (line.StartsWith("Microsoft Visual Studio Solution File", StringComparison.Ordinal))
                {
                    int i = line.LastIndexOf(' ');
                    if (i == -1)
                        throw new InvalidSolutionFormatException(curLineNum);
                    FormatVersion = line.Substring(i + 1);
                    prefixBlankLines = curLineNum - 1;
                }
                if (line.StartsWith("# ", StringComparison.Ordinal))
                {
                    if (!productRead)
                    {
                        productRead = true;
                        ProductDescription = line.Substring(2);
                    }
                }
                else if (line.StartsWith("Project", StringComparison.Ordinal))
                {
                    SlnProject p = new SlnProject();
                    p.Read(reader, line, ref curLineNum);
                    projects.Add(p);
                }
                else if (line == "Global")
                {
                    if (globalFound)
                        throw new InvalidSolutionFormatException(curLineNum, "Global section specified more than once");
                    globalFound = true;
                    while ((line = reader.ReadLine()) != null)
                    {
                        curLineNum++;
                        line = line.Trim();
                        if (line == "EndGlobal")
                        {
                            break;
                        }
                        else if (line.StartsWith("GlobalSection", StringComparison.Ordinal))
                        {
                            var sec = new SlnSection();
                            sec.Read(reader, line, ref curLineNum);
                            sections.Add(sec);
                        }
                        else // Ignore text that's out of place
                            continue;
                    }
                    if (line == null)
                        throw new InvalidSolutionFormatException(curLineNum, "Global section not closed");
                }
                else if (line.IndexOf('=') != -1)
                {
                    metadata.ReadLine(line, curLineNum);
                }
            }
            if (FormatVersion == null)
                throw new InvalidSolutionFormatException(curLineNum, "File header is missing");
        }

        public void Write(string file)
        {
            FileName = file;
            var sw = new StringWriter();
            Write(sw);
            File.WriteAllText(file, sw.ToString());
        }

        public void Write(TextWriter writer)
        {
            writer.NewLine = format.NewLine;
            for (int n = 0; n < prefixBlankLines; n++)
                writer.WriteLine();
            writer.WriteLine("Microsoft Visual Studio Solution File, Format Version " + FormatVersion);
            writer.WriteLine("# " + ProductDescription);

            metadata.Write(writer);

            foreach (var p in projects)
                p.Write(writer);

            writer.WriteLine("Global");
            foreach (SlnSection s in sections)
                s.Write(writer, "GlobalSection");
            writer.WriteLine("EndGlobal");
        }
    }

    public class SlnProject
    {
        SlnSectionCollection sections = new SlnSectionCollection();

        SlnFile parentFile;

        public SlnFile ParentFile
        {
            get
            {
                return parentFile;
            }
            internal set
            {
                parentFile = value;
                sections.ParentFile = parentFile;
            }
        }

        public string Id { get; set; }
        public string TypeGuid { get; set; }
        public string Name { get; set; }
        public string FilePath { get; set; }
        public int Line { get; private set; }
        internal bool Processed { get; set; }

        public SlnSectionCollection Sections
        {
            get { return sections; }
        }

        internal void Read(TextReader reader, string line, ref int curLineNum)
        {
            Line = curLineNum;

            int n = 0;
            FindNext(curLineNum, line, ref n, '(');
            n++;
            FindNext(curLineNum, line, ref n, '"');
            int n2 = n + 1;
            FindNext(curLineNum, line, ref n2, '"');
            TypeGuid = line.Substring(n + 1, n2 - n - 1);

            n = n2 + 1;
            FindNext(curLineNum, line, ref n, ')');
            FindNext(curLineNum, line, ref n, '=');

            FindNext(curLineNum, line, ref n, '"');
            n2 = n + 1;
            FindNext(curLineNum, line, ref n2, '"');
            Name = line.Substring(n + 1, n2 - n - 1);

            n = n2 + 1;
            FindNext(curLineNum, line, ref n, ',');
            FindNext(curLineNum, line, ref n, '"');
            n2 = n + 1;
            FindNext(curLineNum, line, ref n2, '"');
            FilePath = line.Substring(n + 1, n2 - n - 1);

            n = n2 + 1;
            FindNext(curLineNum, line, ref n, ',');
            FindNext(curLineNum, line, ref n, '"');
            n2 = n + 1;
            FindNext(curLineNum, line, ref n2, '"');
            Id = line.Substring(n + 1, n2 - n - 1);

            while ((line = reader.ReadLine()) != null)
            {
                curLineNum++;
                line = line.Trim();
                if (line == "EndProject")
                {
                    return;
                }
                if (line.StartsWith("ProjectSection", StringComparison.Ordinal))
                {
                    if (sections == null)
                        sections = new SlnSectionCollection();
                    var sec = new SlnSection();
                    sections.Add(sec);
                    sec.Read(reader, line, ref curLineNum);
                }
            }

            throw new InvalidSolutionFormatException(curLineNum, "Project section not closed");
        }

        void FindNext(int ln, string line, ref int i, char c)
        {
            i = line.IndexOf(c, i);
            if (i == -1)
                throw new InvalidSolutionFormatException(ln);
        }

        public void Write(TextWriter writer)
        {
            writer.Write("Project(\"");
            writer.Write(TypeGuid);
            writer.Write("\") = \"");
            writer.Write(Name);
            writer.Write("\", \"");
            writer.Write(FilePath);
            writer.Write("\", \"");
            writer.Write(Id);
            writer.WriteLine("\"");
            if (sections != null)
            {
                foreach (SlnSection s in sections)
                    s.Write(writer, "ProjectSection");
            }
            writer.WriteLine("EndProject");
        }
    }

    public class SlnSection
    {
        SlnPropertySetCollection nestedPropertySets;
        SlnPropertySet properties;
        List<string> sectionLines;
        int baseIndex;

        public string Id { get; set; }
        public int Line { get; private set; }

        internal bool Processed { get; set; }

        public SlnFile ParentFile { get; internal set; }

        public bool IsEmpty
        {
            get
            {
                return (properties == null || properties.Count == 0) && (nestedPropertySets == null || nestedPropertySets.All(t => t.IsEmpty)) && (sectionLines == null || sectionLines.Count == 0);
            }
        }

        /// <summary>
        /// If true, this section won't be written to the file if it is empty
        /// </summary>
        /// <value><c>true</c> if skip if empty; otherwise, <c>false</c>.</value>
        public bool SkipIfEmpty { get; set; }

        public void Clear()
        {
            properties = null;
            nestedPropertySets = null;
            sectionLines = null;
        }

        public SlnPropertySet Properties
        {
            get
            {
                if (properties == null)
                {
                    properties = new SlnPropertySet();
                    properties.ParentSection = this;
                    if (sectionLines != null)
                    {
                        foreach (var line in sectionLines)
                            properties.ReadLine(line, Line);
                        sectionLines = null;
                    }
                }
                return properties;
            }
        }

        public SlnPropertySetCollection NestedPropertySets
        {
            get
            {
                if (nestedPropertySets == null)
                {
                    nestedPropertySets = new SlnPropertySetCollection(this);
                    if (sectionLines != null)
                        LoadPropertySets();
                }
                return nestedPropertySets;
            }
        }

        public void SetContent(IEnumerable<KeyValuePair<string, string>> lines)
        {
            sectionLines = new List<string>(lines.Select(p => p.Key + " = " + p.Value));
            properties = null;
            nestedPropertySets = null;
        }

        public IEnumerable<KeyValuePair<string, string>> GetContent()
        {
            if (sectionLines != null)
                return sectionLines.Select(li =>
                {
                    int i = li.IndexOf('=');
                    if (i != -1)
                        return new KeyValuePair<string, string>(li.Substring(0, i).Trim(), li.Substring(i + 1).Trim());
                    else
                        return new KeyValuePair<string, string>(li.Trim(), "");
                });
            else
                return new KeyValuePair<string, string>[0];
        }

        public SlnSectionType SectionType { get; set; }

        SlnSectionType ToSectionType(int curLineNum, string s)
        {
            if (s == "preSolution" || s == "preProject")
                return SlnSectionType.PreProcess;
            if (s == "postSolution" || s == "postProject")
                return SlnSectionType.PostProcess;
            throw new InvalidSolutionFormatException(curLineNum, "Invalid section type: " + s);
        }

        string FromSectionType(bool isProjectSection, SlnSectionType type)
        {
            if (type == SlnSectionType.PreProcess)
                return isProjectSection ? "preProject" : "preSolution";
            else
                return isProjectSection ? "postProject" : "postSolution";
        }

        internal void Read(TextReader reader, string line, ref int curLineNum)
        {
            Line = curLineNum;
            int k = line.IndexOf('(');
            if (k == -1)
                throw new InvalidSolutionFormatException(curLineNum, "Section id missing");
            var tag = line.Substring(0, k).Trim();
            var k2 = line.IndexOf(')', k);
            if (k2 == -1)
                throw new InvalidSolutionFormatException(curLineNum);
            Id = line.Substring(k + 1, k2 - k - 1);

            k = line.IndexOf('=', k2);
            SectionType = ToSectionType(curLineNum, line.Substring(k + 1).Trim());

            var endTag = "End" + tag;

            sectionLines = new List<string>();
            baseIndex = ++curLineNum;
            while ((line = reader.ReadLine()) != null)
            {
                curLineNum++;
                line = line.Trim();
                if (line == endTag)
                    break;
                sectionLines.Add(line);
            }
            if (line == null)
                throw new InvalidSolutionFormatException(curLineNum, "Closing section tag not found");
        }

        void LoadPropertySets()
        {
            if (sectionLines != null)
            {
                SlnPropertySet curSet = null;
                for (int n = 0; n < sectionLines.Count; n++)
                {
                    var line = sectionLines[n];
                    if (string.IsNullOrEmpty(line.Trim()))
                        continue;
                    var i = line.IndexOf('.');
                    if (i == -1)
                        throw new InvalidSolutionFormatException(baseIndex + n);
                    var id = line.Substring(0, i);
                    if (curSet == null || id != curSet.Id)
                    {
                        curSet = new SlnPropertySet(id);
                        nestedPropertySets.Add(curSet);
                    }
                    curSet.ReadLine(line.Substring(i + 1), baseIndex + n);
                }
                sectionLines = null;
            }
        }

        internal void Write(TextWriter writer, string sectionTag)
        {
            if (SkipIfEmpty && IsEmpty)
                return;

            writer.Write("\t");
            writer.Write(sectionTag);
            writer.Write('(');
            writer.Write(Id);
            writer.Write(") = ");
            writer.WriteLine(FromSectionType(sectionTag == "ProjectSection", SectionType));
            if (sectionLines != null)
            {
                foreach (var l in sectionLines)
                    writer.WriteLine("\t\t" + l);
            }
            else if (properties != null)
                properties.Write(writer);
            else if (nestedPropertySets != null)
            {
                foreach (var ps in nestedPropertySets)
                    ps.Write(writer);
            }
            writer.WriteLine("\tEnd" + sectionTag);
        }
    }

    /// <summary>
    /// A collection of properties
    /// </summary>
    public class SlnPropertySet : IDictionary<string, string>
    {
        OrderedDictionary values = new OrderedDictionary();
        bool isMetadata;

        internal bool Processed { get; set; }

        public SlnFile ParentFile
        {
            get { return ParentSection != null ? ParentSection.ParentFile : null; }
        }

        public SlnSection ParentSection { get; set; }

        /// <summary>
        /// Text file line of this section in the original file
        /// </summary>
        /// <value>The line.</value>
        public int Line { get; private set; }

        internal SlnPropertySet()
        {
        }

        /// <summary>
        /// Creates a new property set with the specified ID
        /// </summary>
        /// <param name="id">Identifier.</param>
        public SlnPropertySet(string id)
        {
            Id = id;
        }

        internal SlnPropertySet(bool isMetadata)
        {
            this.isMetadata = isMetadata;
        }

        /// <summary>
        /// Gets a value indicating whether this property set is empty.
        /// </summary>
        /// <value><c>true</c> if this instance is empty; otherwise, <c>false</c>.</value>
        public bool IsEmpty
        {
            get
            {
                return values.Count == 0;
            }
        }

        internal void ReadLine(string line, int currentLine)
        {
            if (Line == 0)
                Line = currentLine;
            int k = line.IndexOf('=');
            if (k != -1)
            {
                var name = line.Substring(0, k).Trim();
                var val = line.Substring(k + 1).Trim();
                values[name] = val;
            }
            else
            {
                line = line.Trim();
                if (!string.IsNullOrWhiteSpace(line))
                    values.Add(line, null);
            }
        }

        internal void Write(TextWriter writer)
        {
            foreach (DictionaryEntry e in values)
            {
                if (!isMetadata)
                    writer.Write("\t\t");
                if (Id != null)
                    writer.Write(Id + ".");
                writer.WriteLine(e.Key + " = " + e.Value);
            }
        }

        /// <summary>
        /// Gets the identifier of the property set
        /// </summary>
        /// <value>The identifier.</value>
        public string Id { get; private set; }

        public string GetValue(string name, string defaultValue = null)
        {
            string res;
            if (TryGetValue(name, out res))
                return res;
            else
                return defaultValue;
        }

        public T GetValue<T>(string name)
        {
            return (T)GetValue(name, typeof(T), default(T));
        }

        public T GetValue<T>(string name, T defaultValue)
        {
            return (T)GetValue(name, typeof(T), defaultValue);
        }

        public object GetValue(string name, Type t, object defaultValue)
        {
            string val;
            if (TryGetValue(name, out val))
            {
                if (t == typeof(bool))
                    return (object)val.Equals("true", StringComparison.OrdinalIgnoreCase);
                if (t.GetTypeInfo().IsEnum)
                    return Enum.Parse(t, val, true);
                if (t.GetTypeInfo().IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    var at = t.GetTypeInfo().GetGenericArguments()[0];
                    if (string.IsNullOrEmpty(val))
                        return null;
                    return Convert.ChangeType(val, at, CultureInfo.InvariantCulture);

                }
                return Convert.ChangeType(val, t, CultureInfo.InvariantCulture);
            }
            else
                return defaultValue;
        }

        public void SetValue(string name, string value, string defaultValue = null, bool preserveExistingCase = false)
        {
            if (value == null && defaultValue == "")
                value = "";
            if (value == defaultValue)
            {
                // if the value is default, only remove the property if it was not already the default
                // to avoid unnecessary project file churn
                string res;
                if (TryGetValue(name, out res) && !string.Equals(defaultValue ?? "", res, preserveExistingCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    Remove(name);
                return;
            }
            string currentValue;
            if (preserveExistingCase && TryGetValue(name, out currentValue) && string.Equals(value, currentValue, StringComparison.OrdinalIgnoreCase))
                return;
            values[name] = value;
        }

        public void SetValue(string name, object value, object defaultValue = null)
        {
            var isDefault = object.Equals(value, defaultValue);
            if (isDefault)
            {
                // if the value is default, only remove the property if it was not already the default
                // to avoid unnecessary project file churn
                if (ContainsKey(name) && (defaultValue == null || !object.Equals(defaultValue, GetValue(name, defaultValue.GetType(), null))))
                    Remove(name);
                return;
            }

            if (value is bool)
                values[name] = (bool)value ? "TRUE" : "FALSE";
            else
                values[name] = Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        void IDictionary<string, string>.Add(string key, string value)
        {
            SetValue(key, value);
        }

        /// <summary>
        /// Determines whether the current instance contains an entry with the specified key
        /// </summary>
        /// <returns><c>true</c>, if key was containsed, <c>false</c> otherwise.</returns>
        /// <param name="key">Key.</param>
        public bool ContainsKey(string key)
        {
            return values.Contains(key);
        }

        /// <summary>
        /// Removes a property
        /// </summary>
        /// <param name="key">Property name</param>
        public bool Remove(string key)
        {
            var wasThere = values.Contains(key);
            values.Remove(key);
            return wasThere;
        }

        /// <summary>
        /// Tries to get the value of a property
        /// </summary>
        /// <returns><c>true</c>, if the property exists, <c>false</c> otherwise.</returns>
        /// <param name="key">Property name</param>
        /// <param name="value">Value.</param>
        public bool TryGetValue(string key, out string value)
        {
            value = (string)values[key];
            return value != null;
        }

        /// <summary>
        /// Gets or sets the value of a property
        /// </summary>
        /// <param name="index">Index.</param>
        public string this[string index]
        {
            get
            {
                return (string)values[index];
            }
            set
            {
                values[index] = value;
            }
        }

        public ICollection<string> Values
        {
            get
            {
                return values.Values.Cast<string>().ToList();
            }
        }

        public ICollection<string> Keys
        {
            get { return values.Keys.Cast<string>().ToList(); }
        }

        void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item)
        {
            SetValue(item.Key, item.Value);
        }

        public void Clear()
        {
            values.Clear();
        }

        internal void ClearExcept(HashSet<string> keys)
        {
            foreach (var k in values.Keys.Cast<string>().Except(keys).ToArray())
                values.Remove(k);
        }

        bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item)
        {
            var val = GetValue(item.Key);
            return val == item.Value;
        }

        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            foreach (DictionaryEntry de in values)
                array[arrayIndex++] = new KeyValuePair<string, string>((string)de.Key, (string)de.Value);
        }

        bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item)
        {
            if (((ICollection<KeyValuePair<string, string>>)this).Contains(item))
            {
                Remove(item.Key);
                return true;
            }
            else
                return false;
        }

        public int Count
        {
            get
            {
                return values.Count;
            }
        }

        internal void SetLines(IEnumerable<KeyValuePair<string, string>> lines)
        {
            values.Clear();
            foreach (var line in lines)
                values[line.Key] = line.Value;
        }

        bool ICollection<KeyValuePair<string, string>>.IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            foreach (DictionaryEntry de in values)
                yield return new KeyValuePair<string, string>((string)de.Key, (string)de.Value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (DictionaryEntry de in values)
                yield return new KeyValuePair<string, string>((string)de.Key, (string)de.Value);
        }
    }

    public class SlnProjectCollection : Collection<SlnProject>
    {
        SlnFile parentFile;

        internal SlnFile ParentFile
        {
            get
            {
                return parentFile;
            }
            set
            {
                parentFile = value;
                foreach (var it in this)
                    it.ParentFile = parentFile;
            }
        }

        public SlnProject GetProject(string id)
        {
            return this.FirstOrDefault(s => s.Id == id);
        }

        public SlnProject GetOrCreateProject(string id)
        {
            var p = this.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (p == null)
            {
                p = new SlnProject { Id = id };
                Add(p);
            }
            return p;
        }

        protected override void InsertItem(int index, SlnProject item)
        {
            base.InsertItem(index, item);
            item.ParentFile = ParentFile;
        }

        protected override void SetItem(int index, SlnProject item)
        {
            base.SetItem(index, item);
            item.ParentFile = ParentFile;
        }

        protected override void RemoveItem(int index)
        {
            var it = this[index];
            it.ParentFile = null;
            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            foreach (var it in this)
                it.ParentFile = null;
            base.ClearItems();
        }
    }

    public class SlnSectionCollection : Collection<SlnSection>
    {
        SlnFile parentFile;

        internal SlnFile ParentFile
        {
            get
            {
                return parentFile;
            }
            set
            {
                parentFile = value;
                foreach (var it in this)
                    it.ParentFile = parentFile;
            }
        }

        public SlnSection GetSection(string id)
        {
            return this.FirstOrDefault(s => s.Id == id);
        }

        public SlnSection GetSection(string id, SlnSectionType sectionType)
        {
            return this.FirstOrDefault(s => s.Id == id && s.SectionType == sectionType);
        }

        public SlnSection GetOrCreateSection(string id, SlnSectionType sectionType)
        {
            if (id == null)
                throw new ArgumentNullException("id");
            var sec = this.FirstOrDefault(s => s.Id == id);
            if (sec == null)
            {
                sec = new SlnSection { Id = id };
                sec.SectionType = sectionType;
                Add(sec);
            }
            return sec;
        }

        public void RemoveSection(string id)
        {
            if (id == null)
                throw new ArgumentNullException("id");
            var s = GetSection(id);
            if (s != null)
                Remove(s);
        }

        protected override void InsertItem(int index, SlnSection item)
        {
            base.InsertItem(index, item);
            item.ParentFile = ParentFile;
        }

        protected override void SetItem(int index, SlnSection item)
        {
            base.SetItem(index, item);
            item.ParentFile = ParentFile;
        }

        protected override void RemoveItem(int index)
        {
            var it = this[index];
            it.ParentFile = null;
            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            foreach (var it in this)
                it.ParentFile = null;
            base.ClearItems();
        }
    }

    public class SlnPropertySetCollection : Collection<SlnPropertySet>
    {
        SlnSection parentSection;

        internal SlnPropertySetCollection(SlnSection parentSection)
        {
            this.parentSection = parentSection;
        }

        public SlnPropertySet GetPropertySet(string id, bool ignoreCase = false)
        {
            var sc = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return this.FirstOrDefault(s => s.Id.Equals(id, sc));
        }

        public SlnPropertySet GetOrCreatePropertySet(string id, bool ignoreCase = false)
        {
            var ps = GetPropertySet(id, ignoreCase);
            if (ps == null)
            {
                ps = new SlnPropertySet(id);
                Add(ps);
            }
            return ps;
        }

        protected override void InsertItem(int index, SlnPropertySet item)
        {
            base.InsertItem(index, item);
            item.ParentSection = parentSection;
        }

        protected override void SetItem(int index, SlnPropertySet item)
        {
            base.SetItem(index, item);
            item.ParentSection = parentSection;
        }

        protected override void RemoveItem(int index)
        {
            var it = this[index];
            it.ParentSection = null;
            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            foreach (var it in this)
                it.ParentSection = null;
            base.ClearItems();
        }
    }

    class InvalidSolutionFormatException : Exception
    {
        public InvalidSolutionFormatException(int line) : base("Invalid format in line " + line)
        {
        }

        public InvalidSolutionFormatException(int line, string msg) : base("Invalid format in line " + line + ": " + msg)
        {

        }
    }

    public enum SlnSectionType
    {
        PreProcess,
        PostProcess
    }
}


// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections;
using System.Globalization;
using System.Reflection;
using Microsoft.DotNet.Cli.Sln.Internal.FileManipulation;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Cli.Sln.Internal
{
    public class SlnFile
    {
        private SlnProjectCollection _projects = new SlnProjectCollection();
        private SlnSectionCollection _sections = new SlnSectionCollection();
        private SlnPropertySet _metadata = new SlnPropertySet(true);
        private int _prefixBlankLines = 1;
        private TextFormatInfo _format = new TextFormatInfo();

        public string FormatVersion { get; set; }
        public string ProductDescription { get; set; }

        public string VisualStudioVersion
        {
            get { return _metadata.GetValue("VisualStudioVersion"); }
            set { _metadata.SetValue("VisualStudioVersion", value); }
        }

        public string MinimumVisualStudioVersion
        {
            get { return _metadata.GetValue("MinimumVisualStudioVersion"); }
            set { _metadata.SetValue("MinimumVisualStudioVersion", value); }
        }

        public string BaseDirectory
        {
            get { return Path.GetDirectoryName(FullPath); }
        }

        public string FullPath { get; set; }

        public SlnPropertySet SolutionConfigurationsSection
        {
            get
            {
                return _sections
                    .GetOrCreateSection("SolutionConfigurationPlatforms", SlnSectionType.PreProcess)
                    .Properties;
            }
        }

        public SlnPropertySetCollection ProjectConfigurationsSection
        {
            get
            {
                return _sections
                    .GetOrCreateSection("ProjectConfigurationPlatforms", SlnSectionType.PostProcess)
                    .NestedPropertySets;
            }
        }

        public SlnSectionCollection Sections
        {
            get { return _sections; }
        }

        public SlnProjectCollection Projects
        {
            get { return _projects; }
        }

        public SlnFile()
        {
            _projects.ParentFile = this;
            _sections.ParentFile = this;
        }

        public static SlnFile Read(string file)
        {
            SlnFile slnFile = new SlnFile();
            slnFile.FullPath = Path.GetFullPath(file);
            slnFile._format = FileUtil.GetTextFormatInfo(file);

            using (var sr = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read)))
            {
                slnFile.Read(sr);
            }

            return slnFile;
        }

        private void Read(TextReader reader)
        {
            const string HeaderPrefix = "Microsoft Visual Studio Solution File, Format Version";

            string line;
            int curLineNum = 0;
            bool globalFound = false;
            bool productRead = false;

            while ((line = reader.ReadLine()) != null)
            {
                curLineNum++;
                line = line.Trim();
                if (line.StartsWith(HeaderPrefix, StringComparison.Ordinal))
                {
                    if (line.Length <= HeaderPrefix.Length)
                    {
                        throw new InvalidSolutionFormatException(
                            curLineNum,
                            LocalizableStrings.FileHeaderMissingVersionError);
                    }

                    FormatVersion = line.Substring(HeaderPrefix.Length).Trim();
                    _prefixBlankLines = curLineNum - 1;
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
                    _projects.Add(p);
                }
                else if (line == "Global")
                {
                    if (globalFound)
                    {
                        throw new InvalidSolutionFormatException(
                            curLineNum,
                            LocalizableStrings.GlobalSectionMoreThanOnceError);
                    }
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
                            _sections.Add(sec);
                        }
                        else // Ignore text that's out of place
                        {
                            continue;
                        }
                    }
                    if (line == null)
                    {
                        throw new InvalidSolutionFormatException(
                            curLineNum,
                            LocalizableStrings.GlobalSectionNotClosedError);
                    }
                }
                else if (line.IndexOf('=') != -1)
                {
                    _metadata.ReadLine(line, curLineNum);
                }
            }
            if (FormatVersion == null)
            {
                throw new InvalidSolutionFormatException(LocalizableStrings.FileHeaderMissingError);
            }
        }

        public void Write(string file = null)
        {
            if (!string.IsNullOrEmpty(file))
            {
                FullPath = Path.GetFullPath(file);
            }
            var sw = new StringWriter();
            Write(sw);
            File.WriteAllText(FullPath, sw.ToString(), Encoding.UTF8);
        }

        private void Write(TextWriter writer)
        {
            writer.NewLine = _format.NewLine;
            for (int n = 0; n < _prefixBlankLines; n++)
            {
                writer.WriteLine();
            }
            writer.WriteLine("Microsoft Visual Studio Solution File, Format Version " + FormatVersion);
            writer.WriteLine("# " + ProductDescription);

            _metadata.Write(writer);

            foreach (var p in _projects)
            {
                p.Write(writer);
            }

            writer.WriteLine("Global");
            foreach (SlnSection s in _sections)
            {
                s.Write(writer, "GlobalSection");
            }
            writer.WriteLine("EndGlobal");
        }
    }

    public class SlnProject
    {
        private SlnSectionCollection _sections = new SlnSectionCollection();

        private SlnFile _parentFile;

        public SlnFile ParentFile
        {
            get
            {
                return _parentFile;
            }
            internal set
            {
                _parentFile = value;
                _sections.ParentFile = _parentFile;
            }
        }

        public string Id { get; set; }
        public string TypeGuid { get; set; }
        public string Name { get; set; }

        private string _filePath;
        public string FilePath
        {
            get
            {
                return _filePath;
            }
            set
            {
                _filePath = PathUtility.RemoveExtraPathSeparators(
                    PathUtility.GetPathWithDirectorySeparator(value));
            }
        }

        public int Line { get; private set; }
        internal bool Processed { get; set; }

        public SlnSectionCollection Sections
        {
            get { return _sections; }
        }

        public SlnSection Dependencies
        {
            get
            {
                return _sections.GetSection("ProjectDependencies", SlnSectionType.PostProcess);
            }
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
                    if (_sections == null)
                    {
                        _sections = new SlnSectionCollection();
                    }
                    var sec = new SlnSection();
                    _sections.Add(sec);
                    sec.Read(reader, line, ref curLineNum);
                }
            }

            throw new InvalidSolutionFormatException(
                curLineNum,
                LocalizableStrings.ProjectSectionNotClosedError);
        }

        private void FindNext(int ln, string line, ref int i, char c)
        {
            var inputIndex = i;
            i = line.IndexOf(c, i);
            if (i == -1)
            {
                throw new InvalidSolutionFormatException(
                    ln,
                    string.Format(LocalizableStrings.ProjectParsingErrorFormatString, c, inputIndex));
            }
        }

        internal void Write(TextWriter writer)
        {
            writer.Write("Project(\"");
            writer.Write(TypeGuid);
            writer.Write("\") = \"");
            writer.Write(Name);
            writer.Write("\", \"");
            writer.Write(PathUtility.GetPathWithBackSlashes(FilePath));
            writer.Write("\", \"");
            writer.Write(Id);
            writer.WriteLine("\"");
            if (_sections != null)
            {
                foreach (SlnSection s in _sections)
                {
                    s.Write(writer, "ProjectSection");
                }
            }
            writer.WriteLine("EndProject");
        }
    }

    public class SlnSection
    {
        private SlnPropertySetCollection _nestedPropertySets;
        private SlnPropertySet _properties;
        private List<string> _sectionLines;
        private int _baseIndex;

        public string Id { get; set; }
        public int Line { get; private set; }

        internal bool Processed { get; set; }

        public SlnFile ParentFile { get; internal set; }

        public bool IsEmpty
        {
            get
            {
                return (_properties == null || _properties.Count == 0) && 
                    (_nestedPropertySets == null || _nestedPropertySets.All(t => t.IsEmpty)) && 
                    (_sectionLines == null || _sectionLines.Count == 0);
            }
        }

        /// <summary>
        /// If true, this section won't be written to the file if it is empty
        /// </summary>
        /// <value><c>true</c> if skip if empty; otherwise, <c>false</c>.</value>
        public bool SkipIfEmpty { get; set; }

        public void Clear()
        {
            _properties = null;
            _nestedPropertySets = null;
            _sectionLines = null;
        }

        public SlnPropertySet Properties
        {
            get
            {
                if (_properties == null)
                {
                    _properties = new SlnPropertySet();
                    _properties.ParentSection = this;
                    if (_sectionLines != null)
                    {
                        foreach (var line in _sectionLines)
                        {
                            _properties.ReadLine(line, Line);
                        }
                        _sectionLines = null;
                    }
                }
                return _properties;
            }
        }

        public SlnPropertySetCollection NestedPropertySets
        {
            get
            {
                if (_nestedPropertySets == null)
                {
                    _nestedPropertySets = new SlnPropertySetCollection(this);
                    if (_sectionLines != null)
                    {
                        LoadPropertySets();
                    }
                }
                return _nestedPropertySets;
            }
        }

        public void SetContent(IEnumerable<KeyValuePair<string, string>> lines)
        {
            _sectionLines = new List<string>(lines.Select(p => p.Key + " = " + p.Value));
            _properties = null;
            _nestedPropertySets = null;
        }

        public IEnumerable<KeyValuePair<string, string>> GetContent()
        {
            if (_sectionLines != null)
            {
                return _sectionLines.Select(li =>
                {
                    int i = li.IndexOf('=');
                    if (i != -1)
                    {
                        return new KeyValuePair<string, string>(li.Substring(0, i).Trim(), li.Substring(i + 1).Trim());
                    }
                    else
                    {
                        return new KeyValuePair<string, string>(li.Trim(), "");
                    }
                });
            }
            else
            {
                return new KeyValuePair<string, string>[0];
            }
        }

        public SlnSectionType SectionType { get; set; }

        private SlnSectionType ToSectionType(int curLineNum, string s)
        {
            if (s == "preSolution" || s == "preProject")
            {
                return SlnSectionType.PreProcess;
            }
            if (s == "postSolution" || s == "postProject")
            {
                return SlnSectionType.PostProcess;
            }
            throw new InvalidSolutionFormatException(
                curLineNum, 
                String.Format(LocalizableStrings.InvalidSectionTypeError, s));
        }

        private string FromSectionType(bool isProjectSection, SlnSectionType type)
        {
            if (type == SlnSectionType.PreProcess)
            {
                return isProjectSection ? "preProject" : "preSolution";
            }
            else
            {
                return isProjectSection ? "postProject" : "postSolution";
            }
        }

        internal void Read(TextReader reader, string line, ref int curLineNum)
        {
            Line = curLineNum;
            int k = line.IndexOf('(');
            if (k == -1)
            {
                throw new InvalidSolutionFormatException(
                    curLineNum,
                    LocalizableStrings.SectionIdMissingError);
            }
            var tag = line.Substring(0, k).Trim();
            var k2 = line.IndexOf(')', k);
            if (k2 == -1)
            {
                throw new InvalidSolutionFormatException(
                    curLineNum,
                    LocalizableStrings.SectionIdMissingError);
            }
            Id = line.Substring(k + 1, k2 - k - 1);

            k = line.IndexOf('=', k2);
            SectionType = ToSectionType(curLineNum, line.Substring(k + 1).Trim());

            var endTag = "End" + tag;

            _sectionLines = new List<string>();
            _baseIndex = ++curLineNum;
            while ((line = reader.ReadLine()) != null)
            {
                curLineNum++;
                line = line.Trim();
                if (line == endTag)
                {
                    break;
                }
                _sectionLines.Add(line);
            }
            if (line == null)
            {
                throw new InvalidSolutionFormatException(
                    curLineNum,
                    LocalizableStrings.ClosingSectionTagNotFoundError);
            }
        }

        private void LoadPropertySets()
        {
            if (_sectionLines != null)
            {
                SlnPropertySet curSet = null;
                for (int n = 0; n < _sectionLines.Count; n++)
                {
                    var line = _sectionLines[n];
                    if (string.IsNullOrEmpty(line.Trim()))
                    {
                        continue;
                    }
                    var i = line.IndexOf('.');
                    if (i == -1)
                    {
                        throw new InvalidSolutionFormatException(
                            _baseIndex + n,
                            string.Format(LocalizableStrings.InvalidPropertySetFormatString, '.'));
                    }
                    var id = line.Substring(0, i);
                    if (curSet == null || id != curSet.Id)
                    {
                        curSet = new SlnPropertySet(id);
                        _nestedPropertySets.Add(curSet);
                    }
                    curSet.ReadLine(line.Substring(i + 1), _baseIndex + n);
                }
                _sectionLines = null;
            }
        }

        internal void Write(TextWriter writer, string sectionTag)
        {
            if (SkipIfEmpty && IsEmpty)
            {
                return;
            }

            writer.Write("\t");
            writer.Write(sectionTag);
            writer.Write('(');
            writer.Write(Id);
            writer.Write(") = ");
            writer.WriteLine(FromSectionType(sectionTag == "ProjectSection", SectionType));
            if (_sectionLines != null)
            {
                foreach (var l in _sectionLines)
                {
                    writer.WriteLine("\t\t" + l);
                }
            }
            else if (_properties != null)
            {
                _properties.Write(writer);
            }
            else if (_nestedPropertySets != null)
            {
                foreach (var ps in _nestedPropertySets)
                {
                    ps.Write(writer);
                }
            }
            writer.WriteLine("\tEnd" + sectionTag);
        }
    }

    /// <summary>
    /// A collection of properties
    /// </summary>
    public class SlnPropertySet : IDictionary<string, string>
    {
        private OrderedDictionary _values = new OrderedDictionary();
        private bool _isMetadata;

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
            _isMetadata = isMetadata;
        }

        public bool IsEmpty
        {
            get
            {
                return _values.Count == 0;
            }
        }

        internal void ReadLine(string line, int currentLine)
        {
            if (Line == 0)
            {
                Line = currentLine;
            }
            int k = line.IndexOf('=');
            if (k != -1)
            {
                var name = line.Substring(0, k).Trim();
                var val = line.Substring(k + 1).Trim();
                _values[name] = val;
            }
            else
            {
                line = line.Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _values.Add(line, null);
                }
            }
        }

        internal void Write(TextWriter writer)
        {
            foreach (DictionaryEntry e in _values)
            {
                if (!_isMetadata)
                {
                    writer.Write("\t\t");
                }
                if (Id != null)
                {
                    writer.Write(Id + ".");
                }
                writer.WriteLine(e.Key + " = " + e.Value);
            }
        }

        public string Id { get; private set; }

        public string GetValue(string name, string defaultValue = null)
        {
            string res;
            if (TryGetValue(name, out res))
            {
                return res;
            }
            else
            {
                return defaultValue;
            }
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
                {
                    return (object)val.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
                if (t.GetTypeInfo().IsEnum)
                {
                    return Enum.Parse(t, val, true);
                }
                if (t.GetTypeInfo().IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    var at = t.GetTypeInfo().GetGenericArguments()[0];
                    if (string.IsNullOrEmpty(val))
                    {
                        return null;
                    }
                    return Convert.ChangeType(val, at, CultureInfo.InvariantCulture);

                }
                return Convert.ChangeType(val, t, CultureInfo.InvariantCulture);
            }
            else
            {
                return defaultValue;
            }
        }

        public void SetValue(string name, string value, string defaultValue = null, bool preserveExistingCase = false)
        {
            if (value == null && defaultValue == "")
            {
                value = "";
            }
            if (value == defaultValue)
            {
                // if the value is default, only remove the property if it was not already the default
                // to avoid unnecessary project file churn
                string res;
                if (TryGetValue(name, out res) &&
                    !string.Equals(defaultValue ?? "",
                        res, preserveExistingCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                {
                    Remove(name);
                }
                return;
            }
            string currentValue;
            if (preserveExistingCase && TryGetValue(name, out currentValue) &&
                string.Equals(value, currentValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            _values[name] = value;
        }

        public void SetValue(string name, object value, object defaultValue = null)
        {
            var isDefault = object.Equals(value, defaultValue);
            if (isDefault)
            {
                // if the value is default, only remove the property if it was not already the default
                // to avoid unnecessary project file churn
                if (ContainsKey(name) && (defaultValue == null ||
                    !object.Equals(defaultValue, GetValue(name, defaultValue.GetType(), null))))
                {
                    Remove(name);
                }
                return;
            }

            if (value is bool)
            {
                _values[name] = (bool)value ? "TRUE" : "FALSE";
            }
            else
            {
                _values[name] = Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        void IDictionary<string, string>.Add(string key, string value)
        {
            SetValue(key, value);
        }

        public bool ContainsKey(string key)
        {
            return _values.Contains(key);
        }

        public bool Remove(string key)
        {
            var wasThere = _values.Contains(key);
            _values.Remove(key);
            return wasThere;
        }

        public bool TryGetValue(string key, out string value)
        {
            value = (string)_values[key];
            return value != null;
        }

        public string this[string index]
        {
            get
            {
                return (string)_values[index];
            }
            set
            {
                _values[index] = value;
            }
        }

        public ICollection<string> Values
        {
            get
            {
                return _values.Values.Cast<string>().ToList();
            }
        }

        public ICollection<string> Keys
        {
            get { return _values.Keys.Cast<string>().ToList(); }
        }

        void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item)
        {
            SetValue(item.Key, item.Value);
        }

        public void Clear()
        {
            _values.Clear();
        }

        internal void ClearExcept(HashSet<string> keys)
        {
            foreach (var k in _values.Keys.Cast<string>().Except(keys).ToArray())
            {
                _values.Remove(k);
            }
        }

        bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item)
        {
            var val = GetValue(item.Key);
            return val == item.Value;
        }

        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            foreach (DictionaryEntry de in _values)
            {
                array[arrayIndex++] = new KeyValuePair<string, string>((string)de.Key, (string)de.Value);
            }
        }

        bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item)
        {
            if (((ICollection<KeyValuePair<string, string>>)this).Contains(item))
            {
                Remove(item.Key);
                return true;
            }
            else
            {
                return false;
            }
        }

        public int Count
        {
            get
            {
                return _values.Count;
            }
        }

        internal void SetLines(IEnumerable<KeyValuePair<string, string>> lines)
        {
            _values.Clear();
            foreach (var line in lines)
            {
                _values[line.Key] = line.Value;
            }
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
            foreach (DictionaryEntry de in _values)
            {
                yield return new KeyValuePair<string, string>((string)de.Key, (string)de.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (DictionaryEntry de in _values)
            {
                yield return new KeyValuePair<string, string>((string)de.Key, (string)de.Value);
            }
        }
    }

    public class SlnProjectCollection : Collection<SlnProject>
    {
        private SlnFile _parentFile;

        internal SlnFile ParentFile
        {
            get
            {
                return _parentFile;
            }
            set
            {
                _parentFile = value;
                foreach (var it in this)
                {
                    it.ParentFile = _parentFile;
                }
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
            {
                it.ParentFile = null;
            }
            base.ClearItems();
        }
    }

    public class SlnSectionCollection : Collection<SlnSection>
    {
        private SlnFile _parentFile;

        internal SlnFile ParentFile
        {
            get
            {
                return _parentFile;
            }
            set
            {
                _parentFile = value;
                foreach (var it in this)
                {
                    it.ParentFile = _parentFile;
                }
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
            {
                throw new ArgumentNullException("id");
            }
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
            {
                throw new ArgumentNullException("id");
            }
            var s = GetSection(id);
            if (s != null)
            {
                Remove(s);
            }
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
            {
                it.ParentFile = null;
            }
            base.ClearItems();
        }
    }

    public class SlnPropertySetCollection : Collection<SlnPropertySet>
    {
        private SlnSection _parentSection;

        internal SlnPropertySetCollection(SlnSection parentSection)
        {
            _parentSection = parentSection;
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
            item.ParentSection = _parentSection;
        }

        protected override void SetItem(int index, SlnPropertySet item)
        {
            base.SetItem(index, item);
            item.ParentSection = _parentSection;
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
            {
                it.ParentSection = null;
            }
            base.ClearItems();
        }
    }

    public class InvalidSolutionFormatException : Exception
    {
        public InvalidSolutionFormatException(string details)
            : base(details)
        {
        }

        public InvalidSolutionFormatException(int line, string details)
            : base(string.Format(LocalizableStrings.ErrorMessageFormatString, line, details))
        {
        }
    }

    public enum SlnSectionType
    {
        PreProcess,
        PostProcess
    }
}


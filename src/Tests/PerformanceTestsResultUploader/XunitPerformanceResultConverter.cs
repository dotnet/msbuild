using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Reporting;

namespace PerformanceTestsResultUploader
{
    public static class XunitPerformanceResultConverter
    {
        public static List<Test> GenerateTestsFromXml(StreamReader stream)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ScenarioBenchmark));

            ScenarioBenchmark scenarioBenchmark = (ScenarioBenchmark)serializer.Deserialize(stream);
            Console.WriteLine(scenarioBenchmark.Name);
            List<Test> tests = new List<Test>();
            foreach (ScenarioBenchmarkTest scenarioBenchmarkTest in scenarioBenchmark.Tests)
            {
                Test test = new Test();
                test.Categories.Add("DotnetCoreSdk");
                test.Name = scenarioBenchmark.Name + "." + scenarioBenchmarkTest.Name;
                test.Counters.Add(new Counter
                {
                    Name = scenarioBenchmarkTest.Performance.metrics.ExecutionTime.displayName,
                    TopCounter = true,
                    DefaultCounter = true,
                    HigherIsBetter = false,
                    MetricName = scenarioBenchmarkTest.Performance.metrics.ExecutionTime.unit,
                    Results = scenarioBenchmarkTest
                        .Performance
                        .iterations
                        .Select(i => decimal.ToDouble(i.ExecutionTime))
                        .ToList()
                });

                tests.Add(test);
            }

            return tests;
        }

        public static List<Test> BatchGenerateTests(DirectoryInfo directory)
        {
            return directory.EnumerateFiles()
                .Where(f => string.Equals(Path.GetExtension(f.FullName), ".xml", StringComparison.OrdinalIgnoreCase))
                .SelectMany(xmlFile => GenerateTestsFromXml(new StreamReader(xmlFile.FullName))).ToList();
        }
    }
}

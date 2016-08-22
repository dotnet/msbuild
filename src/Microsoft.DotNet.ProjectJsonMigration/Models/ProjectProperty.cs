namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class ProjectProperty
    {
        public string Name { get; }
        public string Value { get; }

        public ProjectProperty(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}
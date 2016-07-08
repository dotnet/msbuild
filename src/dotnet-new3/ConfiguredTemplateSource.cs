//using Microsoft.TemplateEngine.Abstractions;

//namespace dotnet_new3
//{
//    internal class ConfiguredTemplateSource : IConfiguredTemplateSource
//    {
//        private readonly string _location;

//        public ConfiguredTemplateSource(ITemplateSource source, string alias, string location)
//        {
//            Source = source;
//            _location = location;
//            Alias = alias;
//        }

//        public string Alias { get; }

//        public IDisposable<ITemplateSourceFolder> Root => Source.RootFor(_location);

//        public string Location => _location;

//        public ITemplateSource Source { get; }

//        public IConfiguredTemplateSource ParentSource => null;
//    }
//}
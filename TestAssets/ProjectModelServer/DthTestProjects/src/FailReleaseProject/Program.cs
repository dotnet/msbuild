namespace FailReleaseProject
{
    public class Program
    {
        public int Main(string[] args)
        {
#if RELEASE
            // fail the compilation under Release configuration
            i
#endif
            return 0;
        }
    }
}

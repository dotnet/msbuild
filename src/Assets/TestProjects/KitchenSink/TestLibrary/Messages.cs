namespace ClassLibrary1
{
    /// <summary>
    /// Provides messages for an application.
    /// </summary>
    public class Messages
    {
        /// <summary>
        /// Gets a greeting message in the current language.
        /// </summary>
        /// <returns>
        /// The greeting message.
        /// </returns>
        public string GetGreeting()
        {
            return Resources.Greeting;
        }
    }
}

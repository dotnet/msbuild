namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    using System.Text.RegularExpressions;
    using Framework = Microsoft.Build.Framework;
    using Utilities = Microsoft.Build.Utilities;

    public class RegexEscape : Utilities.Task
    {
        private string m_text = null;
        private string m_resultRegularExpression = string.Empty;

        [Framework.Required]
        public string Text
        {
            get { return m_text; }
            set { m_text = value; }
        }

        [Framework.Output]
        public string Result
        {
            get { return m_resultRegularExpression; }

        }

        // utility function to take the Path and change that to a valid regular expression
        public override bool Execute()
        {
            if (Text != null)
            {
                m_resultRegularExpression = Regex.Escape(Text);
            }
            return true;
        }
    }
}

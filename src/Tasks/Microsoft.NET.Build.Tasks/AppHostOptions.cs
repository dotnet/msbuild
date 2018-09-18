namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Options to customize the apphost.
    /// </summary>
    internal class AppHostOptions
    {
        /// <summary>
        /// If this is set to true and the apphost is a Windows PE executable, it will have its subsystem set to GUI.
        /// By default apphost's subsystem on Windows is set to CUI (Console).
        /// </summary>
        public bool WindowsGraphicalUserInterface { get; set; }
    }
}

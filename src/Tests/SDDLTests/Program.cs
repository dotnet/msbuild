// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Installer.Windows;

namespace SDDLTests
{
    /// <summary>
    /// Console application containing a manual test to verify security descriptors used by workloads when caching packages.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class SDDLTests
    {
        /// <summary>
        /// The full path of ProgramData.
        /// </summary>
        private static readonly string s_programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        /// <summary>
        /// Directory under the user's %temp% directory where the test asset will be created.
        /// </summary>
        private static readonly string s_userTestDirectory = Path.Combine(Path.GetTempPath(), "SDDLTest");

        /// <summary>
        /// The filename and extension of the test asset.
        /// </summary>
        private static readonly string s_testAsset = "test.txt";

        /// <summary>
        /// Regular expression to capture parts of a security descriptor in SDDL format.
        /// </summary>
        private static readonly string s_SDDL_Pattern = @"O:(?<O_SID>.*?(?=G:))G:(?<G_SID>.*?(?=D:|S:|$))(D:(?<DACL_FLAGS>.*?(?=\())(?<DACL>\(.*?\))*)?(S:(?<SACL_FLAGS>.*?(?=\())(?<SACL>\(.*?\))*)?";

        /// <summary>
        /// The .NET directory under ProgramData. Typically this would be named 'dotnet'. 
        /// </summary>
        private static readonly string s_cacheRootDirectory = Path.Combine(s_programData, "SDDLTest");

        /// <summary>
        /// The root directory under ProgramData where workload related files are stored.
        /// </summary>
        private static readonly string s_workloadsCacheDirectory = Path.Combine(s_cacheRootDirectory, "workloads");

        /// <summary>
        /// The path of the directory under the cache root where the test asset will be placed. For workload packs this would
        /// typically include the package ID and version.
        /// </summary>
        private static readonly string s_workloadPackCacheDirectory = Path.Combine(s_workloadsCacheDirectory, "test.workload.pack", "1.2.3-preview5");

        /// <summary>
        /// The full path to the test asset under the user temp directory.
        /// </summary>
        private static readonly string s_userTestAssetPath = Path.Combine(s_userTestDirectory, s_testAsset);

        /// <summary>
        /// The full path to the test asset under the cache directory.
        /// </summary>
        private static readonly string s_cachedTestAssetPath = Path.Combine(s_workloadPackCacheDirectory, s_testAsset);

        /// <summary>
        /// Access control sections to retrieve from security descriptors: owner, group and access control lists.
        /// </summary>
        private static readonly AccessControlSections s_accessControlSections = AccessControlSections.Group | AccessControlSections.Owner |
            AccessControlSections.Access;

        /// <summary>
        /// Writes a message to the console's error stream.
        /// </summary>
        /// <param name="message">The message to write.</param>
        private static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// Extracts the owner, group and DACL (individual ACES) of the security descriptor.
        /// </summary>
        /// <param name="sddlDescriptor">The SDDL formatted security descriptor.</param>
        /// <returns>The owner, group and DACL.</returns>
        /// <exception cref="FormatException">If the descriptor string cannot be parsed.</exception>
        private static (string ownerSID, string groupSID, IEnumerable<string> DACL_ACEs) GetDescriptorParts(string sddlDescriptor)
        {
            Match m = Regex.Match(sddlDescriptor, s_SDDL_Pattern);

            if (m.Success)
            {
                string owner = m.Groups.ContainsKey("O_SID") ? m.Groups["O_SID"].Value : string.Empty;
                string group = m.Groups.ContainsKey("G_SID") ? m.Groups["G_SID"].Value : string.Empty;
                IEnumerable<string> aces = m.Groups.ContainsKey("DACL") ? m.Groups["DACL"].Captures.Select(c => c.Value.Trim('(', ')')) :
                    Enumerable.Empty<string>();

                return (owner, group, aces);
            }

            throw new FormatException("Invalid SDDL descriptor string.");
        }

        /// <summary>
        /// Determines whether the current user has the Administrator role.
        /// </summary>
        /// <returns><see langword="true"/> if the user has the Administrator role.</returns>
        private static bool IsAdministrator()
        {
            WindowsPrincipal principal = new(WindowsIdentity.GetCurrent());

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Creates a directory in the user's temporary directory along with an empty file. This
        /// simulates the behavior when a workload pack is downloaded and extracted.
        /// </summary>
        /// <returns>
        /// The full path to the test file under the users temporary directory.
        /// </returns>
        private static string CreateTestAsset()
        {
            // Always remove previous directories to ensure we're running in a clean state.
            if (Directory.Exists(s_userTestDirectory))
            {
                Directory.Delete(s_userTestDirectory, recursive: true);
            }

            Directory.CreateDirectory(s_userTestDirectory);

            string testAssetPath = Path.Combine(s_userTestDirectory, "test.txt");
            using StreamWriter sw = File.CreateText(testAssetPath);

            Console.WriteLine($"Created test asset at: {testAssetPath}");

            // Report the directory and file security descriptors
            DirectorySecurity ds = new(s_userTestDirectory, s_accessControlSections);
            FileSecurity fs = new(testAssetPath, s_accessControlSections);

            Console.WriteLine($"Directory descriptor: {ds.GetSecurityDescriptorSddlForm(s_accessControlSections)}");
            Console.WriteLine($"     File descriptor: {fs.GetSecurityDescriptorSddlForm(s_accessControlSections)}");

            return Path.GetFullPath(testAssetPath);
        }

        /// <summary>
        /// Relocate the test asset from the user directory to ProgramData.
        /// </summary>
        private static void RelocateAndSecureAsset()
        {
            MsiPackageCache.CreateSecureDirectory(s_workloadPackCacheDirectory);
            MsiPackageCache.MoveAndSecureFile(s_userTestAssetPath, s_cachedTestAssetPath);
        }

        /// <summary>
        /// Verify a security descriptor against a set of expected values.
        /// </summary>
        /// <param name="path">The full path of the directory to verify.</param>
        /// <param name="expectedOwnerSID">The expected owner SID in SDDL format.</param>
        /// <param name="expectedGroupSID">The expected group SID in SDDL format.</param>
        /// <param name="expectedNumberOfACEsInDACL">The number of ACEs to expect in the DACL.</param>
        /// <param name="expectedACEs">The set of exapected ACEs in SDDL format (no parantheses). This does not have to be the full set.</param>
        private static void VerifySecurityDescriptor(string sddlDescriptor, string expectedOwnerSID,
            string expectedGroupSID, int expectedNumberOfACEsInDACL, params string[] expectedACEs)
        {
            Console.WriteLine($"Verifying descriptor: {sddlDescriptor}");
            (string owner, string group, IEnumerable<string> ACEs) d = GetDescriptorParts(sddlDescriptor);

            Assert.True(expectedOwnerSID == d.owner, $"Expected owner SID to be {expectedOwnerSID}. Actual value: {d.owner}");
            Assert.True(expectedGroupSID == d.group, $"Expected group SID to be {expectedGroupSID}. Actual value: {d.group}");
            Assert.True(d.ACEs.Count() == expectedNumberOfACEsInDACL, $"Expected {expectedNumberOfACEsInDACL}. Actual: {d.ACEs.Count()}");

            foreach (string ace in expectedACEs)
            {
                Assert.True(d.ACEs.Contains(ace), $"Expected DACL to contain {ace}, but it did not.");
            }
        }

        /// <summary>
        /// Verify a directory's security descriptor against a set of expected values.
        /// </summary>
        /// <param name="path">The full path of the directory to verify.</param>
        /// <param name="expectedOwnerSID">The expected owner SID in SDDL format.</param>
        /// <param name="expectedGroupSID">The expected group SID in SDDL format.</param>
        /// <param name="expectedNumberOfACEsInDACL">The number of ACEs to expect in the DACL.</param>
        /// <param name="expectedACEs">The set of exapected ACEs in SDDL format (no parantheses). This does not have to be the full set.</param>
        private static void VerifyDirectorySecurityDescriptor(string path, string expectedOwnerSID,
            string expectedGroupSID, int expectedNumberOfACEsInDACL, params string[] expectedACEs)
        {
            Console.WriteLine($"Verifying directory expectations for {path}");
            DirectorySecurity ds = new(path, s_accessControlSections);
            string descriptor = ds.GetSecurityDescriptorSddlForm(s_accessControlSections);
            VerifySecurityDescriptor(descriptor, expectedOwnerSID, expectedGroupSID, expectedNumberOfACEsInDACL, expectedACEs);
        }

        /// <summary>
        /// Verify a files's security descriptor against a set of expected values.
        /// </summary>
        /// <param name="path">The full path of the directory to verify.</param>
        /// <param name="expectedOwnerSID">The expected owner SID in SDDL format.</param>
        /// <param name="expectedGroupSID">The expected group SID in SDDL format.</param>
        /// <param name="expectedNumberOfACEsInDACL">The number of ACEs to expect in the DACL.</param>
        /// <param name="expectedACEs">The set of exapected ACEs in SDDL format (no parantheses). This does not have to be the full set.</param>
        private static void VerifyFileSecurityDescriptor(string path, string expectedOwnerSID,
            string expectedGroupSID, int expectedNumberOfACEsInDACL, params string[] expectedACEs)
        {
            Console.WriteLine($"Verifying file expectations for {path}");
            FileSecurity ds = new(path, s_accessControlSections);
            string descriptor = ds.GetSecurityDescriptorSddlForm(s_accessControlSections);
            VerifySecurityDescriptor(descriptor, expectedOwnerSID, expectedGroupSID, expectedNumberOfACEsInDACL, expectedACEs);
        }

        /// <summary>
        /// Verify file and directory security descriptors against expected values.
        /// </summary>
        private static void VerifyDescriptors()
        {
            // Dump the descriptor of ProgramData since it's useful for analyzing.
            DirectorySecurity ds = new(s_programData, s_accessControlSections);
            string descriptor = ds.GetSecurityDescriptorSddlForm(s_accessControlSections);
            Console.WriteLine($" Directory: {s_programData}");
            Console.WriteLine($"Descriptor: {descriptor}");

            VerifyDirectorySecurityDescriptor(s_cacheRootDirectory, "BA", "BA", 4, "A;OICI;0x1200a9;;;WD", "A;OICI;FA;;;SY", "A;OICI;FA;;;BA", "A;OICI;0x1200a9;;;BU");
            VerifyDirectorySecurityDescriptor(s_workloadsCacheDirectory, "BA", "BA", 4, "A;OICIID;0x1200a9;;;WD", "A;OICIID;FA;;;SY", "A;OICIID;FA;;;BA", "A;OICIID;0x1200a9;;;BU");
            VerifyDirectorySecurityDescriptor(s_workloadPackCacheDirectory, "BA", "BA", 4, "A;OICIID;0x1200a9;;;WD", "A;OICIID;FA;;;SY", "A;OICIID;FA;;;BA", "A;OICIID;0x1200a9;;;BU");
            VerifyFileSecurityDescriptor(s_cachedTestAssetPath, "BA", "BA", 4, "A;ID;0x1200a9;;;WD", "A;ID;FA;;;SY", "A;ID;FA;;;BA", "A;ID;0x1200a9;;;BU");
        }

        static void Main(string[] args)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("This test is only applicable to Windows.");
                Environment.Exit(-1);
            }

            WindowsIdentity identity = WindowsIdentity.GetCurrent();

            Console.WriteLine($"Running tests as {identity.Name}, admin: {IsAdministrator()}, system: {identity.IsSystem}");

            if (IsAdministrator())
            {
                if (args.Length > 0 && args[0] == "elevate")
                {
                    // SCENARIO 1B: The installer packages from the user's temp directory are being moved to
                    // the package cache under ProgramData through an elevated process.
                    try
                    {
                        RelocateAndSecureAsset();
                    }
                    catch
                    {
                        // Return an error if we couldn't move and secure the assets.
                        Environment.Exit(-2);
                    }
                }
                else if (args.Length == 0)
                {
                    try
                    {
                        // SCENARIO 2: Full test is running as administrator. This is similar to user running
                        // a dotnet workload command from an administrator prompt, local SYSTEM or running inside
                        // Windows Sandbox.
                        CreateTestAsset();
                        RelocateAndSecureAsset();
                        VerifyDescriptors();
                    }
                    catch (Exception e)
                    {
                        WriteError(e.Message);
                        Environment.Exit(-1);
                    }
                }
                else
                {
                    // Invalid scenario
                    WriteError("Invalid scenario!");
                    Environment.Exit(-1);
                }
            }
            else
            {
                // SCENARIO 1A: The user is running with normal privileges. Workload packages are downloaded to
                // the user's temp directory before the CLI elevates and moves the installers into a secured cache
                // under ProgramData.
                try
                {
                    CreateTestAsset();

                    // Launch the elevated portion of the test.
                    ProcessStartInfo startInfo = new($@"""{Environment.ProcessPath}""",
                        $@"""{Assembly.GetExecutingAssembly().Location}"" elevate")
                    {
                        Verb = "runas",
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process p = new()
                    {
                        StartInfo = startInfo,
                    };

                    if (p.Start())
                    {
                        p.WaitForExit();

                        if (p.ExitCode != 0)
                        {
                            WriteError($"Elevated process exited with {p.ExitCode}");
                            Environment.Exit(-1);
                        }

                        VerifyDescriptors();
                    }
                    else
                    {
                        WriteError("Failed to start elevated process.");
                        Environment.Exit(-1);
                    }
                }
                catch (Exception e)
                {
                    WriteError(e.Message);
                    Environment.Exit(-1);
                }
            }
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tests.Utilities.Tests
{
    public class MockFileSystemTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DirectoryExistsShouldCountTheSameNameFile(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string nestedFilePath = Path.Combine(directory, "filename");
            fileSystem.File.CreateEmptyFile(nestedFilePath);

            fileSystem.Directory.Exists(nestedFilePath).Should().BeFalse();
        }

        [WindowsOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void DifferentDirectorySeparatorShouldBeSameFile(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);

            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string nestedFilePath = $"{directory}\\filename";
            fileSystem.File.CreateEmptyFile(nestedFilePath);

            fileSystem.File.Exists($"{directory}/filename").Should().BeTrue();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenDirectoryExistsShouldCreateEmptyFile(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);

            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string nestedFilePath = Path.Combine(directory, "filename");
            fileSystem.File.CreateEmptyFile(nestedFilePath);

            fileSystem.File.Exists(nestedFilePath).Should().BeTrue();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenDirectoryDoesNotExistsCreateEmptyFileShouldThrow(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);

            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string nestedFilePath = Path.Combine(directory, "nonExits", "filename");
            Action a = () => fileSystem.File.CreateEmptyFile(nestedFilePath);

            a.Should().Throw<DirectoryNotFoundException>().And.Message.Should()
                .Contain("Could not find a part of the path");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DirectoryExistsWithRelativePathShouldCountTheSameNameFile(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);

            string directory = fileSystem.Directory.GetCurrentDirectory();
            fileSystem.File.CreateEmptyFile("file");

            fileSystem.File.Exists(Path.Combine(directory, "file")).Should().BeTrue();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WithRelativePathShouldCreateDirectory(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);

            string directory = fileSystem.Directory.GetCurrentDirectory();
            fileSystem.Directory.CreateDirectory("dir");

            fileSystem.Directory.Exists(Path.Combine(directory, "dir")).Should().BeTrue();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ShouldCreateDirectory(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);

            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;

            fileSystem.Directory.Exists(directory).Should().BeTrue();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CreateDirectoryWhenExistsShouldNotThrow(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);

            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;

            Action a = () => fileSystem.Directory.CreateDirectory(directory);
            a.Should().NotThrow();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CreateDirectoryWhenExistsSameNameFileShouldThrow(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);

            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;

            string path = Path.Combine(directory, "sub");
            fileSystem.File.CreateEmptyFile(path);
            Action a = () => fileSystem.Directory.CreateDirectory(path);
            a.Should().Throw<IOException>();
        }

        [WindowsOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void DirectoryDoesNotExistShouldThrow(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);

            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string nestedFilePath = Path.Combine(directory, "subfolder", "filename");
            Action a = () => fileSystem.File.CreateEmptyFile(nestedFilePath);
            a.Should().Throw<DirectoryNotFoundException>();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FileReadAllTextWhenExists(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            const string content = "content";
            string path = Path.Combine(directory, Path.GetRandomFileName());
            fileSystem.File.WriteAllText(path, content);

            fileSystem.File.ReadAllText(path).Should().Be(content);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FileThrowsWhenTryToReadNonExistFile(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string path = Path.Combine(directory, Path.GetRandomFileName());

            Action a = () => fileSystem.File.ReadAllText(path);
            a.Should().Throw<FileNotFoundException>().And.Message.Should().Contain("Could not find file");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FileThrowsWhenTryToReadADictionary(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = Path.Combine(
                fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath,
                Path.GetRandomFileName());
            fileSystem.Directory.CreateDirectory(directory);

            Action a = () => fileSystem.File.ReadAllText(directory);
            a.Should().Throw<UnauthorizedAccessException>().And.Message.Should().Contain("Access to the path");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FileOpenReadWhenExists(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            const string content = "content";
            string path = Path.Combine(directory, Path.GetRandomFileName());
            fileSystem.File.WriteAllText(path, content);

            string fullString = "";
            using (Stream fs = fileSystem.File.OpenRead(path))
            {
                byte[] b = new byte[1024];
                UTF8Encoding temp = new UTF8Encoding(true);

                while (fs.Read(b, 0, b.Length) > 0)
                {
                    fullString += temp.GetString(b);
                }
            }

            fullString.Should().StartWith(content);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MoveFileWhenBothSourceAndDestinationExist(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string sourceFile = Path.Combine(directory, Path.GetRandomFileName());
            fileSystem.File.CreateEmptyFile(sourceFile);
            string destinationFile = Path.Combine(directory, Path.GetRandomFileName());

            fileSystem.File.Move(sourceFile, destinationFile);

            fileSystem.File.Exists(sourceFile).Should().BeFalse();
            fileSystem.File.Exists(destinationFile).Should().BeTrue();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MoveFileThrowsWhenSourceDoesNotExist(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string sourceFile = Path.Combine(directory, Path.GetRandomFileName());

            string destinationFile = Path.Combine(directory, Path.GetRandomFileName());

            Action a = () => fileSystem.File.Move(sourceFile, destinationFile);

            a.Should().Throw<FileNotFoundException>().And.Message.Should().Contain("Could not find file");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MoveFileThrowsWhenSourceIsADirectory(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string badSourceFile = Path.Combine(directory, Path.GetRandomFileName());
            fileSystem.Directory.CreateDirectory(badSourceFile);

            string destinationFile = Path.Combine(directory, Path.GetRandomFileName());

            Action a = () => fileSystem.File.Move(badSourceFile, destinationFile);

            a.Should().Throw<FileNotFoundException>().And.Message.Should().Contain("Could not find file");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MoveFileThrowsWhenDestinationDirectoryDoesNotExist(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string sourceFile = Path.Combine(directory, Path.GetRandomFileName());
            fileSystem.File.CreateEmptyFile(sourceFile);

            string destinationFile = Path.Combine(directory, Path.GetRandomFileName(), Path.GetRandomFileName());

            Action a = () => fileSystem.File.Move(sourceFile, destinationFile);

            a.Should().Throw<DirectoryNotFoundException>()
                .And.Message.Should().Contain("Could not find a part of the path");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CopyFileWhenBothSourceAndDestinationDirectoryExist(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string sourceFile = Path.Combine(directory, Path.GetRandomFileName());
            fileSystem.File.WriteAllText(sourceFile, "content");
            string destinationFile = Path.Combine(directory, Path.GetRandomFileName());

            fileSystem.File.Copy(sourceFile, destinationFile);

            fileSystem.File.ReadAllText(sourceFile).Should().Be(fileSystem.File.ReadAllText(destinationFile));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CopyFileThrowsWhenSourceDoesNotExist(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string sourceFile = Path.Combine(directory, Path.GetRandomFileName());
            string destinationFile = Path.Combine(directory, Path.GetRandomFileName());

            Action a = () => fileSystem.File.Copy(sourceFile, destinationFile);

            a.Should().Throw<FileNotFoundException>().And.Message.Should().Contain("Could not find file");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CopyFileThrowsWhenSourceIsADirectory(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string badSourceFile = Path.Combine(directory, Path.GetRandomFileName());
            fileSystem.Directory.CreateDirectory(badSourceFile);
            string destinationFile = Path.Combine(directory, Path.GetRandomFileName());

            Action a = () => fileSystem.File.Copy(badSourceFile, destinationFile);

            a.Should().Throw<UnauthorizedAccessException>().And.Message.Should().Contain("Access to the path");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CopyFileThrowsWhenDestinationDirectoryDoesNotExist(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string sourceFile = Path.Combine(directory, Path.GetRandomFileName());
            fileSystem.File.CreateEmptyFile(sourceFile);
            string destinationFile = Path.Combine(directory, Path.GetRandomFileName(), Path.GetRandomFileName());

            Action a = () => fileSystem.File.Copy(sourceFile, destinationFile);

            a.Should().Throw<DirectoryNotFoundException>()
                .And.Message.Should().Contain("Could not find a part of the path");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CopyFileThrowsWhenDestinationExists(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string sourceFile = Path.Combine(directory, Path.GetRandomFileName());
            fileSystem.File.CreateEmptyFile(sourceFile);
            string destinationFile = Path.Combine(directory, Path.GetRandomFileName());
            fileSystem.File.CreateEmptyFile(destinationFile);

            Action a = () => fileSystem.File.Copy(sourceFile, destinationFile);

            a.Should().Throw<IOException>()
                .And.Message.Should().Contain("already exists");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DeleteFile(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string file = Path.Combine(directory, Path.GetRandomFileName());
            fileSystem.File.CreateEmptyFile(file);

            fileSystem.File.Delete(file);

            fileSystem.File.Exists(file).Should().BeFalse();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DeleteFileShouldNotThrowWhenFileDoesNotExists(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string file = Path.Combine(directory, Path.GetRandomFileName());

            Action a = () => fileSystem.File.Delete(file);

            a.Should().NotThrow();
        }

        // https://github.com/dotnet/corefx/issues/32110
        // It behaves differently on Windows Vs Non Windows
        // Use Windows behavior since it is more strict
        [WindowsOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void DeleteFileShouldNotThrowWhenDirectoryDoesNotExists(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string file = Path.Combine(directory, Path.GetRandomFileName(), Path.GetRandomFileName());

            Action a = () => fileSystem.File.Delete(file);

            a.Should().Throw<DirectoryNotFoundException>().And.Message.Should().Contain("Could not find a part of the path");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EnumerateAllFilesThrowsWhenDirectoryDoesNotExists(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string nonExistDirectory = Path.Combine(directory, Path.GetRandomFileName(), Path.GetRandomFileName());

            Action a = () => fileSystem.Directory.EnumerateFiles(nonExistDirectory);

            a.Should().Throw<DirectoryNotFoundException>().And.Message.Should()
                .Contain("Could not find a part of the path");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EnumerateAllFilesThrowsWhenPathIsAFile(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string wrongFilePath = Path.Combine(directory, Path.GetRandomFileName());
            fileSystem.File.CreateEmptyFile(wrongFilePath);

            Action a = () => fileSystem.Directory.EnumerateFiles(wrongFilePath).ToArray();

            // On Windows: The parameter is incorrect
            // On Linux: Not a directory
            // But the message is not important
            a.Should().Throw<IOException>();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenEmptyEnumerateAllFiles(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string tempDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string emptyDirectory = Path.Combine(tempDirectory, Path.GetRandomFileName());
            fileSystem.Directory.CreateDirectory(emptyDirectory);

            fileSystem.Directory.EnumerateFiles(emptyDirectory).Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenFilesExistEnumerateAllFiles(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string tempDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string testDirectory = Path.Combine(tempDirectory, Path.GetRandomFileName());
            string file1 = Path.Combine(testDirectory, Path.GetRandomFileName());
            string file2 = Path.Combine(testDirectory, Path.GetRandomFileName());

            fileSystem.Directory.CreateDirectory(testDirectory);
            fileSystem.File.CreateEmptyFile(file1);
            fileSystem.File.CreateEmptyFile(file2);

            fileSystem.Directory.EnumerateFiles(testDirectory).Should().Contain(file1);
            fileSystem.Directory.EnumerateFiles(testDirectory).Should().Contain(file2);
        }


        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EnumerateFileSystemEntriesThrowsWhenDirectoryDoesNotExists(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string nonExistDirectory = Path.Combine(directory, Path.GetRandomFileName(), Path.GetRandomFileName());

            Action a = () => fileSystem.Directory.EnumerateFileSystemEntries(nonExistDirectory);

            a.Should().Throw<DirectoryNotFoundException>().And.Message.Should()
                .Contain("Could not find a part of the path");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EnumerateFileSystemEntriesThrowsWhenPathIsAFile(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string directory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string wrongFilePath = Path.Combine(directory, Path.GetRandomFileName());
            fileSystem.File.CreateEmptyFile(wrongFilePath);

            Action a = () => fileSystem.Directory.EnumerateFileSystemEntries(wrongFilePath).ToArray();

            // On Windows: The parameter is incorrect
            // On Linux: Not a directory
            // But the message is not important
            a.Should().Throw<IOException>();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenEmptyEnumerateFileSystemEntries(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string tempDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string emptyDirectory = Path.Combine(tempDirectory, Path.GetRandomFileName());
            fileSystem.Directory.CreateDirectory(emptyDirectory);

            fileSystem.Directory.EnumerateFileSystemEntries(emptyDirectory).Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenFilesExistEnumerateFileSystemEntries(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string tempDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string testDirectory = Path.Combine(tempDirectory, Path.GetRandomFileName());
            string file1 = Path.Combine(testDirectory, Path.GetRandomFileName());
            string file2 = Path.Combine(testDirectory, Path.GetRandomFileName());
            string nestedDirectoryPath = Path.Combine(testDirectory, Path.GetRandomFileName());

            fileSystem.Directory.CreateDirectory(testDirectory);
            fileSystem.File.CreateEmptyFile(file1);
            fileSystem.File.CreateEmptyFile(file2);
            fileSystem.Directory.CreateDirectory(nestedDirectoryPath);

            fileSystem.Directory.EnumerateFileSystemEntries(testDirectory).Should().Contain(file1);
            fileSystem.Directory.EnumerateFileSystemEntries(testDirectory).Should().Contain(file2);
            fileSystem.Directory.EnumerateFileSystemEntries(testDirectory).Should().Contain(nestedDirectoryPath);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        public void WhenDirectoryExistsItDeleteDirectory(bool testMockBehaviorIsInSync, bool recursive)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string tempDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string testDirectory = Path.Combine(tempDirectory, Path.GetRandomFileName());
            fileSystem.Directory.CreateDirectory(testDirectory);

            fileSystem.Directory.Delete(testDirectory, recursive);
            fileSystem.Directory.Exists(testDirectory).Should().BeFalse();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        public void WhenDirectoryDoesNotExistsDirectoryDeleteThrows(bool testMockBehaviorIsInSync, bool recursive)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string tempDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string nonExistsTestDirectory = Path.Combine(tempDirectory, Path.GetRandomFileName());

            Action action = () => fileSystem.Directory.Delete(nonExistsTestDirectory, recursive);
            action.Should().Throw<DirectoryNotFoundException>().And.Message.Should()
                .Contain("Could not find a part of the path");
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        public void WhenDirectoryPathIsAFileDirectoryDeleteThrows(bool testMockBehaviorIsInSync, bool recursive)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string tempDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string actuallyAFilePath = Path.Combine(tempDirectory, Path.GetRandomFileName());
            fileSystem.File.CreateEmptyFile(actuallyAFilePath);

            Action action = () => fileSystem.Directory.Delete(actuallyAFilePath, recursive);
            action.Should().Throw<IOException>();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenDirectoryPathHasAFileAndNonRecursiveDirectoryDeleteThrows(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string tempDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string testDirectoryPath = Path.Combine(tempDirectory, Path.GetRandomFileName());
            string testDirectoryFilePath = Path.Combine(testDirectoryPath, Path.GetRandomFileName());
            fileSystem.Directory.CreateDirectory(testDirectoryPath);
            fileSystem.File.CreateEmptyFile(testDirectoryFilePath);

            Action action = () => fileSystem.Directory.Delete(testDirectoryPath, false);
            // On Windows: The directory is not empty
            // On Linux: Directory not empty
            // But the message is not important
            action.Should().Throw<IOException>().And.Message.Should().Contain("not empty");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenDirectoryPathHasAFileAndRecursiveItDeletes(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string tempDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string testDirectoryPath = Path.Combine(tempDirectory, Path.GetRandomFileName());
            string testDirectoryFilePath = Path.Combine(testDirectoryPath, Path.GetRandomFileName());
            fileSystem.Directory.CreateDirectory(testDirectoryPath);
            fileSystem.File.CreateEmptyFile(testDirectoryFilePath);

            fileSystem.Directory.Delete(testDirectoryPath, true);
            fileSystem.Directory.Exists(testDirectoryPath).Should().BeFalse();
        }


        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenItMovesDirectory(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string tempDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string testSourceDirectoryPath = Path.Combine(tempDirectory, Path.GetRandomFileName());
            string nestedFilePath = Path.GetRandomFileName();
            string testDirectoryFilePath = Path.Combine(testSourceDirectoryPath, nestedFilePath);
            fileSystem.Directory.CreateDirectory(testSourceDirectoryPath);
            fileSystem.File.CreateEmptyFile(testDirectoryFilePath);

            string testDestinationDirectoryPath = Path.Combine(tempDirectory, Path.GetRandomFileName());

            fileSystem.Directory.Move(testSourceDirectoryPath, testDestinationDirectoryPath);
            fileSystem.Directory.Exists(testSourceDirectoryPath).Should().BeFalse();
            fileSystem.Directory.Exists(testDirectoryFilePath).Should().BeFalse();
            fileSystem.Directory.Exists(testDestinationDirectoryPath).Should().BeTrue();
            fileSystem.File.Exists(Path.Combine(testDestinationDirectoryPath, nestedFilePath)).Should().BeTrue();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenSourcePathDoesNotExistsDirectoryMoveThrows(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string tempDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string testSourceDirectoryPath = Path.Combine(tempDirectory, Path.GetRandomFileName());

            string testDestinationDirectoryPath = Path.Combine(tempDirectory, Path.GetRandomFileName());

            Action a = () => fileSystem.Directory.Move(testSourceDirectoryPath, testDestinationDirectoryPath);
            a.Should().Throw<DirectoryNotFoundException>().And.Message.Should()
                .Contain("Could not find a part of the path");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenDestinationDirectoryPathExistsDirectoryMoveThrows(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string tempDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string testSourceDirectoryPath = Path.Combine(tempDirectory, Path.GetRandomFileName());
            fileSystem.Directory.CreateDirectory(testSourceDirectoryPath);

            string testDestinationDirectoryPath = Path.Combine(tempDirectory, Path.GetRandomFileName());
            fileSystem.Directory.CreateDirectory(testDestinationDirectoryPath);

            Action a = () => fileSystem.Directory.Move(testSourceDirectoryPath, testDestinationDirectoryPath);
            a.Should().Throw<IOException>();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenDestinationDirectoryPathIsAFileDirectoryMoveThrows(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string tempDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string testSourceDirectoryPath = Path.Combine(tempDirectory, Path.GetRandomFileName());
            fileSystem.Directory.CreateDirectory(testSourceDirectoryPath);

            string testDestinationDirectoryPath = Path.Combine(tempDirectory, Path.GetRandomFileName());
            fileSystem.File.CreateEmptyFile(testDestinationDirectoryPath);

            Action a = () => fileSystem.Directory.Move(testSourceDirectoryPath, testDestinationDirectoryPath);
            a.Should().Throw<IOException>();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenSourceAndDestinationPathIsTheSameDirectoryMoveThrows(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem = SetupSubjectFileSystem(testMockBehaviorIsInSync);
            string tempDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            string testSourceDirectoryPath = Path.Combine(tempDirectory, Path.GetRandomFileName());
            fileSystem.Directory.CreateDirectory(testSourceDirectoryPath);

            Action a = () => fileSystem.Directory.Move(testSourceDirectoryPath, testSourceDirectoryPath);
            a.Should().Throw<IOException>().And.Message.Should().Contain("Source and destination path must be different");
        }

        private static IFileSystem SetupSubjectFileSystem(bool testMockBehaviorIsInSync)
        {
            IFileSystem fileSystem;
            if (testMockBehaviorIsInSync)
            {
                FileSystemMockBuilder temporaryFolder = new FileSystemMockBuilder
                {
                    TemporaryFolder = Path.GetTempPath()
                };
                fileSystem = temporaryFolder.Build();
            }
            else
            {
                fileSystem = new FileSystemWrapper();
            }

            return fileSystem;
        }
    }
}

using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace QuickBackup
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3 || args.Length > 4)
            {
                Console.WriteLine("Usage: QuickBackup archive_name exclusions_file primary_backup_folder [secondary_backup_folder]");
                return;
            }

            string archiveName = args[0];
            string exclusionsFile = args[1];
            string primaryBackupFolder = args[2];
            string? secondaryBackupFolder = args.Length == 4 ? args[3] : null;

            string currentDir = Directory.GetCurrentDirectory();
            string parentFolderName = new DirectoryInfo(currentDir).Name;
            string primaryTargetSubfolder = Path.Combine(primaryBackupFolder, parentFolderName);
            Directory.CreateDirectory(primaryTargetSubfolder);

            string archiveFileName = archiveName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? archiveName
                : archiveName + ".zip";
            string archiveFilePath = Path.Combine(primaryTargetSubfolder, archiveFileName);

            // Read exclusions
            List<Regex> exclusionRegexes = new List<Regex>();
            if (File.Exists(exclusionsFile))
            {
                foreach (var line in File.ReadAllLines(exclusionsFile))
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        exclusionRegexes.Add(new Regex(trimmed, RegexOptions.Compiled | RegexOptions.IgnoreCase));
                }
            }
            else
            {
                Console.WriteLine($"Exclusions file not found: {exclusionsFile}");
                return;
            }

            // Gather files to include
            List<string> filesToInclude = new List<string>();
            RecursivelyCollectFiles(currentDir, currentDir, filesToInclude, exclusionRegexes);

            // Create zip
            if (File.Exists(archiveFilePath))
                File.Delete(archiveFilePath);
            using (var zip = ZipFile.Open(archiveFilePath, ZipArchiveMode.Create))
            {
                foreach (var file in filesToInclude)
                {
                    string entryName = Path.GetRelativePath(currentDir, file);
                    zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                }
            }
            Console.WriteLine($"Created archive: {archiveFilePath}");

            // Copy to secondary backup if provided
            if (!string.IsNullOrEmpty(secondaryBackupFolder))
            {
                string secondaryTargetSubfolder = Path.Combine(secondaryBackupFolder, parentFolderName);
                try
                {
                    Directory.CreateDirectory(secondaryTargetSubfolder);
                    string secondaryArchivePath = Path.Combine(secondaryTargetSubfolder, archiveFileName);
                    File.Copy(archiveFilePath, secondaryArchivePath, true);
                    Console.WriteLine($"Copied archive to secondary: {secondaryArchivePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to copy to secondary backup: {ex.Message}");
                }
            }
        }

        static void RecursivelyCollectFiles(string rootDir, string currentDir, List<string> files, List<Regex> exclusions)
        {
            string folderName = new DirectoryInfo(currentDir).Name;
            if (IsExcluded(folderName, exclusions))
                return;

            foreach (var file in Directory.GetFiles(currentDir))
            {
                string fileName = Path.GetFileName(file);
                if (!IsExcluded(fileName, exclusions))
                    files.Add(file);
            }
            foreach (var dir in Directory.GetDirectories(currentDir))
            {
                RecursivelyCollectFiles(rootDir, dir, files, exclusions);
            }
        }

        static bool IsExcluded(string name, List<Regex> exclusions)
        {
            foreach (var regex in exclusions)
            {
                if (regex.IsMatch(name))
                    return true;
            }
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace FeiSharpTerminal3._1;
public class FilePkg
{
    public static void Pack(string sourceDir, string[] patterns, string outputFile, bool recursive = false)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Directory does not exist{sourceDir}");
        if (patterns == null || patterns.Length == 0)
            throw new ArgumentException("Use one file mode at least");
        var allFiles = new List<string>();
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var pattern in patterns)
        {
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                var files = Directory.GetFiles(sourceDir, pattern, searchOption);
                allFiles.AddRange(files);
            }
            else
            {
                string filePath = Path.Combine(sourceDir, pattern);
                if (File.Exists(filePath))
                    allFiles.Add(filePath);
            }
        }
        allFiles = allFiles.Distinct().ToList();
        if (allFiles.Count == 0)
            throw new InvalidOperationException("Could not find the file");
        string outputDir = Path.GetDirectoryName(outputFile);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);
        using (var fileStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
        using (var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Create))
        {
            foreach (var filePath in allFiles)
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                string relativePath = Path.GetRelativePath(sourceDir, filePath);
                var entry = zipArchive.CreateEntry(relativePath, CompressionLevel.Optimal);
                using (var entryStream = entry.Open())
                using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
                {
                    writer.Write(content);
                }
            }
        }
    }
    public static void Unpack(string packageFile, string outputDir = null, bool overwrite = true)
    {
        if (!File.Exists(packageFile))
            throw new FileNotFoundException($"Package file does not exist: {packageFile}");
        if (!packageFile.EndsWith(".fscpkg", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("File must be .fscpkg format");
        if (string.IsNullOrEmpty(outputDir))
            outputDir = string.IsNullOrEmpty(Path.GetDirectoryName(packageFile)) ? Directory.GetCurrentDirectory() : Path.GetDirectoryName(packageFile);
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);
        int fileCount = 0;
        try
        {
            using (var fileStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read))
            using (var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read))
            {
                foreach (var entry in zipArchive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;
                    string outputPath = Path.Combine(outputDir, entry.FullName);
                    string subDir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(subDir) && !Directory.Exists(subDir))
                        Directory.CreateDirectory(subDir);
                    if (File.Exists(outputPath) && !overwrite)
                    {
                        continue;
                    }
                    using (var entryStream = entry.Open())
                    using (var reader = new StreamReader(entryStream, Encoding.UTF8))
                    using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
                    {
                        writer.Write(reader.ReadToEnd());
                    }
                    fileCount++;
                }
            }
            File.Delete(packageFile);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
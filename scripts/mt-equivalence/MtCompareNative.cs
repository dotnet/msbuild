// Helper types for the MSBuild -mt build-equivalence comparison scripts.
// Compiled at runtime with Add-Type; PowerShell's own file enumeration + hashing is roughly an
// order of magnitude slower on the ~25k-file artifact trees this compares.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

public static class MtCompareNative
{
    public sealed class FileEntry
    {
        public string RelativePath;
        public string FullPath;
        public long Length;
        public string Hash;
    }

    /// <summary>
    /// Hashes every file under <paramref name="root"/> in parallel.
    /// Relative paths use forward slashes so they are stable across the two snapshots.
    /// </summary>
    public static List<FileEntry> HashTree(string root, int degreeOfParallelism)
    {
        root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        int prefix = root.Length + 1;

        var files = new List<string>(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        var results = new FileEntry[files.Count];

        var options = new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism <= 0 ? Environment.ProcessorCount : degreeOfParallelism };
        Parallel.For(0, files.Count, options, i =>
        {
            string path = files[i];
            var entry = new FileEntry
            {
                FullPath = path,
                RelativePath = path.Substring(prefix).Replace('\\', '/'),
                Length = -1,
            };
            try
            {
                var info = new FileInfo(path);
                entry.Length = info.Length;
                // FileShare.ReadWrite: some outputs may still be memory-mapped by a lingering process.
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.SequentialScan))
                using (var sha = SHA256.Create())
                {
                    entry.Hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
                }
            }
            catch (Exception ex)
            {
                entry.Hash = "ERROR:" + ex.GetType().Name;
            }
            results[i] = entry;
        });

        return new List<FileEntry>(results);
    }

    public sealed class DiffRun
    {
        public long Start;
        public long Length;
    }

    /// <summary>
    /// Returns the contiguous runs of differing bytes between two files, up to <paramref name="maxRuns"/>.
    /// Trailing bytes of the longer file are reported as a final run.
    /// </summary>
    public static List<DiffRun> DiffRuns(string leftPath, string rightPath, int maxRuns)
    {
        byte[] left = File.ReadAllBytes(leftPath);
        byte[] right = File.ReadAllBytes(rightPath);
        var runs = new List<DiffRun>();
        int min = Math.Min(left.Length, right.Length);

        int i = 0;
        while (i < min && runs.Count < maxRuns)
        {
            if (left[i] == right[i]) { i++; continue; }
            int start = i;
            while (i < min && left[i] != right[i]) { i++; }
            runs.Add(new DiffRun { Start = start, Length = i - start });
        }

        if (left.Length != right.Length && runs.Count < maxRuns)
        {
            runs.Add(new DiffRun { Start = min, Length = Math.Abs(left.Length - right.Length) });
        }

        return runs;
    }

    /// <summary>
    /// Streams a text file, applies prefix/drop/replace normalization and returns a line -> count map.
    /// </summary>
    /// <remarks>
    /// The node-id and timestamp prefix is stripped before anything else, because whether the file
    /// logger emits one depends on the replay engine rather than on the build. Leaving it in place
    /// makes every '^'-anchored drop rule silently stop matching, which turns dropped noise back into
    /// reported differences.
    /// </remarks>
    public static Dictionary<string, int> NormalizedLineCounts(
        string path,
        System.Text.RegularExpressions.Regex[] prefix,
        System.Text.RegularExpressions.Regex[] drop,
        System.Text.RegularExpressions.Regex[] replaceFrom,
        string[] replaceTo,
        System.Text.RegularExpressions.Regex[] setOnly,
        HashSet<string> setOnlySeen)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (string rawLine in File.ReadLines(path))
        {
            string raw = rawLine;
            for (int i = 0; i < prefix.Length; i++)
            {
                raw = prefix[i].Replace(raw, string.Empty);
            }

            bool skip = false;
            for (int i = 0; i < drop.Length; i++)
            {
                if (drop[i].IsMatch(raw)) { skip = true; break; }
            }
            if (skip) { continue; }

            string line = raw;
            for (int i = 0; i < replaceFrom.Length; i++)
            {
                line = replaceFrom[i].Replace(line, replaceTo[i]);
            }
            line = line.Trim();
            if (line.Length == 0) { continue; }

            bool countInsensitive = false;
            for (int i = 0; i < setOnly.Length; i++)
            {
                if (setOnly[i].IsMatch(line)) { countInsensitive = true; break; }
            }

            if (countInsensitive)
            {
                // Presence-only: the file logger re-emits a target/context header every time output
                // from different nodes interleaves, so the count is a scheduling artifact.
                setOnlySeen.Add(line);
                counts[line] = 1;
                continue;
            }

            int existing;
            counts[line] = counts.TryGetValue(line, out existing) ? existing + 1 : 1;
        }

        return counts;
    }

    /// <summary>
    /// Streams a text file and collects, for each named extractor, the set of values captured by the
    /// extractor's "v" group. Used for target/task coverage comparison, where the *set* is meaningful
    /// but the count and ordering are scheduling artifacts.
    /// </summary>
    public static Dictionary<string, HashSet<string>> ExtractSets(
        string path,
        string[] names,
        System.Text.RegularExpressions.Regex[] patterns)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        for (int i = 0; i < names.Length; i++)
        {
            result[names[i]] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0) { continue; }

            for (int i = 0; i < patterns.Length; i++)
            {
                var m = patterns[i].Match(line);
                if (m.Success)
                {
                    result[names[i]].Add(m.Groups["v"].Value);
                    break;
                }
            }
        }

        return result;
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using SystemCoreMonitor.Models;

namespace SystemCoreMonitor.Core
{
    public sealed class VirtualDiskActionResult
    {
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public long ReclaimedBytes { get; set; }
        public string ReclaimedSummary { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
    }

    /// <summary>
    /// Finds large storage consumers that a per-volume usage bar structurally cannot show:
    /// dynamically grown virtual disks, regenerable build caches, the recycle bin and
    /// system-owned files that look like junk but must never be deleted.
    ///
    /// Detection is read-only. Deletion is offered only for whitelisted regenerable caches.
    /// </summary>
    public static class BloatDetector
    {
        private const long ReportFloorBytes = 100L * 1024L * 1024L;
        private const double CritThresholdGB = 20.0;
        private const double WarnThresholdGB = 5.0;

        public const string ActionSafeDelete    = "SafeDelete";
        public const string ActionLaunchTool    = "LaunchTool";
        public const string ActionDockerPrune   = "DockerPrune";
        public const string ActionCompactVhdx   = "CompactVhdx";
        public const string ActionInformational = "Informational";

        public static List<BloatFinding> Scan(bool includeComponentStore = true)
        {
            var findings = new List<BloatFinding>();

            try { DetectVirtualDisks(findings); } catch (Exception ex) { CrashLogger.LogException("BloatDetector.VirtualDisks", ex, false); }
            try { DetectBuildCaches(findings); } catch (Exception ex) { CrashLogger.LogException("BloatDetector.BuildCaches", ex, false); }
            try { DetectRecycleBin(findings); } catch (Exception ex) { CrashLogger.LogException("BloatDetector.RecycleBin", ex, false); }
            try { DetectSystemFiles(findings); } catch (Exception ex) { CrashLogger.LogException("BloatDetector.SystemFiles", ex, false); }
            if (includeComponentStore)
            {
                try { DetectComponentStore(findings); } catch (Exception ex) { CrashLogger.LogException("BloatDetector.ComponentStore", ex, false); }
            }

            foreach (var finding in findings)
            {
                finding.ActionLabel = ResolveActionLabel(finding.ActionKind);
            }

            findings.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
            return findings;
        }

        private static string ResolveActionLabel(string actionKind)
        {
            if (string.Equals(actionKind, ActionSafeDelete, StringComparison.OrdinalIgnoreCase))
                return LocalizationManager.Get("BloatActionClean");

            if (string.Equals(actionKind, ActionDockerPrune, StringComparison.OrdinalIgnoreCase))
                return LocalizationManager.Get("BloatActionDockerPrune");

            if (string.Equals(actionKind, ActionCompactVhdx, StringComparison.OrdinalIgnoreCase))
                return LocalizationManager.Get("BloatActionCompactVhdx");

            if (string.Equals(actionKind, ActionLaunchTool, StringComparison.OrdinalIgnoreCase))
                return LocalizationManager.Get("BloatActionTool");

            return "";
        }

        #region Whitelist

        /// <summary>
        /// The only directories this tool will ever delete. Every entry is a regenerable
        /// package or build cache that its own toolchain rebuilds on the next run.
        /// </summary>
        public static List<string> GetDeletableCachePaths()
        {
            var paths = new List<string>();
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            AddIfExists(paths, profile, ".gradle", "caches");
            AddIfExists(paths, profile, ".nuget", "packages");
            AddIfExists(paths, profile, ".android", "cache");
            AddIfExists(paths, profile, ".m2", "repository");
            AddIfExists(paths, localAppData, "npm-cache");
            AddIfExists(paths, localAppData, "pip", "cache");

            return paths;
        }

        /// <summary>
        /// Exact normalized match against the whitelist. Deliberately not a prefix check:
        /// deletion always targets a whole cache root, never a path derived from user input.
        /// </summary>
        public static bool IsWhitelistedForDeletion(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            string candidate;
            try
            {
                candidate = Path.GetFullPath(path).TrimEnd('\\', '/');
            }
            catch
            {
                return false;
            }

            foreach (var allowed in GetDeletableCachePaths())
            {
                if (string.Equals(candidate, allowed.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Clears a cache directory after confirming it is whitelisted. SafeTempCleaner
        /// applies the age guard and refuses to follow reparse points.
        /// </summary>
        public static TempCleanResult DeleteWhitelistedCache(string path, int minAgeHours = 24)
        {
            if (!IsWhitelistedForDeletion(path))
            {
                var rejected = new TempCleanResult();
                rejected.SkippedReasons.Add("Path is not whitelisted for deletion");
                return rejected;
            }

            return SafeTempCleaner.CleanWhitelistedCache(path, minAgeHours);
        }

        private static void AddIfExists(List<string> target, params string[] segments)
        {
            if (segments == null || segments.Length == 0) return;
            if (string.IsNullOrEmpty(segments[0])) return;

            try
            {
                string combined = segments[0];
                for (int i = 1; i < segments.Length; i++)
                {
                    combined = Path.Combine(combined, segments[i]);
                }

                if (Directory.Exists(combined))
                {
                    target.Add(Path.GetFullPath(combined));
                }
            }
            catch { }
        }

        #endregion

        #region Detectors

        /// <summary>
        /// Dynamically expanding VHDX files (Docker Desktop's WSL2 backend, WSL distros)
        /// grow as data is written but never shrink when it is deleted, so the file size
        /// on disk says nothing about how much is actually stored inside.
        /// </summary>
        private static void DetectVirtualDisks(List<BloatFinding> findings)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData)) return;

            string dockerRoot = Path.Combine(localAppData, "Docker");
            string packagesRoot = Path.Combine(localAppData, "Packages");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var roots = new[]
            {
                (Root: dockerRoot, IsDockerRoot: true),
                (Root: packagesRoot, IsDockerRoot: false)
            };

            foreach (var entry in roots)
            {
                if (!Directory.Exists(entry.Root)) continue;

                foreach (var file in EnumerateFilesSafely(entry.Root, "*.vhdx"))
                {
                    string normalized;
                    try { normalized = Path.GetFullPath(file.FullName); }
                    catch { continue; }

                    if (!seen.Add(normalized)) continue;

                    long size = 0;
                    try { size = file.Length; }
                    catch { continue; }

                    if (size < ReportFloorBytes) continue;

                    bool isDocker = entry.IsDockerRoot ||
                        file.Name.IndexOf("docker", StringComparison.OrdinalIgnoreCase) >= 0;

                    string title;
                    string category;
                    string actionKind;
                    string hint;

                    if (isDocker)
                    {
                        string role = ResolveDockerDiskRole(file);
                        title = string.Format(LocalizationManager.Get("BloatDockerDiskTitle"), role) + " — " + file.Name;
                        category = "DockerVirtualDisk";
                        actionKind = ActionDockerPrune;
                        hint = LocalizationManager.Get("BloatDockerDiskHint");
                    }
                    else
                    {
                        string distro = ResolveWslDistroName(entry.Root, normalized);
                        title = string.Format(LocalizationManager.Get("BloatWslDiskTitle"), distro) + " — " + file.Name;
                        category = "WslVirtualDisk";
                        actionKind = ActionCompactVhdx;
                        hint = LocalizationManager.Get("BloatWslDiskHint");
                    }

                    findings.Add(new BloatFinding
                    {
                        Title = title,
                        Path = normalized,
                        SizeBytes = size,
                        SizeDisplay = MetricFormatting.FormatBytesAuto(size),
                        Severity = ClassifyBySize(size),
                        Category = category,
                        ActionKind = actionKind,
                        ActionHint = hint
                    });
                }
            }
        }

        private static string ResolveDockerDiskRole(FileInfo file)
        {
            string parent = file.Directory?.Name ?? "";
            if (string.Equals(parent, "disk", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parent, "data", StringComparison.OrdinalIgnoreCase))
                return "data";
            if (string.Equals(parent, "main", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parent, "distro", StringComparison.OrdinalIgnoreCase))
                return "distro";
            return string.IsNullOrWhiteSpace(parent) ? "WSL2" : parent;
        }

        private static string ResolveWslDistroName(string packagesRoot, string fullPath)
        {
            try
            {
                string rootWithSep = Path.GetFullPath(packagesRoot).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                if (fullPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
                {
                    string rel = fullPath.Substring(rootWithSep.Length);
                    int sepIdx = rel.IndexOfAny(new[] { '\\', '/' });
                    string pkgFolder = sepIdx > 0 ? rel.Substring(0, sepIdx) : rel;

                    // Strip Microsoft Store publisher hash suffix (e.g., CanonicalGroupLimited.Ubuntu_79rhkp1fndgsc)
                    int underscoreIdx = pkgFolder.IndexOf('_');
                    if (underscoreIdx > 0) pkgFolder = pkgFolder.Substring(0, underscoreIdx);

                    int dotIdx = pkgFolder.LastIndexOf('.');
                    if (dotIdx > 0 && dotIdx < pkgFolder.Length - 1)
                    {
                        string tail = pkgFolder.Substring(dotIdx + 1);
                        if (!string.IsNullOrWhiteSpace(tail) && tail.Length >= 3)
                            return tail;
                    }

                    if (!string.IsNullOrWhiteSpace(pkgFolder))
                        return pkgFolder;
                }
            }
            catch { }

            return "WSL";
        }

        /// <summary>
        /// Ensures a VHDX path belongs exclusively to Docker Desktop or WSL under %LOCALAPPDATA%
        /// and contains no characters that could inject commands into a diskpart script.
        /// </summary>
        public static bool IsWhitelistedVhdxForCompaction(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (path.IndexOfAny(new[] { '"', '\r', '\n', '\0' }) >= 0) return false;
            if (!path.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase)) return false;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData)) return false;

            try
            {
                string full = Path.GetFullPath(path);
                if (!File.Exists(full)) return false;

                string dockerRoot = Path.GetFullPath(Path.Combine(localAppData, "Docker")).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                string packagesRoot = Path.GetFullPath(Path.Combine(localAppData, "Packages")).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;

                return full.StartsWith(dockerRoot, StringComparison.OrdinalIgnoreCase) ||
                       full.StartsWith(packagesRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks whether the Docker Engine daemon is currently reachable via the Docker CLI.
        /// </summary>
        public static bool IsDockerDaemonRunning()
        {
            var probe = RunCliProcess("docker", "info --format \"{{.ServerVersion}}\"", 5_000);
            return probe.Started && probe.ExitCode == 0;
        }

        /// <summary>
        /// Locates Docker Desktop executable on standard Windows install paths.
        /// </summary>
        public static string? FindDockerDesktopExecutable()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Docker", "Docker", "Docker Desktop.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Docker", "Docker Desktop.exe")
            };

            foreach (var path in candidates)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        return path;
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// Starts Docker Desktop and polls until the Docker Engine daemon responds or the timeout expires.
        /// </summary>
        public static bool StartDockerDesktopAndWait(int maxWaitSeconds = 60)
        {
            string? exe = FindDockerDesktopExecutable();
            if (string.IsNullOrWhiteSpace(exe)) return false;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                CrashLogger.LogException("BloatDetector.StartDockerDesktop", ex, false);
                return false;
            }

            var deadline = DateTime.UtcNow.AddSeconds(Math.Max(10, maxWaitSeconds));
            while (DateTime.UtcNow < deadline)
            {
                System.Threading.Thread.Sleep(2000);
                if (IsDockerDaemonRunning()) return true;
            }

            return false;
        }

        /// <summary>
        /// Prefers PowerShell 7+ (pwsh.exe) when installed, falling back to Windows PowerShell 5.1 (powershell.exe).
        /// </summary>
        public static string ResolvePowerShellExecutable()
        {
            try
            {
                string? pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrWhiteSpace(pathEnv))
                {
                    foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                    {
                        try
                        {
                            string candidate = Path.Combine(dir.Trim(), "pwsh.exe");
                            if (File.Exists(candidate)) return candidate;
                        }
                        catch { }
                    }
                }

                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (!string.IsNullOrWhiteSpace(programFiles))
                {
                    foreach (var sub in new[] { "7", "7-preview" })
                    {
                        string candidate = Path.Combine(programFiles, "PowerShell", sub, "pwsh.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            catch { }

            return "powershell.exe";
        }

        /// <summary>
        /// Safely prunes stopped Docker containers, unused networks, dangling images, and build cache
        /// without touching persistent volumes.
        /// </summary>
        public static VirtualDiskActionResult RunDockerPrune()
        {
            var result = new VirtualDiskActionResult();
            try
            {
                var sysPrune = RunCliProcess("docker", "system prune -f", 120_000);
                if (!sysPrune.Started)
                {
                    result.ErrorMessage = LocalizationManager.Get("ToastDockerNotRunning");
                    return result;
                }

                if (sysPrune.ExitCode != 0)
                {
                    result.ErrorMessage = LocalizationManager.Get("ToastDockerNotRunning");
                    return result;
                }

                var builderPrune = RunCliProcess("docker", "builder prune -f", 120_000);
                string combined = (sysPrune.StdOut ?? "") + "\n" + (builderPrune.StdOut ?? "");

                // Extract "Total reclaimed space: <amount>" lines if present
                var matches = Regex.Matches(combined, @"Total reclaimed space:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
                var reclaimedParts = new List<string>();
                foreach (Match m in matches)
                {
                    string val = m.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(val) && !string.Equals(val, "0B", StringComparison.OrdinalIgnoreCase))
                    {
                        reclaimedParts.Add(val);
                    }
                }

                result.Success = true;
                result.ReclaimedSummary = reclaimedParts.Count > 0 ? string.Join(" + ", reclaimedParts) : "";
                return result;
            }
            catch (Exception ex)
            {
                CrashLogger.LogException("BloatDetector.RunDockerPrune", ex, false);
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Shuts down WSL and runs elevated diskpart compact vdisk on a whitelisted Docker/WSL VHDX
        /// to reclaim sparse free blocks back to Windows.
        /// </summary>
        public static VirtualDiskActionResult CompactVirtualDisk(string vhdxPath)
        {
            var result = new VirtualDiskActionResult();
            if (!IsWhitelistedVhdxForCompaction(vhdxPath))
            {
                result.ErrorMessage = LocalizationManager.Get("BloatCleanRejected");
                return result;
            }

            string fullPath = Path.GetFullPath(vhdxPath);
            long sizeBefore = 0;
            try { sizeBefore = new FileInfo(fullPath).Length; } catch { }

            string scriptPath = Path.Combine(Path.GetTempPath(), $"scm-compact-{Guid.NewGuid():N}.txt");
            try
            {
                string scriptContent =
                    $"select vdisk file=\"{fullPath}\"\r\n" +
                    "attach vdisk readonly\r\n" +
                    "compact vdisk\r\n" +
                    "detach vdisk\r\n" +
                    "exit\r\n";

                File.WriteAllText(scriptPath, scriptContent);

                string psExe = ResolvePowerShellExecutable();
                string psCommand = $"wsl --shutdown; Start-Sleep -Seconds 3; diskpart /s '{scriptPath.Replace("'", "''")}'";
                var psi = new ProcessStartInfo
                {
                    FileName = psExe,
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    result.ErrorMessage = "Failed to launch diskpart";
                    return result;
                }

                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    result.ErrorMessage = $"diskpart exit code {proc.ExitCode}";
                    return result;
                }

                long sizeAfter = sizeBefore;
                try { sizeAfter = new FileInfo(fullPath).Length; } catch { }

                result.Success = true;
                result.ReclaimedBytes = Math.Max(0, sizeBefore - sizeAfter);
                result.ReclaimedSummary = MetricFormatting.FormatBytesAuto(result.ReclaimedBytes);
                return result;
            }
            catch (Win32Exception wex) when (wex.NativeErrorCode == 1223)
            {
                // User declined UAC prompt
                result.Cancelled = true;
                return result;
            }
            catch (Exception ex)
            {
                CrashLogger.LogException("BloatDetector.CompactVirtualDisk", ex, false);
                result.ErrorMessage = ex.Message;
                return result;
            }
            finally
            {
                try { if (File.Exists(scriptPath)) File.Delete(scriptPath); } catch { }
            }
        }

        private static (bool Started, int ExitCode, string StdOut, string StdErr) RunCliProcess(string fileName, string arguments, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return (false, -1, "", "");

                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();

                if (!proc.WaitForExit(timeoutMs))
                {
                    try { proc.Kill(); } catch { }
                    return (true, -1, "", "Timeout");
                }

                return (true, proc.ExitCode, stdoutTask.Result, stderrTask.Result);
            }
            catch (Win32Exception)
            {
                return (false, -1, "", "");
            }
        }

        private static void DetectBuildCaches(List<BloatFinding> findings)
        {
            foreach (var path in GetDeletableCachePaths())
            {
                long size = FolderSizeScanner.MeasureTotalBytes(path);
                if (size < ReportFloorBytes) continue;

                findings.Add(new BloatFinding
                {
                    Title = LocalizationManager.Get("BloatBuildCacheTitle") + " — " + new DirectoryInfo(path).Name,
                    Path = path,
                    SizeBytes = size,
                    SizeDisplay = MetricFormatting.FormatBytesAuto(size),
                    Severity = ClassifyBySize(size),
                    Category = "BuildCache",
                    ActionKind = ActionSafeDelete,
                    ActionHint = LocalizationManager.Get("BloatBuildCacheHint")
                });
            }
        }

        private static void DetectRecycleBin(List<BloatFinding> findings)
        {
            var info = new NativeMethods.SHQUERYRBINFO();
            info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.SHQUERYRBINFO));

            int hr = NativeMethods.SHQueryRecycleBin(null, ref info);
            if (hr != 0 || info.i64Size < ReportFloorBytes) return;

            findings.Add(new BloatFinding
            {
                Title = LocalizationManager.Get("BloatRecycleBinTitle"),
                Path = "",
                SizeBytes = info.i64Size,
                SizeDisplay = MetricFormatting.FormatBytesAuto(info.i64Size),
                Severity = ClassifyBySize(info.i64Size),
                Category = "RecycleBin",
                ActionKind = ActionSafeDelete,
                ActionHint = LocalizationManager.Get("BloatRecycleBinHint")
            });
        }

        /// <summary>
        /// Paging and hibernation files are reported so users stop hunting for them:
        /// they are large, owned by Windows, and resized through system settings only.
        /// </summary>
        private static void DetectSystemFiles(List<BloatFinding> findings)
        {
            string systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            if (string.IsNullOrEmpty(systemRoot)) return;

            AddSystemFileFinding(findings, Path.Combine(systemRoot, "pagefile.sys"), "BloatPageFileTitle", "BloatPageFileHint");
            AddSystemFileFinding(findings, Path.Combine(systemRoot, "hiberfil.sys"), "BloatHiberFileTitle", "BloatHiberFileHint");
        }

        private static void AddSystemFileFinding(List<BloatFinding> findings, string path, string titleKey, string hintKey)
        {
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length < ReportFloorBytes) return;

                findings.Add(new BloatFinding
                {
                    Title = LocalizationManager.Get(titleKey),
                    Path = path,
                    SizeBytes = file.Length,
                    SizeDisplay = MetricFormatting.FormatBytesAuto(file.Length),
                    Severity = "Info",
                    Category = "SystemFile",
                    ActionKind = ActionInformational,
                    ActionHint = LocalizationManager.Get(hintKey)
                });
            }
            catch { }
        }

        /// <summary>
        /// WinSxS is never safe to delete by hand; only DISM knows which components are
        /// still referenced. Reported so its size stops looking like unexplained usage.
        /// </summary>
        private static void DetectComponentStore(List<BloatFinding> findings)
        {
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrEmpty(windir)) return;

            string winSxS = Path.Combine(windir, "WinSxS");
            if (!Directory.Exists(winSxS)) return;

            long size = FolderSizeScanner.MeasureTotalBytes(winSxS);
            if (size < ReportFloorBytes) return;

            findings.Add(new BloatFinding
            {
                Title = LocalizationManager.Get("BloatComponentStoreTitle"),
                Path = winSxS,
                SizeBytes = size,
                SizeDisplay = MetricFormatting.FormatBytesAuto(size),
                Severity = "Info",
                Category = "ComponentStore",
                ActionKind = ActionLaunchTool,
                ActionHint = LocalizationManager.Get("BloatComponentStoreHint")
            });
        }

        #endregion

        #region Helpers

        public static string ClassifyBySize(long sizeBytes)
        {
            double gb = MetricFormatting.BytesToGB(sizeBytes);
            if (gb >= CritThresholdGB) return "Crit";
            if (gb >= WarnThresholdGB) return "Warn";
            return "Info";
        }

        /// <summary>
        /// Recursive file search that tolerates protected branches. The framework's
        /// AllDirectories option aborts the whole enumeration on the first denied folder.
        /// </summary>
        private static IEnumerable<FileInfo> EnumerateFilesSafely(string root, string pattern)
        {
            var matches = new List<FileInfo>();
            var pending = new Stack<DirectoryInfo>();

            try { pending.Push(new DirectoryInfo(root)); }
            catch { return matches; }

            while (pending.Count > 0)
            {
                var current = pending.Pop();

                try
                {
                    foreach (var file in current.EnumerateFiles(pattern, SearchOption.TopDirectoryOnly))
                    {
                        matches.Add(file);
                    }
                }
                catch { }

                try
                {
                    foreach (var child in current.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                    {
                        if (!FileSystemSafety.IsReparsePoint(child)) pending.Push(child);
                    }
                }
                catch { }
            }

            return matches;
        }

        #endregion
    }
}

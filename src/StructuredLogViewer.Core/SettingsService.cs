using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer.Core;

#nullable enable

namespace StructuredLogViewer
{
    public class SettingsService
    {
        private const int MaxCount = 10;

        // TODO: protect access to these with a Mutex
        private static readonly string recentLogsFilePath = Path.Combine(GetRootPath(), "RecentLogs.txt");
        private static readonly string recentProjectsFilePath = Path.Combine(GetRootPath(), "RecentProjects.txt");
        private static readonly string recentMSBuildLocationsFilePath = Path.Combine(GetRootPath(), "RecentMSBuildLocations.txt");
        private static readonly string recentSearchesFilePath = Path.Combine(GetRootPath(), "RecentSearches.txt");
        private static readonly string customArgumentsFilePath = Path.Combine(GetRootPath(), "CustomMSBuildArguments.txt");
        private static readonly string disableUpdatesFilePath = Path.Combine(GetRootPath(), "DisableUpdates.txt");
        private static readonly string settingsFilePath = Path.Combine(GetRootPath(), "Settings.txt");
        private static readonly string tempFolder = Path.Combine(GetRootPath(), "Temp");

        private static bool _settingsRead;

        public static void AddRecentLogFile(string filePath)
        {
            AddRecentItem(filePath, recentLogsFilePath);
        }

        public static void AddRecentProject(string filePath)
        {
            AddRecentItem(filePath, recentProjectsFilePath);
        }

        public static void AddRecentMSBuildLocation(string filePath)
        {
            _cachedRecentMSBuildLocations = null;
            AddRecentItem(filePath, recentMSBuildLocationsFilePath);
        }

        private static IEnumerable<string> _cachedRecentMSBuildLocations;

        public static IEnumerable<string> GetRecentMSBuildLocations(IEnumerable<string> extraLocations = null)
        {
            extraLocations ??= Enumerable.Empty<string>();

            return _cachedRecentMSBuildLocations ??= GetRecentItems(recentMSBuildLocationsFilePath)
                .Where(File.Exists)
                .Union(extraLocations, StringComparer.OrdinalIgnoreCase)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static void AddRecentSearchText(string searchText, bool discardPrefixes = false)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return;
            }

            AddRecentItem(searchText, recentSearchesFilePath, discardPrefixes);
        }

        public static IEnumerable<string> GetRecentLogFiles()
        {
            return GetRecentItems(recentLogsFilePath);
        }

        public static IEnumerable<string> GetRecentProjects()
        {
            return GetRecentItems(recentProjectsFilePath);
        }

        public static void RemoveRecentLogFile(string filePath)
        {
            RemoveRecentItem(filePath, recentLogsFilePath);
        }

        public static void RemoveRecentProject(string filePath)
        {
            RemoveRecentItem(filePath, recentProjectsFilePath);
        }

        public static IEnumerable<string> GetRecentSearchStrings()
        {
            return GetRecentItems(recentSearchesFilePath);
        }

        private static void AddRecentItem(string item, string storageFilePath, bool discardPrefixes = false)
        {
            var list = GetRecentItems(storageFilePath).ToList();
            if (AddOrPromote(list, item, discardPrefixes))
            {
                SaveText(storageFilePath, list);
            }
        }

        private static void RemoveRecentItem(string item, string storageFilePath)
        {
            var list = GetRecentItems(storageFilePath).ToList();
            if (list.Remove(item))
            {
                SaveText(storageFilePath, list);
            }
        }

        private static IEnumerable<string> GetRecentItems(string storageFilePath)
        {
            using (SingleGlobalInstance.Acquire(Path.GetFileName(storageFilePath)))
            {
                if (!File.Exists(storageFilePath))
                {
                    return Array.Empty<string>();
                }

                var lines = File.ReadAllLines(storageFilePath);
                return lines;
            }
        }

        public static string GetRootPath()
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            path = Path.Combine(path, "Microsoft", "MSBuildStructuredLog");
            return path;
        }

        private static void SaveText(string storageFilePath, IEnumerable<string> lines)
        {
            using (SingleGlobalInstance.Acquire(Path.GetFileName(storageFilePath)))
            {
                string directoryName = Path.GetDirectoryName(storageFilePath);
                Directory.CreateDirectory(directoryName);
                File.WriteAllLines(storageFilePath, lines);
            }
        }

        private static bool AddOrPromote(List<string> list, string item, bool discardPrefixes = false)
        {
            if (list.Count > 0 && list[0] == item)
            {
                // if the first item is exact match, don't do anything
                return false;
            }

            int index = list.FindIndex(i => StringComparer.OrdinalIgnoreCase.Compare(i, item) == 0);
            if (index >= 0)
            {
                list.RemoveAt(index);
            }
            else if (discardPrefixes)
            {
                index = list.FindIndex(i => item.StartsWith(i));
                if (index >= 0)
                {
                    list.RemoveAt(index);
                }
            }

            if (list.Count >= MaxCount)
            {
                list.RemoveAt(list.Count - 1);
            }

            list.Insert(0, item);
            return true;
        }

        public static string GetMSBuildExe()
        {
            return GetRecentMSBuildLocations().FirstOrDefault();
        }

        private const string DefaultArguments = "/t:Rebuild";

        public static string GetCustomArguments(string filePath)
        {
            string[] lines;

            using (SingleGlobalInstance.Acquire(Path.GetFileName(customArgumentsFilePath)))
            {
                if (!File.Exists(customArgumentsFilePath))
                {
                    return DefaultArguments;
                }

                lines = File.ReadAllLines(customArgumentsFilePath);
            }

            if (FindArguments(lines, filePath, out string arguments, out _))
            {
                return arguments;
            }

            var mostRecentArguments = TextUtilities.ParseNameValue(lines[0]);
            return mostRecentArguments.Value;
        }

        private const int MaximumProjectsInRecentArgumentsList = 100;

        /// <summary>
        /// Just an escape hatch in case some users might want it
        /// </summary>
        public static bool DisableUpdates => File.Exists(disableUpdatesFilePath);

        private static bool FindArguments(IList<string> lines, string projectFilePath, out string existingArguments, out int index)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                int equals = line.IndexOf('=');
                if (equals == -1)
                {
                    continue;
                }

                string project = line.Substring(0, equals);
                string arguments = line.Substring(equals + 1, line.Length - equals - 1);
                if (string.Equals(projectFilePath, project, StringComparison.OrdinalIgnoreCase))
                {
                    existingArguments = arguments;
                    index = i;
                    return true;
                }
            }

            existingArguments = null;
            index = -1;
            return false;
        }

        public static void SaveCustomArguments(string projectFilePath, string newArguments)
        {
            using (SingleGlobalInstance.Acquire(Path.GetFileName(customArgumentsFilePath)))
            {
                if (!File.Exists(customArgumentsFilePath))
                {
                    if (newArguments == DefaultArguments)
                    {
                        return;
                    }
                    else
                    {
                        File.WriteAllLines(customArgumentsFilePath, new[] {projectFilePath + "=" + newArguments});
                        return;
                    }
                }

                var list = File.ReadAllLines(customArgumentsFilePath).ToList();

                if (FindArguments(list, projectFilePath, out _, out var index))
                {
                    list.RemoveAt(index);
                }

                list.Insert(0, projectFilePath + "=" + newArguments);
                if (list.Count >= MaximumProjectsInRecentArgumentsList)
                {
                    list.RemoveAt(list.Count - 1);
                }

                File.WriteAllLines(customArgumentsFilePath, list);
            }
        }

        private static bool _enableTreeViewVirtualization = true;

        public static bool EnableTreeViewVirtualization
        {
            get
            {
                EnsureSettingsRead();
                return _enableTreeViewVirtualization;
            }

            set
            {
                if (_enableTreeViewVirtualization == value)
                {
                    return;
                }

                _enableTreeViewVirtualization = value;
                SaveSettings();
            }
        }

        private static bool _parentAllTargetsUnderProject;

        public static bool ParentAllTargetsUnderProject
        {
            get
            {
                EnsureSettingsRead();
                return _parentAllTargetsUnderProject;
            }

            set
            {
                if (_parentAllTargetsUnderProject == value)
                {
                    return;
                }

                _parentAllTargetsUnderProject = value;
                Construction.ParentAllTargetsUnderProject = value;
                SaveSettings();
            }
        }

        private static void EnsureSettingsRead()
        {
            if (!_settingsRead)
            {
                ReadSettings();
                _settingsRead = true;
            }
        }

        private const string Virtualization = "Virtualization=";
        const string ParentAllTargetsUnderProjectSetting = nameof(ParentAllTargetsUnderProject) + "=";

        private static void SaveSettings()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Virtualization + _enableTreeViewVirtualization.ToString());
            sb.AppendLine(ParentAllTargetsUnderProjectSetting + _parentAllTargetsUnderProject.ToString());


            using (SingleGlobalInstance.Acquire(Path.GetFileName(settingsFilePath)))
            {
                File.WriteAllText(settingsFilePath, sb.ToString());
            }
        }

        private static void ReadSettings()
        {
            using (SingleGlobalInstance.Acquire(Path.GetFileName(settingsFilePath)))
            {
                if (!File.Exists(settingsFilePath))
                {
                    return;
                }

                var lines = File.ReadAllLines(settingsFilePath);
                foreach (var line in lines)
                {
                    if (line.StartsWith(Virtualization))
                    {
                        var value = line.Substring(Virtualization.Length);
                        if (bool.TryParse(value, out bool boolValue))
                        {
                            _enableTreeViewVirtualization = boolValue;
                        }
                    }
                    else if (line.StartsWith(ParentAllTargetsUnderProjectSetting))
                    {
                        var value = line.Substring(ParentAllTargetsUnderProjectSetting.Length);
                        if (bool.TryParse(value, out bool boolValue))
                        {
                            _parentAllTargetsUnderProject = boolValue;
                        }
                    }
                }
            }
        }

        private static bool _cleanedUpTempFiles;

        public static string WriteContentToTempFileAndGetPath(string content, string fileExtension)
        {
            var folder = tempFolder;
            var filePath = Path.Combine(folder, Utilities.GetMD5Hash(content, 16) + fileExtension);

            using (SingleGlobalInstance.Acquire(Path.GetFileName(filePath)))
            {
                if (File.Exists(filePath))
                {
                    return filePath;
                }

                Directory.CreateDirectory(folder);
                File.WriteAllText(filePath, content);
            }

            if (!_cleanedUpTempFiles)
            {
                System.Threading.Tasks.Task.Run(() => CleanupTempFiles());
            }

            return filePath;
        }

        /// <summary>
        /// Delete temp files older than one month
        /// </summary>
        private static void CleanupTempFiles()
        {
            using (SingleGlobalInstance.Acquire("StructuredLogViewerTempFileCleanup"))
            {
                if (_cleanedUpTempFiles)
                {
                    return;
                }

                _cleanedUpTempFiles = true;

                var folder = tempFolder;
                try
                {
                    foreach (var file in Directory.GetFiles(folder))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            if (fileInfo.LastWriteTimeUtc < DateTime.UtcNow - TimeSpan.FromDays(30))
                            {
                                fileInfo.Delete();
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;
using Paratext.PluginInterfaces;

namespace CustomVrsPlugin
{
    /// <summary>
    /// Paratext plugin that adds Tools > Custom Versification Editor... to Scripture windows.
    /// It starts the separate CustomVrsEditor.exe (bundled in the plugin folder) on the active
    /// project's custom.vrs (which may not exist yet), in Paratext's interface language. The plugin API gives no
    /// project folder, so the folder is found from the Paratext projects folder in the registry.
    /// </summary>
    public class CustomVrsEditorPlugin : IParatextStandalonePlugin
    {
        public const string PluginName = "Custom Versification Editor";
        public const string PublisherName = "Phil Leckrone (SIL)";
        public const string PluginFolderName = "CustomVrsEditor";
        public const string EditorExeName = "CustomVrsEditor.exe";
        public const string VrsFileName = "custom.vrs";
        public const string MenuText = "Custom Versification Editor...";

        private const string DefaultProjectsDir = @"C:\My Paratext 9 Projects";

        public string Name
        {
            get { return PluginName; }
        }

        public Version Version
        {
            get { return new Version(1, 0, 0, 0); }
        }

        public string VersionString
        {
            get { return "1.0"; }
        }

        public string Publisher
        {
            get { return PublisherName; }
        }

        public IEnumerable<PluginMenuEntry> PluginMenuEntries
        {
            get
            {
                var entry = new PluginMenuEntry(MenuText, OnMenuClicked, PluginMenuLocation.ScrTextTools);
                entry.LocalizedTextNeeded += LocalizeMenuText;
                yield return entry;
            }
        }

        /// <summary>Menu text in Paratext's interface language (same languages as the editor); null keeps English.</summary>
        internal static string LocalizeMenuText(string defaultText, string locale)
        {
            string lang = string.IsNullOrEmpty(locale) ? "" : locale.ToLowerInvariant();
            if (lang.StartsWith("es")) return "Editor de versificaci\u00f3n personalizada...";
            if (lang.StartsWith("fr")) return "\u00c9diteur de versification personnalis\u00e9e...";
            if (lang.StartsWith("pt")) return "Editor de versifica\u00e7\u00e3o personalizada...";
            if (lang.StartsWith("zh")) return "\u81ea\u5b9a\u4e49\u7248\u672c\u7f16\u6392\u7f16\u8f91\u5668...";
            return null;
        }

        public string GetDescription(string locale)
        {
            string lang = string.IsNullOrEmpty(locale) ? "en" : locale.ToLowerInvariant();
            if (lang.StartsWith("es"))
                return "Edita el archivo custom.vrs del proyecto (versificaci\u00f3n personalizada de Paratext).";
            if (lang.StartsWith("fr"))
                return "Modifie le fichier custom.vrs du projet (versification personnalis\u00e9e de Paratext).";
            if (lang.StartsWith("pt"))
                return "Edita o arquivo custom.vrs do projeto (versifica\u00e7\u00e3o personalizada do Paratext).";
            if (lang.StartsWith("zh"))
                return "\u7f16\u8f91\u9879\u76ee\u7684 custom.vrs \u6587\u4ef6 (Paratext \u81ea\u5b9a\u4e49\u7248\u672c\u7f16\u6392)\u3002";
            return "Edits the project's custom.vrs file (Paratext's custom versification).";
        }

        public IDataFileMerger GetMerger(IPluginHost host, string dataIdentifier)
        {
            return null;   // the plugin keeps no project data
        }

        private void OnMenuClicked(IPluginHost host, IParatextChildState state)
        {
            string exePath = null;
            try
            {
                IProject project = state != null ? state.Project : null;
                if (project == null || project.IsResource)
                {
                    Report(host, "Open a project in a Scripture window first, then choose " + MenuText, null);
                    return;
                }

                string projectsDir = ProjectsDirectory();
                string projectDir = ResolveProjectDir(projectsDir, project.ShortName, project.ID);
                if (projectDir == null)
                {
                    Report(host, "The folder of project " + project.ShortName + " was not found in:\n" +
                        projectsDir, null);
                    return;
                }

                exePath = ResolveExePath(Assembly.GetExecutingAssembly().Location, ParatextDirectory());
                if (!File.Exists(exePath))
                {
                    Report(host, "The Custom Versification Editor program was not found:\n" + exePath +
                        "\n\nPlease reinstall the plugin (Install-Plugin.cmd).", null);
                    return;
                }

                string args = "\"" + Path.Combine(projectDir, VrsFileName) + "\"";
                string locale = UiLocale(host);
                if (!string.IsNullOrEmpty(locale))
                    args += " --lang " + locale;

                var psi = new ProcessStartInfo(exePath, args);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                // Never let an error escape into Paratext.
                Report(host, "Could not start the Custom Versification Editor:\n" + exePath + "\n\n" + ex.Message, ex);
            }
        }

        private void Report(IPluginHost host, string message, Exception ex)
        {
            try
            {
                if (host != null)
                    host.Log(this, message + (ex != null ? "\n" + ex : ""));
            }
            catch (Exception) { }
            MessageBox.Show(message, PluginName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static string UiLocale(IPluginHost host)
        {
            try
            {
                if (host != null && host.UserSettings != null)
                {
                    string locale = host.UserSettings.UiLocale;
                    // Only a plain culture name goes on the command line.
                    if (!string.IsNullOrEmpty(locale) && locale.IndexOfAny(new[] { ' ', '"' }) < 0)
                        return locale;
                }
            }
            catch (Exception) { }
            return null;
        }

        /// <summary>Folder of the running Paratext.exe (also the base folder of the app domain).</summary>
        private static string ParatextDirectory()
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                if (!string.IsNullOrEmpty(exe))
                    return Path.GetDirectoryName(exe);
            }
            catch (Exception) { }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>
        /// The Paratext projects folder: Settings_Directory under SOFTWARE\Paratext\8 (Paratext 9
        /// still uses that key), checked in both registry views, then for the current user.
        /// </summary>
        internal static string ProjectsDirectory()
        {
            foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                string dir = ReadSettingsDirectory(RegistryHive.LocalMachine, view);
                if (dir != null) return dir;
            }
            string user = ReadSettingsDirectory(RegistryHive.CurrentUser, RegistryView.Default);
            return user ?? DefaultProjectsDir;
        }

        private static string ReadSettingsDirectory(RegistryHive hive, RegistryView view)
        {
            try
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = root.OpenSubKey(@"SOFTWARE\Paratext\8"))
                {
                    if (key == null) return null;
                    string dir = key.GetValue("Settings_Directory") as string;
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        return dir.TrimEnd('\\');
                }
            }
            catch (Exception) { }
            return null;
        }

        /// <summary>
        /// The project's folder: projectsDir\shortName when its Settings.xml Guid matches the
        /// project ID (or has no readable Guid); otherwise the subfolder whose Guid matches;
        /// if no folder's Guid matches, projectsDir\shortName when it holds a Settings.xml.
        /// </summary>
        internal static string ResolveProjectDir(string projectsDir, string shortName, string projectId)
        {
            if (string.IsNullOrEmpty(projectsDir) || !Directory.Exists(projectsDir)) return null;

            string byName = null;
            if (!string.IsNullOrEmpty(shortName))
            {
                string candidate = Path.Combine(projectsDir, shortName);
                string settings = Path.Combine(candidate, "Settings.xml");
                if (File.Exists(settings))
                {
                    byName = candidate;
                    string guid = ReadProjectGuid(settings);
                    if (guid == null || string.IsNullOrEmpty(projectId) || SameId(guid, projectId))
                        return candidate;
                }
            }

            if (string.IsNullOrEmpty(projectId)) return byName;
            try
            {
                foreach (string dir in Directory.GetDirectories(projectsDir))
                {
                    string settings = Path.Combine(dir, "Settings.xml");
                    if (File.Exists(settings) && SameId(ReadProjectGuid(settings), projectId))
                        return dir;
                }
            }
            catch (Exception) { }
            return byName;
        }

        private static bool SameId(string a, string b)
        {
            return a != null && b != null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The ScriptureText/Guid value of a Settings.xml, or null.</summary>
        internal static string ReadProjectGuid(string settingsPath)
        {
            try
            {
                var xmlSettings = new XmlReaderSettings();
                xmlSettings.DtdProcessing = DtdProcessing.Ignore;
                using (XmlReader reader = XmlReader.Create(settingsPath, xmlSettings))
                {
                    if (reader.ReadToFollowing("Guid"))
                    {
                        string guid = reader.ReadElementContentAsString();
                        return string.IsNullOrEmpty(guid) ? null : guid.Trim();
                    }
                }
            }
            catch (Exception) { }
            return null;
        }

        /// <summary>
        /// The editor exe beside the plugin assembly; if the assembly location is unknown (for
        /// example loaded from bytes), the plugin's folder under the Paratext install folder.
        /// </summary>
        internal static string ResolveExePath(string assemblyLocation, string paratextDir)
        {
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                string besideAssembly = Path.Combine(Path.GetDirectoryName(assemblyLocation), EditorExeName);
                if (File.Exists(besideAssembly))
                    return besideAssembly;
            }
            return Path.Combine(Path.Combine(Path.Combine(paratextDir, "plugins"), PluginFolderName), EditorExeName);
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace CustomVrsEditor.Services
{
    public static class VersificationService
    {
        private static readonly Dictionary<int, string> Names = new Dictionary<int, string>
        {
            { 1, "Original (org.vrs)" },
            { 2, "Septuagint (lxx.vrs)" },
            { 3, "Vulgate (vul.vrs)" },
            { 4, "English (eng.vrs)" },
            { 5, "RussianProtestant (rsc.vrs)" },
            { 6, "RussianOrthodox (rso.vrs)" }
        };

        public static string GetName(int code)
        {
            string name;
            return Names.TryGetValue(code, out name) ? name : null;
        }

        public static string TryReadProjectVersification(string projectDir)
        {
            try
            {
                if (string.IsNullOrEmpty(projectDir))
                    return null;

                string settingsPath = Path.Combine(projectDir, "Settings.xml");
                if (!File.Exists(settingsPath))
                    return null;

                XDocument doc = XDocument.Load(settingsPath);
                XElement element = doc.Root != null ? doc.Root.Element("Versification") : null;
                if (element == null)
                    return null;

                int code;
                if (!int.TryParse(element.Value.Trim(), out code))
                    return null;

                return GetName(code);
            }
            catch
            {
                return null;
            }
        }
    }
}

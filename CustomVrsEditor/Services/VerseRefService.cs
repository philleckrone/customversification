using System.Text.RegularExpressions;

namespace CustomVrsEditor.Services
{
    public static class VerseRefService
    {
        private static readonly Regex RefWithRangePattern =
            new Regex(@"^([A-Za-z0-9]{2,4})\s+(\d{1,3}):(\d{1,3})(?:-(\d{1,3}))?$", RegexOptions.Compiled);

        private static readonly Regex RefNoRangePattern =
            new Regex(@"^([A-Za-z0-9]{2,4})\s+(\d{1,3}):(\d{1,3})$", RegexOptions.Compiled);

        public static bool TryParse(string text, bool allowRange, out string book, out int chapter, out int verse, out int? verseEnd)
        {
            book = null;
            chapter = 0;
            verse = 0;
            verseEnd = null;

            if (string.IsNullOrEmpty(text))
                return false;

            string trimmed = text.Trim();
            Regex pattern = allowRange ? RefWithRangePattern : RefNoRangePattern;
            Match m = pattern.Match(trimmed);
            if (!m.Success)
                return false;

            book = m.Groups[1].Value.ToUpperInvariant();
            chapter = int.Parse(m.Groups[2].Value);
            verse = int.Parse(m.Groups[3].Value);

            if (allowRange && m.Groups[4].Success)
                verseEnd = int.Parse(m.Groups[4].Value);

            return true;
        }
    }
}

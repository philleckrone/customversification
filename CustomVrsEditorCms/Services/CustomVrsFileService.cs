using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CustomVrsEditor.Models;

namespace CustomVrsEditor.Services
{
    public static class CustomVrsFileService
    {
        public const string HeaderChapterDiffs = "# Chapters with different number of verses than Original";
        public const string HeaderMapping = "# Mapping";
        public const string HeaderMissingVerses = "# Missing verses";

        private static readonly Regex ChapterVerseTokenPattern =
            new Regex(@"^(\d{1,3}):(\d{1,3})$", RegexOptions.Compiled);

        public static void Read(string path,
            out List<string> headerLines,
            out ObservableCollection<ChapterDiffEntry> chapterDiffs,
            out ObservableCollection<MappingEntry> mappings,
            out ObservableCollection<MissingVerseEntry> missingVerses)
        {
            var headers = new List<string>();
            var diffs = new ObservableCollection<ChapterDiffEntry>();
            var maps = new ObservableCollection<MappingEntry>();
            var missing = new ObservableCollection<MissingVerseEntry>();
            var diffLookup = new Dictionary<string, ChapterDiffEntry>(StringComparer.OrdinalIgnoreCase);

            bool pastHeader = false;

            Encoding enc = DetectEncoding(path);
            using (var reader = new StreamReader(path, enc, detectEncodingFromByteOrderMarks: true))
            {
                string rawLine;
                while ((rawLine = reader.ReadLine()) != null)
                {
                    string trimmed = rawLine.Trim();

                    if (IsCanonicalHeader(trimmed))
                        continue;

                    bool disabled = false;
                    string content = trimmed;
                    if (content.StartsWith("#"))
                    {
                        disabled = true;
                        content = content.Substring(1).TrimStart();
                    }

                    bool parsed = content.Length > 0
                        && TryParseDataLine(content, disabled, diffLookup, diffs, maps, missing);

                    if (!pastHeader)
                    {
                        if (!parsed)
                        {
                            headers.Add(rawLine);
                            continue;
                        }
                        pastHeader = true;
                    }
                }
            }

            SortChapterDiffsInPlace(diffs);
            SortMappingsInPlace(maps);
            SortMissingVersesInPlace(missing);

            headerLines = headers;
            chapterDiffs = diffs;
            mappings = maps;
            missingVerses = missing;
        }

        public static void Write(string path, List<string> headerLines,
            IEnumerable<ChapterDiffEntry> chapterDiffs,
            IEnumerable<MappingEntry> mappings,
            IEnumerable<MissingVerseEntry> missingVerses)
        {
            var lines = new List<string>();

            if (headerLines != null)
                foreach (var h in headerLines)
                    lines.Add(h);

            var diffList = SortChapterDiffs(chapterDiffs.ToList());
            if (diffList.Count > 0)
            {
                lines.Add(HeaderChapterDiffs);
                foreach (var d in diffList)
                    lines.Add((d.IsDisabled ? "#" : "") + d.Book + " " + d.ChaptersText);
            }

            var mappingList = SortMappings(mappings.ToList());
            if (mappingList.Count > 0)
            {
                lines.Add(HeaderMapping);
                foreach (var m in mappingList)
                    lines.Add((m.IsDisabled ? "#" : "") + m.FromRef + " = " + m.ToRef);
            }

            var missingList = SortMissingVerses(missingVerses.ToList());
            if (missingList.Count > 0)
            {
                lines.Add(HeaderMissingVerses);
                foreach (var mv in missingList)
                    lines.Add((mv.IsDisabled ? "#" : "") + "-" + mv.VerseRef);
            }

            File.WriteAllText(path, string.Join("\r\n", lines), new UTF8Encoding(false));
        }

        private static bool TryParseDataLine(string content, bool disabled,
            Dictionary<string, ChapterDiffEntry> diffLookup,
            ObservableCollection<ChapterDiffEntry> chapterDiffs,
            ObservableCollection<MappingEntry> mappings,
            ObservableCollection<MissingVerseEntry> missingVerses)
        {
            if (content.StartsWith("-"))
            {
                string book;
                int chapter, verse;
                int? verseEnd;
                if (!VerseRefService.TryParse(content.Substring(1), false, out book, out chapter, out verse, out verseEnd))
                    return false;

                missingVerses.Add(new MissingVerseEntry
                {
                    VerseRef = book + " " + chapter + ":" + verse,
                    IsDisabled = disabled
                });
                return true;
            }

            int eqIdx = content.IndexOf('=');
            if (eqIdx >= 0)
            {
                string fromPart = content.Substring(0, eqIdx).Trim();
                string toPart = content.Substring(eqIdx + 1).Trim();

                string fromBook, toBook;
                int fromCh, fromVs, toCh, toVs;
                int? fromEnd, toEnd;
                bool fromOk = VerseRefService.TryParse(fromPart, true, out fromBook, out fromCh, out fromVs, out fromEnd);
                bool toOk = VerseRefService.TryParse(toPart, true, out toBook, out toCh, out toVs, out toEnd);
                if (!fromOk || !toOk)
                    return false;

                mappings.Add(new MappingEntry { FromRef = fromPart, ToRef = toPart, IsDisabled = disabled });
                return true;
            }

            string[] parts = content.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return false;

            string bookCode = parts[0].ToUpperInvariant();
            var validTokens = new List<ChapterVerseToken>();
            for (int i = 1; i < parts.Length; i++)
            {
                Match tm = ChapterVerseTokenPattern.Match(parts[i]);
                if (!tm.Success) continue;
                validTokens.Add(new ChapterVerseToken
                {
                    Chapter = int.Parse(tm.Groups[1].Value),
                    MaxVerse = int.Parse(tm.Groups[2].Value)
                });
            }

            if (validTokens.Count == 0)
                return false;

            ChapterDiffEntry entry;
            if (!diffLookup.TryGetValue(bookCode, out entry))
            {
                entry = new ChapterDiffEntry { Book = bookCode, IsDisabled = disabled };
                diffLookup[bookCode] = entry;
                chapterDiffs.Add(entry);
            }

            foreach (var token in validTokens)
                entry.MergeChapter(token.Chapter, token.MaxVerse);

            return true;
        }

        private static bool IsCanonicalHeader(string trimmed)
        {
            return string.Equals(trimmed, HeaderChapterDiffs, StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, HeaderMapping, StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, HeaderMissingVerses, StringComparison.OrdinalIgnoreCase);
        }

        public static List<ChapterDiffEntry> SortChapterDiffs(List<ChapterDiffEntry> entries)
        {
            return entries
                .OrderBy(d => SafeBookIndex(d.Book))
                .ThenBy(d => d.Book, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<MappingEntry> SortMappings(List<MappingEntry> entries)
        {
            return entries
                .OrderBy(m => ParseRefSortKey(m.FromRef).BookIndex)
                .ThenBy(m => ParseRefSortKey(m.FromRef).Chapter)
                .ThenBy(m => ParseRefSortKey(m.FromRef).Verse)
                .ToList();
        }

        public static List<MissingVerseEntry> SortMissingVerses(List<MissingVerseEntry> entries)
        {
            return entries
                .OrderBy(mv => ParseRefSortKey(mv.VerseRef).BookIndex)
                .ThenBy(mv => ParseRefSortKey(mv.VerseRef).Chapter)
                .ThenBy(mv => ParseRefSortKey(mv.VerseRef).Verse)
                .ToList();
        }

        private static void SortChapterDiffsInPlace(ObservableCollection<ChapterDiffEntry> diffs)
        {
            ReplaceAll(diffs, SortChapterDiffs(diffs.ToList()));
        }

        private static void SortMappingsInPlace(ObservableCollection<MappingEntry> mappings)
        {
            ReplaceAll(mappings, SortMappings(mappings.ToList()));
        }

        private static void SortMissingVersesInPlace(ObservableCollection<MissingVerseEntry> missingVerses)
        {
            ReplaceAll(missingVerses, SortMissingVerses(missingVerses.ToList()));
        }

        private static void ReplaceAll<T>(ObservableCollection<T> collection, List<T> sorted)
        {
            collection.Clear();
            foreach (var item in sorted)
                collection.Add(item);
        }

        private static int SafeBookIndex(string book)
        {
            int idx = BookOrderService.GetSortIndex(book);
            return idx >= 0 ? idx : int.MaxValue;
        }

        private struct RefSortKey
        {
            public int BookIndex;
            public int Chapter;
            public int Verse;
        }

        private static RefSortKey ParseRefSortKey(string refText)
        {
            string book;
            int chapter, verse;
            int? verseEnd;
            if (VerseRefService.TryParse(refText, true, out book, out chapter, out verse, out verseEnd))
                return new RefSortKey { BookIndex = SafeBookIndex(book), Chapter = chapter, Verse = verse };
            return new RefSortKey { BookIndex = int.MaxValue, Chapter = int.MaxValue, Verse = int.MaxValue };
        }

        private static Encoding DetectEncoding(string path)
        {
            var bom = new byte[4];
            using (var fs = File.OpenRead(path))
                fs.Read(bom, 0, 4);

            if (bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode;          // UTF-16 LE
            if (bom[0] == 0xFE && bom[1] == 0xFF) return Encoding.BigEndianUnicode; // UTF-16 BE
            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return Encoding.UTF8; // UTF-8 BOM
            return new UTF8Encoding(false); // default: UTF-8 no BOM
        }
    }
}

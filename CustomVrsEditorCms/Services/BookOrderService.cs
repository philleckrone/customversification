using System;
using System.Collections.Generic;

namespace CustomVrsEditor.Services
{
    public static class BookOrderService
    {
        public static readonly IList<string> CanonicalBooks = new List<string>
        {
            // Old Testament (39)
            "GEN","EXO","LEV","NUM","DEU","JOS","JDG","RUT","1SA","2SA","1KI","2KI",
            "1CH","2CH","EZR","NEH","EST","JOB","PSA","PRO","ECC","SNG","ISA","JER",
            "LAM","EZK","DAN","HOS","JOL","AMO","OBA","JON","MIC","NAM","HAB","ZEP",
            "HAG","ZEC","MAL",
            // New Testament (27)
            "MAT","MRK","LUK","JHN","ACT","ROM","1CO","2CO","GAL","EPH","PHP","COL",
            "1TH","2TH","1TI","2TI","TIT","PHM","HEB","JAS","1PE","2PE","1JN","2JN",
            "3JN","JUD","REV",
            // Deuterocanon (12)
            "TOB","JDT","ESG","WIS","SIR","BAR","LJE","S3Y","SUS","BEL","1MA","2MA",
            // Additional Orthodox (8)
            "3MA","4MA","1ES","2ES","MAN","PS2","ODA","PSS"
        };

        private static readonly Dictionary<string, int> IndexLookup = BuildIndexLookup();

        private static Dictionary<string, int> BuildIndexLookup()
        {
            var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < CanonicalBooks.Count; i++)
                lookup[CanonicalBooks[i]] = i;
            return lookup;
        }

        public static int GetSortIndex(string book)
        {
            if (string.IsNullOrEmpty(book)) return -1;
            int idx;
            if (IndexLookup.TryGetValue(book, out idx))
                return idx;
            return -1;
        }

        public static bool IsKnownBook(string book)
        {
            return GetSortIndex(book) >= 0;
        }

        public static readonly IComparer<string> BookComparer = new BookCodeComparer();

        private class BookCodeComparer : IComparer<string>
        {
            public int Compare(string a, string b)
            {
                int ia = GetSortIndex(a);
                int ib = GetSortIndex(b);
                if (ia >= 0 && ib >= 0) return ia.CompareTo(ib);
                if (ia >= 0) return -1;
                if (ib >= 0) return 1;
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}

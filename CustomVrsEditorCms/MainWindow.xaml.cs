using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CustomVrsEditor.Models;
using CustomVrsEditor.Services;
using Microsoft.Win32;

namespace CustomVrsEditor
{
    public partial class MainWindow : Window
    {
        public static readonly RoutedUICommand SaveAsCommand = new RoutedUICommand(
            "Save As", "SaveAs", typeof(MainWindow),
            new InputGestureCollection { new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift) });

        private ObservableCollection<ChapterDiffEntry> _chapterDiffs = new ObservableCollection<ChapterDiffEntry>();
        private ObservableCollection<MappingEntry> _mappings = new ObservableCollection<MappingEntry>();
        private ObservableCollection<MissingVerseEntry> _missingVerses = new ObservableCollection<MissingVerseEntry>();
        private List<string> _headerLines = new List<string>();
        private string _currentPath = null;
        private bool _isDirty = false;
        private string _versificationCachePath = null;
        private string _versificationCacheValue = null;

        public MainWindow()
        {
            InitializeComponent();

            ChapterDiffGrid.ItemsSource = _chapterDiffs;
            MappingGrid.ItemsSource = _mappings;
            MissingVerseGrid.ItemsSource = _missingVerses;

            _chapterDiffs.CollectionChanged += ChapterDiffs_CollectionChanged;
            _mappings.CollectionChanged += Mappings_CollectionChanged;
            _missingVerses.CollectionChanged += MissingVerses_CollectionChanged;

            ChapterDiffBookCombo.ItemsSource = BookOrderService.CanonicalBooks;
            FromBookCombo.ItemsSource = BookOrderService.CanonicalBooks;
            ToBookCombo.ItemsSource = BookOrderService.CanonicalBooks;
            MissingBookCombo.ItemsSource = BookOrderService.CanonicalBooks;

            PopulateLanguageCombo();
            UpdateLocalizedColumnHeaders();
            UpdateStatus();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string GetStr(string key)
        {
            object val = Application.Current.Resources[key];
            return val as string ?? key;
        }

        // ── Language ────────────────────────────────────────────────────────────

        private void PopulateLanguageCombo()
        {
            var languages = new List<LanguageOption>
            {
                new LanguageOption { DisplayName = "English",      Culture = "en" },
                new LanguageOption { DisplayName = "Español",      Culture = "es" },
                new LanguageOption { DisplayName = "Français",     Culture = "fr" },
                new LanguageOption { DisplayName = "Português",    Culture = "pt" },
                new LanguageOption { DisplayName = "中文 (简体)",  Culture = "zh-Hans" }
            };
            LanguageCombo.ItemsSource = languages;
            LanguageCombo.DisplayMemberPath = "DisplayName";

            LanguageCombo.SelectionChanged -= LanguageCombo_SelectionChanged;
            LanguageCombo.SelectedIndex = 0;
            LanguageCombo.SelectionChanged += LanguageCombo_SelectionChanged;
        }

        private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LanguageOption opt = LanguageCombo.SelectedItem as LanguageOption;
            if (opt == null) return;
            App.SwitchLanguage(opt.Culture);
            UpdateLocalizedColumnHeaders();
            UpdateStatus();
        }

        private void UpdateLocalizedColumnHeaders()
        {
            if (ChapterDiffGrid.Columns.Count >= 3)
            {
                ChapterDiffGrid.Columns[0].Header = GetStr("Column_Book");
                ChapterDiffGrid.Columns[1].Header = GetStr("Column_Chapters");
                ChapterDiffGrid.Columns[2].Header = GetStr("Column_Disabled");
            }
            if (MappingGrid.Columns.Count >= 3)
            {
                MappingGrid.Columns[0].Header = GetStr("Column_FromRef");
                MappingGrid.Columns[1].Header = GetStr("Column_ToRef");
                MappingGrid.Columns[2].Header = GetStr("Column_Disabled");
            }
            if (MissingVerseGrid.Columns.Count >= 2)
            {
                MissingVerseGrid.Columns[0].Header = GetStr("Column_VerseRef");
                MissingVerseGrid.Columns[1].Header = GetStr("Column_Disabled");
            }
        }

        // ── Dirty tracking ─────────────────────────────────────────────────────

        private void ChapterDiffs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ChapterDiffEntry item in e.NewItems)
                    item.PropertyChanged += Item_PropertyChanged;
            if (e.OldItems != null)
                foreach (ChapterDiffEntry item in e.OldItems)
                    item.PropertyChanged -= Item_PropertyChanged;
            MarkDirty();
        }

        private void Mappings_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (MappingEntry item in e.NewItems)
                    item.PropertyChanged += Item_PropertyChanged;
            if (e.OldItems != null)
                foreach (MappingEntry item in e.OldItems)
                    item.PropertyChanged -= Item_PropertyChanged;
            MarkDirty();
        }

        private void MissingVerses_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (MissingVerseEntry item in e.NewItems)
                    item.PropertyChanged += Item_PropertyChanged;
            if (e.OldItems != null)
                foreach (MissingVerseEntry item in e.OldItems)
                    item.PropertyChanged -= Item_PropertyChanged;
            MarkDirty();
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            MarkDirty();
        }

        private void MarkDirty()
        {
            _isDirty = true;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            string appTitle = GetStr("App_Title");
            string dirty = _isDirty ? "* " : "";
            string file = _currentPath != null ? _currentPath : GetStr("Status_Unsaved");
            string summary = string.Format(GetStr("Status_SummaryFormat"),
                _chapterDiffs.Count, _mappings.Count, _missingVerses.Count);
            StatusText.Text = dirty + file + "  —  " + summary;
            Title = _currentPath != null
                ? dirty + appTitle + " — " + Path.GetFileName(_currentPath)
                : dirty + appTitle;

            ProjectHeaderText.Text = _currentPath != null
                ? BuildProjectHeaderText(_currentPath)
                : GetStr("Status_Unsaved");
        }

        private string BuildProjectHeaderText(string currentPath)
        {
            string projectName = Path.GetFileName(Path.GetDirectoryName(currentPath));
            string versification = GetCachedVersification(currentPath);

            return versification != null
                ? string.Format(GetStr("ProjectBar_FormatWithVersification"), projectName, versification)
                : string.Format(GetStr("ProjectBar_Format"), projectName);
        }

        private string GetCachedVersification(string currentPath)
        {
            if (_versificationCachePath != currentPath)
            {
                _versificationCachePath = currentPath;
                _versificationCacheValue = VersificationService.TryReadProjectVersification(Path.GetDirectoryName(currentPath));
            }
            return _versificationCacheValue;
        }

        // ── File operations ────────────────────────────────────────────────────

        private void ClearAll()
        {
            ReplaceChapterDiffs(new List<ChapterDiffEntry>());
            ReplaceMappings(new List<MappingEntry>());
            ReplaceMissingVerses(new List<MissingVerseEntry>());
            _headerLines.Clear();
        }

        protected void LoadFile(string path)
        {
            List<string> headers;
            ObservableCollection<ChapterDiffEntry> diffs;
            ObservableCollection<MappingEntry> maps;
            ObservableCollection<MissingVerseEntry> missing;
            CustomVrsFileService.Read(path, out headers, out diffs, out maps, out missing);

            ReplaceChapterDiffs(diffs);
            ReplaceMappings(maps);
            ReplaceMissingVerses(missing);
            _headerLines = headers;

            _currentPath = path;
            _isDirty = false;
            UpdateStatus();
        }

        private void ReplaceChapterDiffs(IEnumerable<ChapterDiffEntry> newItems)
        {
            _chapterDiffs.CollectionChanged -= ChapterDiffs_CollectionChanged;
            foreach (var item in _chapterDiffs) item.PropertyChanged -= Item_PropertyChanged;
            _chapterDiffs.Clear();
            foreach (var item in newItems)
            {
                item.PropertyChanged += Item_PropertyChanged;
                _chapterDiffs.Add(item);
            }
            _chapterDiffs.CollectionChanged += ChapterDiffs_CollectionChanged;
        }

        private void ReplaceMappings(IEnumerable<MappingEntry> newItems)
        {
            _mappings.CollectionChanged -= Mappings_CollectionChanged;
            foreach (var item in _mappings) item.PropertyChanged -= Item_PropertyChanged;
            _mappings.Clear();
            foreach (var item in newItems)
            {
                item.PropertyChanged += Item_PropertyChanged;
                _mappings.Add(item);
            }
            _mappings.CollectionChanged += Mappings_CollectionChanged;
        }

        private void ReplaceMissingVerses(IEnumerable<MissingVerseEntry> newItems)
        {
            _missingVerses.CollectionChanged -= MissingVerses_CollectionChanged;
            foreach (var item in _missingVerses) item.PropertyChanged -= Item_PropertyChanged;
            _missingVerses.Clear();
            foreach (var item in newItems)
            {
                item.PropertyChanged += Item_PropertyChanged;
                _missingVerses.Add(item);
            }
            _missingVerses.CollectionChanged += MissingVerses_CollectionChanged;
        }

        private bool SaveToPath(string path)
        {
            try
            {
                CustomVrsFileService.Write(path, _headerLines, _chapterDiffs, _mappings, _missingVerses);
                _currentPath = path;
                _isDirty = false;
                UpdateStatus();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(GetStr("Dlg_SaveErrorBody") + "\n" + ex.Message,
                    GetStr("Dlg_SaveErrorTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool PromptSaveIfDirty()
        {
            if (!_isDirty) return true;
            var result = MessageBox.Show(
                GetStr("Dlg_UnsavedBody"),
                GetStr("Dlg_UnsavedTitle"),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes) return DoSave();
            if (result == MessageBoxResult.No) return true;
            return false;
        }

        private bool DoSave()
        {
            if (_currentPath != null) return SaveToPath(_currentPath);
            return DoSaveAs();
        }

        private bool DoSaveAs()
        {
            var dlg = new SaveFileDialog
            {
                Title = GetStr("Dlg_SaveTitle"),
                Filter = "Versification files (*.vrs)|*.vrs|All files (*.*)|*.*",
                DefaultExt = "vrs",
                FileName = _currentPath != null ? Path.GetFileName(_currentPath) : "custom.vrs"
            };
            if (_currentPath != null)
                dlg.InitialDirectory = Path.GetDirectoryName(_currentPath);

            if (dlg.ShowDialog() == true)
                return SaveToPath(dlg.FileName);
            return false;
        }

        // ── Command handlers ───────────────────────────────────────────────────

        private void Always_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void New_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!PromptSaveIfDirty()) return;
            ClearAll();
            _currentPath = null;
            _isDirty = false;
            UpdateStatus();
        }

        private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!PromptSaveIfDirty()) return;
            var dlg = new OpenFileDialog
            {
                Title = GetStr("Dlg_OpenTitle"),
                Filter = "Versification files (*.vrs)|*.vrs|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
                LoadFile(dlg.FileName);
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            DoSave();
        }

        private void SaveAs_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            DoSaveAs();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!PromptSaveIfDirty()) e.Cancel = true;
        }

        // ── Chapter/Verse Differences tab ──────────────────────────────────────

        private void AcceptChapterDiff_Click(object sender, RoutedEventArgs e)
        {
            CommitChapterDiff();
        }

        private void ChapterDiffInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                CommitChapterDiff();
        }

        private void CommitChapterDiff()
        {
            string book = ChapterDiffBookCombo.Text.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(book))
            {
                ChapterDiffBookCombo.Focus();
                return;
            }

            int chapter, maxVerse;
            if (!int.TryParse(ChapterBox.Text.Trim(), out chapter) || chapter <= 0)
            {
                ChapterBox.Focus();
                return;
            }
            if (!int.TryParse(MaxVerseBox.Text.Trim(), out maxVerse) || maxVerse <= 0)
            {
                MaxVerseBox.Focus();
                return;
            }

            ChapterDiffEntry entry = _chapterDiffs.FirstOrDefault(
                d => string.Equals(d.Book, book, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                entry = new ChapterDiffEntry { Book = book };
                _chapterDiffs.Add(entry);
            }
            entry.MergeChapter(chapter, maxVerse);
            ResortChapterDiffs();

            ChapterDiffBookCombo.Text = string.Empty;
            ChapterBox.Clear();
            MaxVerseBox.Clear();
            ChapterDiffBookCombo.Focus();
        }

        private void ResortChapterDiffs()
        {
            var sorted = CustomVrsFileService.SortChapterDiffs(_chapterDiffs.ToList());
            _chapterDiffs.CollectionChanged -= ChapterDiffs_CollectionChanged;
            _chapterDiffs.Clear();
            foreach (var item in sorted) _chapterDiffs.Add(item);
            _chapterDiffs.CollectionChanged += ChapterDiffs_CollectionChanged;
        }

        private void DeleteChapterDiff_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedChapterDiff();
        }

        private void DeleteSelectedChapterDiff()
        {
            var entry = ChapterDiffGrid.SelectedItem as ChapterDiffEntry;
            if (entry != null) _chapterDiffs.Remove(entry);
        }

        private void ChapterDiffGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DeleteSelectedChapterDiff();
                e.Handled = true;
            }
        }

        // ── Mapping tab ─────────────────────────────────────────────────────────

        private void MappingInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                CommitMapping();
        }

        private void AcceptMapping_Click(object sender, RoutedEventArgs e)
        {
            CommitMapping();
        }

        private void CommitMapping()
        {
            string fromRef, toRef;
            if (!TryBuildRef(FromBookCombo, FromChapterBox, FromVerseBox, FromVerseEndBox, out fromRef))
                return;
            if (!TryBuildRef(ToBookCombo, ToChapterBox, ToVerseBox, ToVerseEndBox, out toRef))
                return;

            _mappings.Add(new MappingEntry { FromRef = fromRef, ToRef = toRef });
            ResortMappings();

            FromBookCombo.Text = string.Empty;
            FromChapterBox.Clear();
            FromVerseBox.Clear();
            FromVerseEndBox.Clear();
            ToBookCombo.Text = string.Empty;
            ToChapterBox.Clear();
            ToVerseBox.Clear();
            ToVerseEndBox.Clear();
            FromBookCombo.Focus();
        }

        private void ResortMappings()
        {
            var sorted = CustomVrsFileService.SortMappings(_mappings.ToList());
            _mappings.CollectionChanged -= Mappings_CollectionChanged;
            _mappings.Clear();
            foreach (var item in sorted) _mappings.Add(item);
            _mappings.CollectionChanged += Mappings_CollectionChanged;
        }

        private void DeleteMapping_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedMapping();
        }

        private void DeleteSelectedMapping()
        {
            var entry = MappingGrid.SelectedItem as MappingEntry;
            if (entry != null) _mappings.Remove(entry);
        }

        private void MappingGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DeleteSelectedMapping();
                e.Handled = true;
            }
        }

        // ── Missing Verses tab ──────────────────────────────────────────────────

        private void MissingVerseInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                CommitMissingVerse();
        }

        private void AcceptMissingVerse_Click(object sender, RoutedEventArgs e)
        {
            CommitMissingVerse();
        }

        private void CommitMissingVerse()
        {
            string book;
            int chapter, verse;
            int? verseEnd;
            if (!TryReadRefParts(MissingBookCombo, MissingChapterBox, MissingVerseBox, MissingVerseEndBox,
                                 out book, out chapter, out verse, out verseEnd))
                return;

            // A range is only an input shortcut: custom.vrs lists each missing verse separately.
            int lastVerse = verseEnd.HasValue ? verseEnd.Value : verse;
            for (int v = verse; v <= lastVerse; v++)
            {
                string verseRef = book + " " + chapter + ":" + v;
                bool alreadyListed = _missingVerses.Any(
                    m => string.Equals(m.VerseRef.Trim(), verseRef, StringComparison.OrdinalIgnoreCase));
                if (!alreadyListed)
                    _missingVerses.Add(new MissingVerseEntry { VerseRef = verseRef });
            }
            ResortMissingVerses();

            MissingBookCombo.Text = string.Empty;
            MissingChapterBox.Clear();
            MissingVerseBox.Clear();
            MissingVerseEndBox.Clear();
            MissingBookCombo.Focus();
        }

        private void ResortMissingVerses()
        {
            var sorted = CustomVrsFileService.SortMissingVerses(_missingVerses.ToList());
            _missingVerses.CollectionChanged -= MissingVerses_CollectionChanged;
            _missingVerses.Clear();
            foreach (var item in sorted) _missingVerses.Add(item);
            _missingVerses.CollectionChanged += MissingVerses_CollectionChanged;
        }

        private void DeleteMissingVerse_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedMissingVerse();
        }

        private void DeleteSelectedMissingVerse()
        {
            var entry = MissingVerseGrid.SelectedItem as MissingVerseEntry;
            if (entry != null) _missingVerses.Remove(entry);
        }

        private void MissingVerseGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DeleteSelectedMissingVerse();
                e.Handled = true;
            }
        }

        // ── Shared Book/Chapter/Verse entry validation ──────────────────────────

        private static bool TryBuildRef(ComboBox bookCombo, TextBox chapterBox, TextBox verseBox, TextBox verseEndBox, out string refText)
        {
            refText = null;

            string book;
            int chapter, verse;
            int? verseEnd;
            if (!TryReadRefParts(bookCombo, chapterBox, verseBox, verseEndBox, out book, out chapter, out verse, out verseEnd))
                return false;

            refText = book + " " + chapter + ":" + verse + (verseEnd.HasValue ? "-" + verseEnd : "");
            return true;
        }

        private static bool TryReadRefParts(ComboBox bookCombo, TextBox chapterBox, TextBox verseBox, TextBox verseEndBox,
            out string book, out int chapter, out int verse, out int? verseEnd)
        {
            chapter = 0;
            verse = 0;
            verseEnd = null;

            book = bookCombo.Text.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(book))
            {
                bookCombo.Focus();
                return false;
            }

            if (!int.TryParse(chapterBox.Text.Trim(), out chapter) || chapter <= 0)
            {
                chapterBox.Focus();
                return false;
            }

            if (!int.TryParse(verseBox.Text.Trim(), out verse) || verse <= 0)
            {
                verseBox.Focus();
                return false;
            }

            if (verseEndBox != null && !string.IsNullOrEmpty(verseEndBox.Text.Trim()))
            {
                int parsedEnd;
                if (!int.TryParse(verseEndBox.Text.Trim(), out parsedEnd) || parsedEnd <= verse)
                {
                    verseEndBox.Focus();
                    return false;
                }
                verseEnd = parsedEnd;
            }

            return true;
        }

        // ── Cms launch entry point ─────────────────────────────────────────────

        public void OpenOrCreateFile(string path)
        {
            if (File.Exists(path))
                LoadFile(path);
            else
                NewFileAt(path);
        }

        private void NewFileAt(string path)
        {
            ClearAll();
            _currentPath = path;
            _isDirty = false;
            UpdateStatus();
        }
    }

    public class LanguageOption
    {
        public string DisplayName { get; set; }
        public string Culture     { get; set; }
    }
}

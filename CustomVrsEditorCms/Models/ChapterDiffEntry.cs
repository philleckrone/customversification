using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace CustomVrsEditor.Models
{
    public class ChapterDiffEntry : INotifyPropertyChanged
    {
        private string _book = string.Empty;
        private bool _isDisabled = false;
        private readonly List<ChapterVerseToken> _tokens = new List<ChapterVerseToken>();

        public string Book
        {
            get { return _book; }
            set
            {
                _book = value ?? string.Empty;
                OnPropertyChanged("Book");
            }
        }

        public bool IsDisabled
        {
            get { return _isDisabled; }
            set
            {
                _isDisabled = value;
                OnPropertyChanged("IsDisabled");
            }
        }

        public string ChaptersText
        {
            get
            {
                var sb = new StringBuilder();
                for (int i = 0; i < _tokens.Count; i++)
                {
                    if (i > 0) sb.Append(' ');
                    sb.Append(_tokens[i].Chapter).Append(':').Append(_tokens[i].MaxVerse);
                }
                return sb.ToString();
            }
        }

        public IEnumerable<ChapterVerseToken> Tokens
        {
            get { return _tokens; }
        }

        public void MergeChapter(int chapter, int maxVerse)
        {
            for (int i = 0; i < _tokens.Count; i++)
            {
                if (_tokens[i].Chapter == chapter)
                {
                    _tokens[i].MaxVerse = maxVerse;
                    OnPropertyChanged("ChaptersText");
                    return;
                }
                if (_tokens[i].Chapter > chapter)
                {
                    _tokens.Insert(i, new ChapterVerseToken { Chapter = chapter, MaxVerse = maxVerse });
                    OnPropertyChanged("ChaptersText");
                    return;
                }
            }
            _tokens.Add(new ChapterVerseToken { Chapter = chapter, MaxVerse = maxVerse });
            OnPropertyChanged("ChaptersText");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}

using System.ComponentModel;

namespace CustomVrsEditor.Models
{
    public class MissingVerseEntry : INotifyPropertyChanged
    {
        private string _verseRef = string.Empty;
        private bool _isDisabled = false;

        public string VerseRef
        {
            get { return _verseRef; }
            set
            {
                _verseRef = value ?? string.Empty;
                OnPropertyChanged("VerseRef");
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}

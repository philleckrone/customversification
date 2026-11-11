using System.ComponentModel;

namespace CustomVrsEditor.Models
{
    public class MappingEntry : INotifyPropertyChanged
    {
        private string _fromRef = string.Empty;
        private string _toRef = string.Empty;
        private bool _isDisabled = false;

        public string FromRef
        {
            get { return _fromRef; }
            set
            {
                _fromRef = value ?? string.Empty;
                OnPropertyChanged("FromRef");
            }
        }

        public string ToRef
        {
            get { return _toRef; }
            set
            {
                _toRef = value ?? string.Empty;
                OnPropertyChanged("ToRef");
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

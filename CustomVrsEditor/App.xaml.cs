using System;
using System.Windows;

namespace CustomVrsEditor
{
    public partial class App : Application
    {
        /// <summary>
        /// Command line: [custom.vrs] [--lang culture]. The Paratext plugin passes the
        /// project's custom.vrs (which often does not exist yet) and Paratext's interface language.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string filePath = null;
            string lang = null;
            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = e.Args[i];
                if (string.Compare(arg, "--lang", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (i + 1 < e.Args.Length)
                        lang = e.Args[++i];
                }
                else if (filePath == null && arg.Length > 0)
                {
                    filePath = arg;
                }
            }

            var window = new MainWindow();
            MainWindow = window;
            window.Show();

            if (filePath != null || lang != null)
            {
                string capturedPath = filePath;
                string capturedLang = lang;
                window.Dispatcher.BeginInvoke(new Action(delegate()
                {
                    if (capturedLang != null)
                        window.SetLanguage(capturedLang);
                    if (capturedPath != null)
                        window.OpenOrCreateFile(capturedPath);
                }));
            }
        }

        public static void SwitchLanguage(string culture)
        {
            var dicts = Current.Resources.MergedDictionaries;
            ResourceDictionary toRemove = null;
            foreach (ResourceDictionary d in dicts)
            {
                if (d.Source != null && d.Source.OriginalString.Contains("Strings."))
                {
                    toRemove = d;
                    break;
                }
            }
            if (toRemove != null)
                dicts.Remove(toRemove);

            var newDict = new ResourceDictionary();
            newDict.Source = new Uri("Resources/Strings." + culture + ".xaml", UriKind.Relative);
            dicts.Add(newDict);
        }
    }
}

using System.Windows;
using Wpf.Ui.Controls;
using Wpf.Ui.Appearance;

namespace LiveCaptionsTranslator
{
    public partial class BatchTranslationWindow : FluentWindow
    {
        public BatchTranslationWindow(string apiName, string targetLanguage, List<BatchTranslationRow> items)
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();

            Loaded += (s, e) =>
            {
                SystemThemeWatcher.Watch(
                    this,
                    WindowBackdropType.Mica,
                    true
                );
            };

            ApiNameTextBlock.Text = apiName;
            TargetLanguageTextBlock.Text = targetLanguage;
            SentencesListView.ItemsSource = items;
        }
    }

    public class BatchTranslationRow
    {
        public string SourceText { get; set; } = string.Empty;
        public string TranslatedText { get; set; } = string.Empty;
    }
}

using System.Windows;
using System.Collections.Generic;
using Wpf.Ui.Controls;
using Wpf.Ui.Appearance;
using LiveCaptionsTranslator.models; // ▼ 追加: 各API設定クラスへのアクセス用

namespace LiveCaptionsTranslator
{
    public partial class BatchTranslationWindow : FluentWindow
    {
        // ▼ 修正: コンストラクタ引数に `string modelName = ""` を追加
        public BatchTranslationWindow(string apiName, string targetLanguage, List<BatchTranslationRow> items, string modelName = "")
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

            // ▼ 追加: モデル名が渡されなかった場合は、現在のAPI設定から取得を試みる
            if (string.IsNullOrEmpty(modelName))
            {
                if (apis.TranslateAPI.GetBatchLLMConfig(apiName, out var config))
                {
                    if (config is OpenAIConfig openai) modelName = openai.ModelName;
                    else if (config is OpenRouterConfig openRouter) modelName = openRouter.ModelName;
                    else if (config is LMStudioConfig lmStudio) modelName = lmStudio.ModelName;
                    else if (config is OllamaConfig ollama) modelName = ollama.ModelName;
                }

                // Google翻訳などモデル名の設定がないAPI用
                if (string.IsNullOrEmpty(modelName))
                {
                    modelName = "N/A";
                }
            }

            ModelNameTextBlock.Text = modelName;
            // ▲ 追加ここまで ▲

            TargetLanguageTextBlock.Text = targetLanguage;
            SentencesListView.ItemsSource = items;
        }
        private void CopyRequestButton_Click(object sender, RoutedEventArgs e)
        {
            string reqLog = apis.TranslateAPI.GetFormattedRequestLog();
            if (string.IsNullOrEmpty(reqLog))
            {
                System.Windows.MessageBox.Show("コピーするリクエストデータがありません。", "情報", System.Windows.MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                System.Windows.Clipboard.SetText(reqLog);
                System.Windows.MessageBox.Show("リクエスト内容をクリップボードにコピーしました。", "コピー完了", System.Windows.MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                System.Windows.MessageBox.Show("クリップボードへのアクセスに失敗しました。", "エラー", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyResponseButton_Click(object sender, RoutedEventArgs e)
        {
            string resLog = apis.TranslateAPI.GetFormattedResponseLog();
            if (string.IsNullOrEmpty(resLog))
            {
                System.Windows.MessageBox.Show("コピーするレスポンスデータがありません。", "情報", System.Windows.MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                System.Windows.Clipboard.SetText(resLog);
                System.Windows.MessageBox.Show("レスポンス内容をクリップボードにコピーしました。", "コピー完了", System.Windows.MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                System.Windows.MessageBox.Show("クリップボードへのアクセスに失敗しました。", "エラー", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class BatchTranslationRow
    {
        public string SourceText { get; set; } = string.Empty;
        public string TranslatedText { get; set; } = string.Empty;
    }
}
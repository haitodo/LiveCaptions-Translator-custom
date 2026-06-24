using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices; // 追加：Win32 API呼び出しのために必要
using System.Text.Json;
using System.Text.Json.Serialization; // 追加：JsonPropertyNameやデシリアライズ関連
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop; // 追加：WindowInteropHelperのために必要
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using LiveCaptionsTranslator.apis;
using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.utils;
using LiveCaptionsTranslator.Utils;
using Button = Wpf.Ui.Controls.Button;

namespace LiveCaptionsTranslator
{
    public partial class MainWindow : Window
    {
        public bool IsAutoHeight { get; set; } = true;
        private static AppSettingWindow? _appSettingWindow;

        #region Win32 API Definitions (Aero Snapの無効化用)

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_STYLE = -16;
        private const int WS_MAXIMIZEBOX = 0x00010000;

        #endregion

        public MainWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();

            Loaded += (s, e) =>
            {
                SystemThemeWatcher.Watch(this, WindowBackdropType.None, false);
                this.Background = System.Windows.Media.Brushes.Transparent;
                RootNavigation.Navigate(new CaptionPage());
                CheckForFirstUse();
                CheckForUpdates();
                UpdateLiveCaptionsButtonState();
                UpdateLogOnlyButtonState();
            };

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            var windowState = WindowHandler.LoadState(this, Translator.Setting);
            var defaultBounds = new Rect((screenWidth - 775) / 2, screenHeight * 3 / 4 - 167, 775, 167);
            var validatedState = WindowHandler.ValidateAndAdjustBounds(windowState, defaultBounds);
            WindowHandler.RestoreState(this, validatedState);

            ToggleTopmost(Translator.Setting.MainWindow.Topmost);
            ShowLogCard(Translator.Setting.MainWindow.CaptionLogEnabled);
            UpdateShowOriginalButton(Translator.Setting.MainWindow.ShowOriginalCaption);
            UpdateAutoScrollButton(Translator.Setting.MainWindow.AutoScrollEnabled);
            UpdateTogglePreviewButtonState(Translator.Setting.MainWindow.HidePreviewEnabled);

            // 設定値の変更を監視し、UI表示を常にモデルと同期させる
            if (Translator.Setting != null && Translator.Setting.MainWindow != null)
            {
                Translator.Setting.MainWindow.PropertyChanged += MainWindowSetting_PropertyChanged;
            }

            PreviewMouseWheel += MainWindow_PreviewMouseWheel;

            Closed += (s, e) =>
            {
                PreviewMouseWheel -= MainWindow_PreviewMouseWheel;
                if (Translator.Setting != null && Translator.Setting.MainWindow != null)
                {
                    Translator.Setting.MainWindow.PropertyChanged -= MainWindowSetting_PropertyChanged;
                }
            };
        }

        /// <summary>
        /// ウィンドウのネイティブハンドル初期化時に、Aero Snap（自動最大化機能）をスタイルから除外します。
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                int style = GetWindowLong(hwnd, GWL_STYLE);

                // 最大化ボックス（Aero Snap動作）のスタイルビットを取り除く
                SetWindowLong(hwnd, GWL_STYLE, style & ~WS_MAXIMIZEBOX);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to disable Aero Snap: {ex.Message}");
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Translator.ClearAllCaptions();
        }

        private void TopmostButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleTopmost(!this.Topmost);
        }

        private void LogOnlyButton_Click(object sender, RoutedEventArgs e)
        {
            Translator.LogOnlyFlag = !Translator.LogOnlyFlag;
            if (Translator.LogOnlyFlag)
            {
                Translator.ClearPendingQueues();
            }
            UpdateLogOnlyButtonState();
        }

        public void UpdateLogOnlyButtonState()
        {
            if (LogOnlyButton == null) return;

            if (LogOnlyButton.Icon is SymbolIcon icon)
            {
                if (Translator.LogOnlyFlag)
                {
                    icon.Symbol = SymbolRegular.Play16;
                    icon.Filled = true;
                    LogOnlyButton.Appearance = ControlAppearance.Primary;
                    LogOnlyButton.ToolTip = Application.Current.TryFindResource("ToolTipResumeTranslation") as string ?? "翻訳を再開";

                    string baseTitle = Application.Current.TryFindResource("MainWindowTitle") as string ?? "LiveCaptions Translator";
                    string pausedText = Application.Current.TryFindResource("Paused") as string ?? "[一時停止中]";
                    this.Title = $"{baseTitle} {pausedText}";
                }
                else
                {
                    icon.Symbol = SymbolRegular.Pause16;
                    icon.Filled = false;
                    LogOnlyButton.Appearance = ControlAppearance.Transparent;
                    LogOnlyButton.ToolTip = Application.Current.TryFindResource("ToolTipPauseTranslation") as string ?? "翻訳を一時停止 (ログのみ)";

                    this.Title = Application.Current.TryFindResource("MainWindowTitle") as string ?? "LiveCaptions Translator";
                }
            }
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            var window = sender as Window;
            WindowHandler.SaveState(window, Translator.Setting);
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MainWindow_LocationChanged(sender, e);
            IsAutoHeight = false;
        }

        public void ToggleTopmost(bool enabled)
        {
            var button = TopmostButton as Button;
            var symbolIcon = button?.Icon as SymbolIcon;
            if (symbolIcon != null) symbolIcon.Filled = enabled;
            this.Topmost = enabled;
            if (Translator.Setting?.MainWindow != null && Translator.Setting.MainWindow.Topmost != enabled)
            {
                Translator.Setting.MainWindow.Topmost = enabled;
            }
        }

        private void CheckForFirstUse()
        {
            if (!Translator.FirstUseFlag)
                return;

            OpenAppSettingWindow();
            LiveCaptionsHandler.RestoreLiveCaptions(Translator.Window);

            Dispatcher.InvokeAsync(() =>
            {
                var welcomeWindow = new WelcomeWindow
                {
                    Owner = this
                };
                welcomeWindow.Show();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            OpenAppSettingWindow();
        }

        public static void OpenAppSettingWindow()
        {
            if (_appSettingWindow != null && _appSettingWindow.IsLoaded)
            {
                _appSettingWindow.Activate();
            }
            else
            {
                _appSettingWindow = new AppSettingWindow();
                _appSettingWindow.Closed += (s, ev) => _appSettingWindow = null;
                _appSettingWindow.Show();
            }
        }

        private async Task CheckForUpdates()
        {
            if (Translator.FirstUseFlag)
                return;

            string latestVersion = string.Empty;
            try
            {
                latestVersion = await UpdateUtil.GetLatestVersion();
            }
            catch (Exception ex)
            {
                string errMsg = Application.Current.TryFindResource("UpdateCheckFailed") as string ?? "[ERROR] Update Check Failed.";
                SnackbarHost.Show(errMsg, ex.Message, SnackbarType.Error,
                    timeout: 2, closeButton: true);

                return;
            }

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            var ignoredVersion = Translator.Setting.IgnoredUpdateVersion;
            if (!string.IsNullOrEmpty(ignoredVersion) && ignoredVersion == latestVersion)
                return;
            if (!string.IsNullOrEmpty(latestVersion) && latestVersion != currentVersion)
            {
                string title = Application.Current.TryFindResource("UpdateTitle") as string ?? "New Version Available";
                string contentTmpl = Application.Current.TryFindResource("UpdateContent") as string ?? "A new version has been detected: {0}\nCurrent version: {1}\nPlease visit GitHub to download the latest release.";
                string content = string.Format(contentTmpl, latestVersion, currentVersion);
                string btnUpdate = Application.Current.TryFindResource("ButtonUpdate") as string ?? "Update";
                string btnIgnore = Application.Current.TryFindResource("ButtonIgnoreVersion") as string ?? "Ignore this version";

                var dialog = new Wpf.Ui.Controls.MessageBox
                {
                    Title = title,
                    Content = content,
                    PrimaryButtonText = btnUpdate,
                    CloseButtonText = btnIgnore
                };
                var result = await dialog.ShowDialogAsync();

                if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
                {
                    var url = UpdateUtil.GitHubReleasesUrl;
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        string errMsg = Application.Current.TryFindResource("OpenBrowserFailed") as string ?? "[ERROR] Open Browser Failed.";
                        SnackbarHost.Show(errMsg, ex.Message, SnackbarType.Error,
                            timeout: 2, closeButton: true);
                    }
                }
                else
                    Translator.Setting.IgnoredUpdateVersion = latestVersion;
            }
        }

        public void ShowLogCard(bool enabled)
        {
            CaptionPage.Instance?.CollapseTranslatedCaption(enabled);
        }

        private void ShowOriginalButton_Click(object sender, RoutedEventArgs e)
        {
            if (Translator.Setting?.MainWindow != null)
            {
                Translator.Setting.MainWindow.ShowOriginalCaption = !Translator.Setting.MainWindow.ShowOriginalCaption;
            }
        }

        public void UpdateShowOriginalButton(bool enabled)
        {
            if (ShowOriginalButton.Icon is SymbolIcon icon)
            {
                if (enabled)
                {
                    icon.Symbol = SymbolRegular.Eye24;
                    icon.Filled = true;
                }
                else
                {
                    icon.Symbol = SymbolRegular.EyeOff24;
                    icon.Filled = false;
                }
            }
        }

        private void AutoScrollButton_Click(object sender, RoutedEventArgs e)
        {
            if (Translator.Setting?.MainWindow != null)
            {
                Translator.Setting.MainWindow.AutoScrollEnabled = !Translator.Setting.MainWindow.AutoScrollEnabled;
            }
        }

        public void UpdateAutoScrollButton(bool enabled)
        {
            if (AutoScrollButton.Icon is SymbolIcon icon)
            {
                AutoScrollButton.Appearance = enabled ? ControlAppearance.Primary : ControlAppearance.Transparent;

                if (enabled)
                {
                    icon.Symbol = SymbolRegular.ArrowDown24;
                    icon.Filled = true;
                }
                else
                {
                    icon.Symbol = SymbolRegular.ArrowDown24;
                    icon.Filled = false;
                }
            }
        }
        private void TogglePreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (Translator.Setting?.MainWindow != null)
            {
                Translator.Setting.MainWindow.HidePreviewEnabled = !Translator.Setting.MainWindow.HidePreviewEnabled;
            }
        }

        public void UpdateTogglePreviewButtonState(bool hideEnabled)
        {
            if (TogglePreviewButton == null) return;

            if (TogglePreviewButton.Icon is SymbolIcon icon)
            {
                TogglePreviewButton.Appearance = hideEnabled ? ControlAppearance.Primary : ControlAppearance.Transparent;
                icon.Filled = hideEnabled;
            }
        }
        private void MainWindowSetting_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Translator.Setting?.MainWindow == null) return;

            if (e.PropertyName == nameof(Translator.Setting.MainWindow.AutoScrollEnabled))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (Translator.Setting?.MainWindow != null)
                    {
                        UpdateAutoScrollButton(Translator.Setting.MainWindow.AutoScrollEnabled);
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else if (e.PropertyName == nameof(Translator.Setting.MainWindow.ShowOriginalCaption))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (Translator.Setting?.MainWindow != null)
                    {
                        UpdateShowOriginalButton(Translator.Setting.MainWindow.ShowOriginalCaption);
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else if (e.PropertyName == nameof(Translator.Setting.MainWindow.HidePreviewEnabled))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (Translator.Setting?.MainWindow != null)
                    {
                        UpdateTogglePreviewButtonState(Translator.Setting.MainWindow.HidePreviewEnabled);
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else if (e.PropertyName == nameof(Translator.Setting.MainWindow.CaptionLogEnabled))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (Translator.Setting?.MainWindow != null)
                    {
                        ShowLogCard(Translator.Setting.MainWindow.CaptionLogEnabled);
                        IsAutoHeight = true;
                        CaptionPage.Instance?.AutoHeight();
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else if (e.PropertyName == nameof(Translator.Setting.MainWindow.Topmost))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (Translator.Setting?.MainWindow != null)
                    {
                        ToggleTopmost(Translator.Setting.MainWindow.Topmost);
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        public void AutoHeightAdjust(int minHeight = -1, int maxHeight = -1)
        {
            if (minHeight > 0 && Height < minHeight)
            {
                Height = minHeight;
                IsAutoHeight = true;
            }

            if (IsAutoHeight && maxHeight > 0 && Height > maxHeight)
                Height = maxHeight;
        }

        private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (CaptionPage.Instance != null && CaptionPage.Instance.IsVisible)
            {
                var scrollViewer = CaptionPage.Instance.CaptionPageScrollViewer;
                if (scrollViewer != null)
                {
                    int lines = Math.Abs(e.Delta) / 40;
                    if (lines == 0) lines = 1;

                    for (int i = 0; i < lines; i++)
                    {
                        if (e.Delta > 0)
                            scrollViewer.LineUp();
                        else
                            scrollViewer.LineDown();
                    }
                    e.Handled = true;
                }
            }
        }

        private void LiveCaptionsToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (Translator.Window == null)
                return;

            bool isHide = Translator.Window.Current.BoundingRectangle == Rect.Empty;
            if (isHide)
            {
                LiveCaptionsHandler.RestoreLiveCaptions(Translator.Window);
            }
            else
            {
                LiveCaptionsHandler.HideLiveCaptions(Translator.Window);
            }
            UpdateLiveCaptionsButtonState();
        }

        public void UpdateLiveCaptionsButtonState()
        {
            if (Translator.Window == null || LiveCaptionsToggleButton == null) return;

            bool isHide = Translator.Window.Current.BoundingRectangle == Rect.Empty;
            if (LiveCaptionsToggleButton.Icon is SymbolIcon icon)
            {
                icon.Filled = !isHide;
                LiveCaptionsToggleButton.Appearance = !isHide ? ControlAppearance.Primary : ControlAppearance.Transparent;
            }
        }

        private async void BatchTranslateButton_Click(object sender, RoutedEventArgs e)
        {
            var originalSentences = Translator.Caption?.Contexts
                .Select(c => c.SourceText?.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            if (originalSentences == null || originalSentences.Count == 0)
            {
                string noTextMsg = "翻訳元の履歴がありません。字幕が文字起こしされてから実行してください。";
                SnackbarHost.Show("一括翻訳", noTextMsg, SnackbarType.Warning, timeout: 3);
                return;
            }

            string batchApiName = Translator.Setting?.BatchApiName ?? "OpenRouter";
            BaseLLMConfig? config = null;

            if (batchApiName != "Google" && batchApiName != "Google2")
            {
                if (!TranslateAPI.GetBatchLLMConfig(batchApiName, out config))
                {
                    string errorMsg = $"一括翻訳用 API として「{batchApiName}」が指定されていますが、API設定（APIキーまたはモデル名）が入力されていません。設定画面で API の設定を行ってください。";
                    var messageBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "LLM未設定",
                        Content = errorMsg,
                        CloseButtonText = "OK"
                    };
                    await messageBox.ShowDialogAsync();
                    return;
                }
            }

            LoadingOverlay.Visibility = Visibility.Visible;
            Cursor = Cursors.Wait;
            BatchTranslateButton.IsEnabled = false;

            try
            {
                string translatedFullText = await Task.Run(async () =>
                {
                    if (batchApiName == "Google" || batchApiName == "Google2")
                    {
                        string joinedText = string.Join("\n", originalSentences);
                        if (batchApiName == "Google")
                        {
                            return await TranslateAPI.Google(joinedText);
                        }
                        else
                        {
                            return await TranslateAPI.Google2(joinedText);
                        }
                    }
                    else
                    {
                        var inputObjects = originalSentences.Select((text, index) => new { id = index, text = text }).ToList();
                        string inputJson = JsonSerializer.Serialize(inputObjects, new JsonSerializerOptions { WriteIndented = true });

                        return await TranslateAPI.TranslateBatchWithLLM(batchApiName, config!, inputJson, "ja-JP");
                    }
                });

                if (translatedFullText.StartsWith("[ERROR]"))
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    var errorBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "一括翻訳エラー",
                        Content = $"翻訳処理中にエラーが発生しました:\n{translatedFullText}",
                        CloseButtonText = "閉じる"
                    };
                    await errorBox.ShowDialogAsync();
                    return;
                }

                var rows = new List<BatchTranslationRow>();
                bool parsedSuccessfully = false;
                var translatedMap = new Dictionary<int, string>();

                if (batchApiName != "Google" && batchApiName != "Google2")
                {
                    // 【第1段】 JsonDocumentによる柔軟なパース
                    try
                    {
                        string rawResponse = translatedFullText.Trim();
                        string jsonStr = ExtractJsonArray(rawResponse);

                        if (!string.IsNullOrEmpty(jsonStr))
                        {
                            using var doc = JsonDocument.Parse(jsonStr);
                            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var element in doc.RootElement.EnumerateArray())
                                {
                                    if (element.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out int id))
                                    {
                                        // プロンプトで指示している "translation" キーを探す
                                        if (element.TryGetProperty("translation", out var transElement))
                                        {
                                            translatedMap[id] = transElement.GetString() ?? "";
                                        }
                                        // LLMが勝手に "text" にしてしまった場合の備え
                                        else if (element.TryGetProperty("text", out var textElement))
                                        {
                                            translatedMap[id] = textElement.GetString() ?? "";
                                        }
                                    }
                                }
                                if (translatedMap.Count > 0)
                                {
                                    parsedSuccessfully = true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"JSONパース失敗 (正規表現フォールバックへ移行します): {ex.Message}");
                    }

                    // 【第2段】 JSONが崩れていてパース失敗した場合の、正規表現による強制抽出
                    if (!parsedSuccessfully)
                    {
                        try
                        {
                            // "id": 数値, "translation": "文字列" のパターンを強制的に検索
                            string pattern = @"""id""\s*:\s*(?<id>\d+)\s*,\s*""(?:translation|text)""\s*:\s*""(?<text>(?:[^""\\]|\\.)*?)""";
                            var matches = System.Text.RegularExpressions.Regex.Matches(
                                translatedFullText,
                                pattern,
                                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                            if (matches.Count > 0)
                            {
                                foreach (System.Text.RegularExpressions.Match match in matches)
                                {
                                    if (int.TryParse(match.Groups["id"].Value, out int id))
                                    {
                                        // 正規表現でエスケープされた文字（\n や \" など）を元に戻す
                                        string unescapedText = System.Text.RegularExpressions.Regex.Unescape(match.Groups["text"].Value);
                                        translatedMap[id] = unescapedText;
                                    }
                                }
                                if (translatedMap.Count > 0)
                                {
                                    parsedSuccessfully = true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"正規表現抽出エラー: {ex.Message}");
                        }
                    }

                    // 抽出成功時のリスト構築
                    if (parsedSuccessfully)
                    {
                        for (int i = 0; i < originalSentences.Count; i++)
                        {
                            string src = originalSentences[i];
                            string trans = translatedMap.TryGetValue(i, out var t) ? t : string.Empty;
                            rows.Add(new BatchTranslationRow
                            {
                                SourceText = src,
                                TranslatedText = trans
                            });
                        }
                    }
                }

                // 【最終手段】 LLMがJSONを一切返さず、ただのテキストを返してきた場合の従来フォールバック
                if (!parsedSuccessfully)
                {
                    var translatedLines = translatedFullText
                        .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries) // 空行は詰める
                        .Select(line => line.Trim())
                        .Where(line => !line.StartsWith("{") && !line.StartsWith("[")) // JSONの残骸を除外
                        .ToList();

                    int maxCount = Math.Max(originalSentences.Count, translatedLines.Count);
                    for (int i = 0; i < maxCount; i++)
                    {
                        string src = i < originalSentences.Count ? originalSentences[i] : string.Empty;
                        string trans = i < translatedLines.Count ? translatedLines[i] : string.Empty;
                        rows.Add(new BatchTranslationRow
                        {
                            SourceText = src,
                            TranslatedText = trans
                        });
                    }
                }

                // （以降はそのまま）
                var batchWindow = new BatchTranslationWindow(batchApiName, "日本語", rows)
                {
                    Owner = this
                };
                batchWindow.Show();
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                var exceptionBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "一括翻訳エラー",
                    Content = $"予期しないエラーが発生しました:\n{ex.Message}",
                    CloseButtonText = "閉じる"
                };
                await exceptionBox.ShowDialogAsync();
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                Cursor = Cursors.Arrow;
                BatchTranslateButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 文字列から最初に見つかる '[' と、最後に見つかる ']' の範囲（JSON配列）を抽出します。
        /// </summary>
        private string ExtractJsonArray(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            int startIndex = input.IndexOf('[');
            int endIndex = input.LastIndexOf(']');

            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                return input.Substring(startIndex, endIndex - startIndex + 1);
            }
            return input;
        }
    }
}
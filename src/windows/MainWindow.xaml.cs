using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using LiveCaptionsTranslator.utils;
using LiveCaptionsTranslator.Utils;
using Button = Wpf.Ui.Controls.Button;

namespace LiveCaptionsTranslator
{
    public partial class MainWindow : Window
    {
        public bool IsAutoHeight { get; set; } = true;
        private static AppSettingWindow? _appSettingWindow;

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
            var button = sender as Button;
            var symbolIcon = button?.Icon as SymbolIcon;

            if (Translator.LogOnlyFlag)
            {
                Translator.LogOnlyFlag = false;
                symbolIcon.Filled = false;
            }
            else
            {
                Translator.LogOnlyFlag = true;
                symbolIcon.Filled = true;
            }

            Translator.ClearContexts();
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

        // 各設定プロパティ変更時のイベントハンドラ（UI表示を一元更新する）
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

        // ホーム画面（CaptionPage）のアクティブ時にマウスホイールスクロールを強制的に制御する
        private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (CaptionPage.Instance != null && CaptionPage.Instance.IsVisible)
            {
                var scrollViewer = CaptionPage.Instance.CaptionPageScrollViewer;
                if (scrollViewer != null)
                {
                    // マウスホイールの回転量に応じてスクロール量を決定
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
    }
}
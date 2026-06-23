using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator
{
    public partial class CaptionPage : Page
    {
        public const int CARD_HEIGHT = 110;

        // プログラムによって最下部へ自動スクロール中かどうかのフラグ
        private bool _isProgrammaticScroll = false;
        // 最下部とみなす許容誤差（ピクセル）
        private const double SCROLL_BOTTOM_TOLERANCE = 10.0;

        private static CaptionPage instance;
        public static CaptionPage Instance => instance;

        public CaptionPage()
        {
            InitializeComponent();
            DataContext = Translator.Caption;
            instance = this;

            Loaded += (s, e) =>
            {
                AutoHeight();
                var mainWindow = App.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.CaptionLogButton.Visibility = Visibility.Visible;
                    mainWindow.ShowOriginalButton.Visibility = Visibility.Visible;
                    mainWindow.AutoScrollButton.Visibility = Visibility.Visible;
                    ScrollViewer.SetVerticalScrollBarVisibility(mainWindow.RootNavigation, ScrollBarVisibility.Disabled);
                    ScrollViewer.SetHorizontalScrollBarVisibility(mainWindow.RootNavigation, ScrollBarVisibility.Disabled);

                    // レイアウト崩れを防ぐためにウィンドウやナビゲーションのリサイズイベントを購読
                    mainWindow.SizeChanged += MainWindow_SizeChanged;
                    if (mainWindow.RootNavigation != null)
                    {
                        mainWindow.RootNavigation.SizeChanged += RootNavigation_SizeChanged;
                    }
                    UpdateScrollViewerMaxHeight(mainWindow);
                }
                Translator.Caption.PropertyChanged += TranslatedChanged;

                if (Translator.Setting != null)
                {
                    Translator.Setting.PropertyChanged += SettingChanged;
                    if (Translator.Setting.MainWindow != null)
                    {
                        Translator.Setting.MainWindow.PropertyChanged += MainWindowSettingChanged;
                    }
                }

                if (App.Current.MainWindow is MainWindow mw)
                {
                    mw.PreviewKeyDown += MainWindow_PreviewKeyDown;
                }

                UpdateOriginalCaptionVisibility();

                // ページロード時に最下部へスクロール
                ScrollToBottomIfEnabled();
            };
            Unloaded += (s, e) =>
            {
                var mainWindow = App.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.CaptionLogButton.Visibility = Visibility.Collapsed;
                    mainWindow.ShowOriginalButton.Visibility = Visibility.Collapsed;
                    mainWindow.AutoScrollButton.Visibility = Visibility.Collapsed;
                    ScrollViewer.SetVerticalScrollBarVisibility(mainWindow.RootNavigation, ScrollBarVisibility.Auto);
                    ScrollViewer.SetHorizontalScrollBarVisibility(mainWindow.RootNavigation, ScrollBarVisibility.Auto);

                    mainWindow.SizeChanged -= MainWindow_SizeChanged;
                    if (mainWindow.RootNavigation != null)
                    {
                        mainWindow.RootNavigation.SizeChanged -= RootNavigation_SizeChanged;
                    }
                }
                Translator.Caption.PropertyChanged -= TranslatedChanged;

                if (Translator.Setting != null)
                {
                    Translator.Setting.PropertyChanged -= SettingChanged;
                    if (Translator.Setting.MainWindow != null)
                    {
                        Translator.Setting.MainWindow.PropertyChanged -= MainWindowSettingChanged;
                    }
                }

                if (App.Current.MainWindow is MainWindow mw)
                {
                    mw.PreviewKeyDown -= MainWindow_PreviewKeyDown;
                }
            };

            CollapseTranslatedCaption(Translator.Setting.MainWindow.CaptionLogEnabled);
            UpdateOriginalCaptionVisibility();
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is MainWindow mainWindow)
            {
                UpdateScrollViewerMaxHeight(mainWindow);
            }
        }

        private void RootNavigation_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var mainWindow = App.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                UpdateScrollViewerMaxHeight(mainWindow);
            }
        }

        private void UpdateScrollViewerMaxHeight(MainWindow mainWindow)
        {
            if (mainWindow.RootNavigation != null)
            {
                // ナビゲーションコントロールの実際の高さからマージン（12px * 2 = 24px）を引いてスクロールの最大高さを制限
                double navHeight = mainWindow.RootNavigation.ActualHeight;
                if (navHeight > 0)
                {
                    CaptionPageScrollViewer.MaxHeight = Math.Max(0, navHeight - 24);
                }
            }
        }

        /// <summary>
        /// 自動スクロールが有効かつユーザーが上退避していない場合、最下部にスクロールします。
        /// レイアウト確定後（DispatcherPriority.Background）に実行することで確実に最新の高さを参照します。
        /// </summary>
        private void ScrollToBottomIfEnabled()
        {
            if (!Translator.Setting.MainWindow.AutoScrollEnabled) return;

            // Background優先度 = レイアウト（Render）・バインディング更新（DataBind）後に実行される
            Dispatcher.BeginInvoke(new Action(() =>
            {
                double distanceFromBottom = CaptionPageScrollViewer.ExtentHeight - CaptionPageScrollViewer.ViewportHeight - CaptionPageScrollViewer.VerticalOffset;
                if (distanceFromBottom > SCROLL_BOTTOM_TOLERANCE)
                {
                    _isProgrammaticScroll = true;
                    CaptionPageScrollViewer.ScrollToEnd();
                }
                else
                {
                    _isProgrammaticScroll = false;
                }
            }), DispatcherPriority.Background);
        }

        private async void TextBlock_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                try
                {
                    Clipboard.SetText(textBlock.Text);
                    SnackbarHost.Show("Copied.", textBlock.Text, SnackbarType.Info, 100);
                }
                catch
                {
                    SnackbarHost.Show("Copy Failed.", string.Empty, SnackbarType.Error, 100);
                }
                await Task.Delay(500);
            }
        }

        private void TranslatedChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Translator.Caption.DisplayTranslatedCaption))
            {
                if (Encoding.UTF8.GetByteCount(Translator.Caption.DisplayTranslatedCaption) >= TextUtil.LONG_THRESHOLD)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.TranslatedCaption.FontSize = 15;
                    }), DispatcherPriority.Background);
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.TranslatedCaption.FontSize = 18;
                    }), DispatcherPriority.Background);
                }
            }

            if (e.PropertyName == nameof(Translator.Caption.DisplayLogCards) ||
                e.PropertyName == nameof(Translator.Caption.DisplayTranslatedCaption) ||
                e.PropertyName == nameof(Translator.Caption.DisplayOriginalCaption))
            {
                ScrollToBottomIfEnabled();
            }
        }

        private void SettingChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Translator.Setting.AutoTranslate))
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateOriginalCaptionVisibility()), DispatcherPriority.Background);
            }
        }

        private void MainWindowSettingChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Translator.Setting.MainWindow.ShowOriginalCaption))
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateOriginalCaptionVisibility()), DispatcherPriority.Background);
            }
            else if (e.PropertyName == nameof(Translator.Setting.MainWindow.AutoScrollEnabled))
            {
                if (Translator.Setting.MainWindow.AutoScrollEnabled)
                {
                    ScrollToBottomIfEnabled();
                }
            }
        }

        private void UpdateOriginalCaptionVisibility()
        {
            if (OriginalCaptionCard == null) return;

            if (Translator.Setting.MainWindow.CaptionLogEnabled && Translator.Setting.AutoTranslate)
            {
                OriginalCaptionCard.Visibility = Visibility.Collapsed;
            }
            else
            {
                bool showOriginal = Translator.Setting.MainWindow.ShowOriginalCaption || !Translator.Setting.AutoTranslate;
                OriginalCaptionCard.Visibility = showOriginal ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// ログカードモードの切り替え（isCollapsed=true: ログカード表示、false: 翻訳テキストのみ表示）
        /// </summary>
        public void CollapseTranslatedCaption(bool isCollapsed)
        {
            if (isCollapsed)
            {
                // ログカードモードON: ログカードを表示し、個別の翻訳カード・原文カードは非表示（ログ内に統合されるため）
                LogCards.Visibility = Visibility.Visible;
                TranslatedCaptionCard.Visibility = Visibility.Collapsed;
                OriginalCaptionCard.Visibility = Visibility.Collapsed;
            }
            else
            {
                // ログカードモードOFF: ログカードを非表示にして翻訳カードのみ表示
                LogCards.Visibility = Visibility.Collapsed;
                TranslatedCaptionCard.Visibility = Visibility.Visible;
                UpdateOriginalCaptionVisibility();
            }
        }

        public void AutoHeight()
        {
            var mainWindow = App.Current.MainWindow as MainWindow;
            if (mainWindow == null) return;

            if (Translator.Setting.MainWindow.CaptionLogEnabled)
            {
                if (mainWindow.Height <= 170)
                {
                    mainWindow.Height = 400;
                }
                mainWindow.AutoHeightAdjust(
                    minHeight: 170,
                    maxHeight: -1);
            }
            else
            {
                mainWindow.AutoHeightAdjust(
                    minHeight: 170,
                    maxHeight: 170);
            }
        }

        private void ManualTranslateButton_Click(object sender, RoutedEventArgs e)
        {
            Translator.TriggerManualTranslation();
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.T &&
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                Translator.TriggerManualTranslation();
                e.Handled = true;
            }
        }

        private void CaptionPageScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // プログラムによるスクロール中のイベントは無視し、最下部に到達したらフラグをリセットする
            if (_isProgrammaticScroll)
            {
                double distanceFromBottom = e.ExtentHeight - e.ViewportHeight - e.VerticalOffset;
                if (distanceFromBottom <= SCROLL_BOTTOM_TOLERANCE)
                {
                    _isProgrammaticScroll = false;
                }
                return;
            }

            // コンテンツの高さ変更（新テキスト追加やリサイズなど）
            if (e.ExtentHeightChange > 0)
            {
                if (Translator.Setting.MainWindow.AutoScrollEnabled)
                {
                    ScrollToBottomIfEnabled();
                }
                return;
            }

            // スクロール位置のみが変更された（ユーザーの手動操作）
            if (e.VerticalChange != 0)
            {
                double distanceFromBottom = e.ExtentHeight - e.ViewportHeight - e.VerticalOffset;
                if (distanceFromBottom > SCROLL_BOTTOM_TOLERANCE)
                {
                    // ユーザーが上方向にスクロールしたため、自動スクロールをOFFにする
                    if (Translator.Setting.MainWindow.AutoScrollEnabled)
                    {
                        Translator.Setting.MainWindow.AutoScrollEnabled = false;
                    }
                }
                else
                {
                    // ユーザーが最下部までスクロールしたため、自動スクロールをONにする
                    if (!Translator.Setting.MainWindow.AutoScrollEnabled)
                    {
                        Translator.Setting.MainWindow.AutoScrollEnabled = true;
                    }
                }
            }
        }
    }
}

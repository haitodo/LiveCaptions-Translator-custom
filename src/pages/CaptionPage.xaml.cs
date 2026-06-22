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

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    CaptionPageScrollViewer.ScrollToEnd();
                }), DispatcherPriority.Background);
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
                if (Translator.Setting.MainWindow.AutoScrollEnabled)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CaptionPageScrollViewer.ScrollToEnd();
                    }), DispatcherPriority.Background);
                }
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
        }

        private void UpdateOriginalCaptionVisibility()
        {
            if (OriginalCaptionCard == null) return;

            bool showOriginal = Translator.Setting.MainWindow.ShowOriginalCaption || !Translator.Setting.AutoTranslate;
            OriginalCaptionCard.Visibility = showOriginal ? Visibility.Visible : Visibility.Collapsed;
        }

        public void CollapseTranslatedCaption(bool isCollapsed)
        {
            var converter = new GridLengthConverter();

            if (isCollapsed)
            {
                CaptionLogCard_Row.Height = (GridLength)converter.ConvertFromString("*");
                TranslatedCaption_Row.Height = (GridLength)converter.ConvertFromString("Auto");
                LogCards.Visibility = Visibility.Visible;
            }
            else
            {
                CaptionLogCard_Row.Height = (GridLength)converter.ConvertFromString("0");
                TranslatedCaption_Row.Height = (GridLength)converter.ConvertFromString("*");
                LogCards.Visibility = Visibility.Collapsed;
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
            if (Translator.Setting.MainWindow.AutoScrollEnabled && e.ExtentHeightChange > 0)
            {
                CaptionPageScrollViewer.ScrollToEnd();
            }
        }
    }
}

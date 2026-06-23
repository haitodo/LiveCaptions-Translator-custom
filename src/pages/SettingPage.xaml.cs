using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Appearance;

using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.utils;
using Wpf.Ui.Controls;

namespace LiveCaptionsTranslator
{
    public partial class SettingPage : Page
    {
        private static SettingWindow? SettingWindow;

        public SettingPage()
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();
            DataContext = Translator.Setting;

            Loaded += (s, e) =>
            {
                var mainWindow = App.Current.MainWindow as MainWindow;
                if (mainWindow != null && mainWindow.IsAutoHeight)
                {
                    mainWindow.AutoHeightAdjust(maxHeight: (int)mainWindow.MinHeight);
                }
                CheckForFirstUse();
                UpdateButtonText();
            };

            TranslateAPIBox.ItemsSource = Translator.Setting?.Configs.Keys;
            TranslateAPIBox.SelectedIndex = 0;

            // UI言語セレクターの初期選択状態を設定します
            string currentLang = Translator.Setting?.InterfaceLanguage ?? "ja";
            foreach (ComboBoxItem item in UILanguageBox.Items)
            {
                if (item.Tag as string == currentLang)
                {
                    UILanguageBox.SelectedItem = item;
                    break;
                }
            }

            LoadAPISetting();
            PopulateCaptionFontColors();
            PopulateCaptionFontFamilies();
        }

        private void UpdateButtonText()
        {
            if (Translator.Window != null && ButtonText != null)
            {
                bool isHide = Translator.Window.Current.BoundingRectangle == Rect.Empty;
                ButtonText.Text = Application.Current.TryFindResource(isHide ? "ButtonHide" : "ButtonShow") as string ?? (isHide ? "Hide" : "Show");
            }
        }

        private void LiveCaptionsButton_click(object sender, RoutedEventArgs e)
        {
            if (Translator.Window == null)
                return;

            var button = sender as Wpf.Ui.Controls.Button;

            bool isHide = Translator.Window.Current.BoundingRectangle == Rect.Empty;
            if (isHide)
            {
                LiveCaptionsHandler.RestoreLiveCaptions(Translator.Window);
            }
            else
            {
                LiveCaptionsHandler.HideLiveCaptions(Translator.Window);
            }
            UpdateButtonText();
            (App.Current.MainWindow as MainWindow)?.UpdateLiveCaptionsButtonState();
        }

        private void TranslateAPIBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadAPISetting();
        }

        private void TargetLangBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TargetLangBox.SelectedItem != null)
                Translator.Setting.TargetLanguage = TargetLangBox.SelectedItem.ToString();
        }

        private void TargetLangBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Translator.Setting.TargetLanguage = TargetLangBox.Text;
        }

        private void APISettingButton_click(object sender, RoutedEventArgs e)
        {
            if (SettingWindow != null && SettingWindow.IsLoaded)
                SettingWindow.Activate();
            else
            {
                SettingWindow = new SettingWindow();
                SettingWindow.Closed += (sender, args) => SettingWindow = null;
                SettingWindow.Show();
            }
        }

        private void Contexts_ValueChanged(object sender, NumberBoxValueChangedEventArgs args)
        {
            if (Translator.Setting.DisplaySentences > Translator.Setting.NumContexts)
                Translator.Setting.DisplaySentences = Translator.Setting.NumContexts;
        }

        private void DisplaySentences_ValueChanged(object sender, NumberBoxValueChangedEventArgs args)
        {
            if (Translator.Setting.DisplaySentences > Translator.Setting.NumContexts)
                Translator.Setting.NumContexts = Translator.Setting.DisplaySentences;
            Translator.Caption.OnPropertyChanged("DisplayLogCards");
            Translator.Caption.OnPropertyChanged("OverlayPreviousTranslation");
        }

        private void LiveCaptionsInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            LiveCaptionsInfoFlyout.Show();
        }

        private void LiveCaptionsInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            LiveCaptionsInfoFlyout.Hide();
        }

        private void FrequencyInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            FrequencyInfoFlyout.Show();
        }

        private void FrequencyInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            FrequencyInfoFlyout.Hide();
        }

        private void TranslateAPIInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            TranslateAPIInfoFlyout.Show();
        }

        private void TranslateAPIInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            TranslateAPIInfoFlyout.Hide();
        }

        private void TargetLangInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            TargetLangInfoFlyout.Show();
        }

        private void TargetLangInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            TargetLangInfoFlyout.Hide();
        }

        private void CaptionLogMaxInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            CaptionLogMaxInfoFlyout.Show();
        }

        private void CaptionLogMaxInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            CaptionLogMaxInfoFlyout.Hide();
        }

        private void ContextAwareInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            ContextAwareInfoFlyout.Show();
        }

        private void ContextAwareInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            ContextAwareInfoFlyout.Hide();
        }

        private void AutoTranslateInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            AutoTranslateInfoFlyout.Show();
        }

        private void AutoTranslateInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            AutoTranslateInfoFlyout.Hide();
        }

        private void AccumulateEnabledInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            AccumulateEnabledInfoFlyout.Show();
        }

        private void AccumulateEnabledInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            AccumulateEnabledInfoFlyout.Hide();
        }

        private void CheckForFirstUse()
        {
            if (Translator.FirstUseFlag)
                UpdateButtonText();
        }

        private void UILanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UILanguageBox.SelectedItem is ComboBoxItem item && item.Tag is string lang)
            {
                if (Translator.Setting != null && Translator.Setting.InterfaceLanguage != lang)
                {
                    Translator.Setting.InterfaceLanguage = lang;
                    LocalizationManager.SetLanguage(lang);
                    
                    // UIテキストを更新します
                    UpdateButtonText();
                    PopulateCaptionFontColors();
                    PopulateCaptionFontFamilies();
                }
            }
        }

        public void LoadAPISetting()
        {
            var configType = Translator.Setting[Translator.Setting.ApiName].GetType();
            var languagesProp = configType.GetProperty(
                "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);

            // Traverse base classes to find `SupportedLanguages`
            while (configType != null && languagesProp == null)
            {
                configType = configType.BaseType;
                languagesProp = configType.GetProperty(
                    "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);
            }
            if (languagesProp == null)
                languagesProp = typeof(TranslateAPIConfig).GetProperty(
                    "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);

            var supportedLanguages = (Dictionary<string, string>)languagesProp.GetValue(null);
            TargetLangBox.ItemsSource = supportedLanguages.Keys;

            string targetLang = Translator.Setting.TargetLanguage;
            if (!supportedLanguages.ContainsKey(targetLang))
                supportedLanguages[targetLang] = targetLang;    // add custom language to supported languages
            TargetLangBox.SelectedItem = targetLang;
        }

        private void PopulateCaptionFontColors()
        {
            if (CaptionFontColorBox == null) return;

            var selectedTag = (CaptionFontColorBox.SelectedItem as ComboBoxItem)?.Tag as Utils.Color?;
            if (selectedTag == null && Translator.Setting?.MainWindow != null)
            {
                selectedTag = Translator.Setting.MainWindow.CaptionFontColor;
            }

            CaptionFontColorBox.Items.Clear();
            CaptionFontColorBox.Items.Add(new ComboBoxItem { Content = Application.Current.TryFindResource("ColorDefault") as string ?? "Default", Tag = Utils.Color.Default });
            CaptionFontColorBox.Items.Add(new ComboBoxItem { Content = Application.Current.TryFindResource("ColorWhite") as string ?? "White", Tag = Utils.Color.White });
            CaptionFontColorBox.Items.Add(new ComboBoxItem { Content = Application.Current.TryFindResource("ColorYellow") as string ?? "Yellow", Tag = Utils.Color.Yellow });
            CaptionFontColorBox.Items.Add(new ComboBoxItem { Content = Application.Current.TryFindResource("ColorLimeGreen") as string ?? "Green", Tag = Utils.Color.LimeGreen });
            CaptionFontColorBox.Items.Add(new ComboBoxItem { Content = Application.Current.TryFindResource("ColorAqua") as string ?? "Aqua", Tag = Utils.Color.Aqua });
            CaptionFontColorBox.Items.Add(new ComboBoxItem { Content = Application.Current.TryFindResource("ColorBlue") as string ?? "Blue", Tag = Utils.Color.Blue });
            CaptionFontColorBox.Items.Add(new ComboBoxItem { Content = Application.Current.TryFindResource("ColorDeepPink") as string ?? "Pink", Tag = Utils.Color.DeepPink });
            CaptionFontColorBox.Items.Add(new ComboBoxItem { Content = Application.Current.TryFindResource("ColorRed") as string ?? "Red", Tag = Utils.Color.Red });
            CaptionFontColorBox.Items.Add(new ComboBoxItem { Content = Application.Current.TryFindResource("ColorBlack") as string ?? "Black", Tag = Utils.Color.Black });

            if (selectedTag != null)
            {
                foreach (ComboBoxItem item in CaptionFontColorBox.Items)
                {
                    if ((Utils.Color)item.Tag == selectedTag.Value)
                    {
                        CaptionFontColorBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void CaptionFontColorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CaptionFontColorBox.SelectedItem is ComboBoxItem item && item.Tag is Utils.Color color)
            {
                if (Translator.Setting?.MainWindow != null)
                {
                    Translator.Setting.MainWindow.CaptionFontColor = color;
                }
            }
        }

        private void PopulateCaptionFontFamilies()
        {
            if (CaptionFontFamilyBox == null) return;

            string selectedFont = "Default";
            if (CaptionFontFamilyBox.SelectedItem is ComboBoxItem selectedItem)
            {
                selectedFont = selectedItem.Tag as string ?? "Default";
            }
            else if (Translator.Setting?.MainWindow != null)
            {
                selectedFont = Translator.Setting.MainWindow.CaptionFontFamily;
            }

            CaptionFontFamilyBox.Items.Clear();
            CaptionFontFamilyBox.Items.Add(new ComboBoxItem { Content = Application.Current.TryFindResource("ColorDefault") as string ?? "Default", Tag = "Default" });

            var fonts = new System.Collections.Generic.List<string>();
            foreach (var fontFamily in System.Windows.Media.Fonts.SystemFontFamilies)
            {
                fonts.Add(fontFamily.Source);
            }
            fonts.Sort();

            foreach (var fontName in fonts)
            {
                CaptionFontFamilyBox.Items.Add(new ComboBoxItem { Content = fontName, Tag = fontName });
            }

            foreach (ComboBoxItem item in CaptionFontFamilyBox.Items)
            {
                if ((item.Tag as string) == selectedFont)
                {
                    CaptionFontFamilyBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void CaptionFontFamilyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CaptionFontFamilyBox.SelectedItem is ComboBoxItem item && item.Tag is string fontName)
            {
                if (Translator.Setting?.MainWindow != null)
                {
                    Translator.Setting.MainWindow.CaptionFontFamily = fontName;
                }
            }
        }
    }
}
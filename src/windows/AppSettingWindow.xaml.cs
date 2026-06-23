using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.utils;
using LiveCaptionsTranslator.Utils;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBlock = System.Windows.Controls.TextBlock;

namespace LiveCaptionsTranslator
{
    public partial class AppSettingWindow : FluentWindow
    {
        private System.Windows.Controls.Button currentSelected;
        private Dictionary<string, FrameworkElement> sectionReferences;
        private static SettingWindow? apiSettingWindow;

        public AppSettingWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();
            DataContext = Translator.Setting;

            Loaded += (sender, args) =>
            {
                SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);
                InitializeSections();
                SelectButton(GeneralNavButton);
                
                CheckForFirstUse();
                UpdateButtonText();
            };

            if (Translator.Setting != null)
            {
                TranslateAPIBox.ItemsSource = Translator.Setting.Configs.Keys;
                TranslateAPIBox.SelectedItem = Translator.Setting.ApiName;

                BatchTranslateAPIBox.ItemsSource = LiveCaptionsTranslator.apis.TranslateAPI.LLM_BASED_APIS;
                BatchTranslateAPIBox.SelectedItem = Translator.Setting.BatchApiName;
            }

            LoadAPISetting();
            PopulateCaptionFontColors();
            PopulateCaptionFontFamilies();
        }

        private void InitializeSections()
        {
            sectionReferences = new Dictionary<string, FrameworkElement>
            {
                { "General", GeneralSection },
                { "Translation", TranslationSection },
                { "Display", DisplaySection }
            };
        }

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button)
            {
                SelectButton(button);
                string targetSection = button.Tag.ToString();
                if (sectionReferences.TryGetValue(targetSection, out FrameworkElement element))
                    element.BringIntoView();
            }
        }

        private void SelectButton(System.Windows.Controls.Button button)
        {
            if (currentSelected != null)
                currentSelected.Background = new SolidColorBrush(Colors.Transparent);
            button.Background = (Brush)FindResource("ControlFillColorSecondaryBrush");
            currentSelected = button;
        }

        private void UpdateButtonText()
        {
            if (Translator.Window != null && ButtonText != null)
            {
                bool isHide = Translator.Window.Current.BoundingRectangle == Rect.Empty;
                ButtonText.Text = Application.Current.TryFindResource(isHide ? "ButtonHide" : "ButtonShow") as string ?? (isHide ? "Hide" : "Show");
            }
        }

        private void LiveCaptionsButton_Click(object sender, RoutedEventArgs e)
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
            UpdateButtonText();
            (App.Current.MainWindow as MainWindow)?.UpdateLiveCaptionsButtonState();
        }

        private void TranslateAPIBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadAPISetting();
        }

        private void TargetLangBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TargetLangBox.SelectedItem != null && Translator.Setting != null)
                Translator.Setting.TargetLanguage = TargetLangBox.SelectedItem.ToString();
        }

        private void TargetLangBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (Translator.Setting != null)
                Translator.Setting.TargetLanguage = TargetLangBox.Text;
        }

        private void APISettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (apiSettingWindow != null && apiSettingWindow.IsLoaded)
                apiSettingWindow.Activate();
            else
            {
                apiSettingWindow = new SettingWindow();
                apiSettingWindow.Closed += (s, args) => apiSettingWindow = null;
                apiSettingWindow.Show();
            }
        }

        private void Contexts_ValueChanged(object sender, NumberBoxValueChangedEventArgs args)
        {
            if (Translator.Setting == null) return;
            if (Translator.Setting.DisplaySentences > Translator.Setting.NumContexts)
                Translator.Setting.DisplaySentences = Translator.Setting.NumContexts;
        }

        private void DisplaySentences_ValueChanged(object sender, NumberBoxValueChangedEventArgs args)
        {
            if (Translator.Setting == null) return;
            if (Translator.Setting.DisplaySentences > Translator.Setting.NumContexts)
                Translator.Setting.NumContexts = Translator.Setting.DisplaySentences;
            Translator.Caption?.OnPropertyChanged("DisplayLogCards");
        }

        public void LoadAPISetting()
        {
            if (Translator.Setting == null) return;

            var configType = Translator.Setting[Translator.Setting.ApiName].GetType();
            var languagesProp = configType.GetProperty(
                "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);

            // 基底クラスを遡って SupportedLanguages を探索
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
                supportedLanguages[targetLang] = targetLang;    // カスタム言語をサポート言語に追加

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

        private void CheckForFirstUse()
        {
            if (Translator.FirstUseFlag)
                UpdateButtonText();
        }
    }
}

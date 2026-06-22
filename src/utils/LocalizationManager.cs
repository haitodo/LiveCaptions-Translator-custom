using System;
using System.Windows;

namespace LiveCaptionsTranslator.utils
{
    public static class LocalizationManager
    {
        private static ResourceDictionary? currentDictionary;

        /// <summary>
        /// 指定された言語（"ja" または "en"）にUI言語を切り替えます。
        /// </summary>
        /// <param name="lang">言語コード</param>
        public static void SetLanguage(string lang)
        {
            // 既に適用済みのローカライズ用ディクショナリがあれば削除します
            if (currentDictionary != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(currentDictionary);
            }

            // 安全のため、他に追加されている可能性があるローカライズディクショナリも検索して削除します
            ResourceDictionary? existing = null;
            foreach (var dict in Application.Current.Resources.MergedDictionaries)
            {
                if (dict.Source != null && 
                    (dict.Source.OriginalString.Contains("/src/strings/Strings.") || 
                     dict.Source.OriginalString.Contains("/strings/Strings.")))
                {
                    existing = dict;
                    break;
                }
            }
            if (existing != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existing);
            }

            // 新しい言語リソースのURIを設定します（デフォルトは日本語）
            string uriString = lang == "en" ? "/src/strings/Strings.en.xaml" : "/src/strings/Strings.ja.xaml";
            
            try
            {
                var dict = new ResourceDictionary
                {
                    Source = new Uri(uriString, UriKind.RelativeOrAbsolute)
                };
                Application.Current.Resources.MergedDictionaries.Add(dict);
                currentDictionary = dict;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load localization resource: {ex.Message}");
            }
        }
    }
}

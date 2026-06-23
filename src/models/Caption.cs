using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator.models
{
    public class Caption : INotifyPropertyChanged
    {
        public const int MAX_CONTEXTS = 100;

        private static Caption? instance = null;
        public event PropertyChangedEventHandler? PropertyChanged;

        private string displayOriginalCaption = string.Empty;
        private string displayTranslatedCaption = string.Empty;


        public string OriginalCaption { get; set; } = string.Empty;
        public string TranslatedCaption { get; set; } = string.Empty;

        public List<TranslationHistoryEntry> Contexts { get; } = new(MAX_CONTEXTS);

        public IEnumerable<TranslationHistoryEntry> AwareContexts => GetPreviousContexts(Translator.Setting.NumContexts);
        public string AwareContextsCaption => GetPreviousText(Translator.Setting.NumContexts, TextType.Caption);

        public IEnumerable<TranslationHistoryEntry> DisplayLogCards
        {
            get
            {
                var list = GetCompletedContexts(MAX_CONTEXTS).ToList();

                string activeSource = displayOriginalCaption?.Trim() ?? string.Empty;
                string activeTranslated = displayTranslatedCaption?.Trim() ?? string.Empty;

                if (!string.IsNullOrEmpty(activeSource) || !string.IsNullOrEmpty(activeTranslated))
                {
                    bool isDuplicate = false;
                    if (list.Count > 0)
                    {
                        var lastEntry = list[^1];
                        bool sourceMatches = string.Equals(lastEntry.SourceText?.Trim(), activeSource, StringComparison.Ordinal);
                        bool translatedMatches = string.Equals(lastEntry.TranslatedText?.Trim(), activeTranslated, StringComparison.Ordinal);

                        if (sourceMatches && (translatedMatches || string.IsNullOrEmpty(activeTranslated) || string.Equals(lastEntry.TranslatedText?.Trim(), "N/A", StringComparison.Ordinal)))
                        {
                            isDuplicate = true;
                        }
                    }

                    if (!isDuplicate)
                    {
                        list.Add(new TranslationHistoryEntry
                        {
                            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                            TimestampFull = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            SourceText = displayOriginalCaption,
                            TranslatedText = displayTranslatedCaption,
                            TargetLanguage = Translator.Setting?.TargetLanguage ?? "N/A",
                            ApiUsed = Translator.Setting?.ApiName ?? "N/A"
                        });
                    }
                }

                return list;
            }
        }

        public string DisplayOriginalCaption
        {
            get => displayOriginalCaption;
            set
            {
                displayOriginalCaption = value;
                OnPropertyChanged("DisplayOriginalCaption");
                OnPropertyChanged("DisplayLogCards");
            }
        }
        public string DisplayTranslatedCaption
        {
            get => displayTranslatedCaption;
            set
            {
                displayTranslatedCaption = value;
                OnPropertyChanged("DisplayTranslatedCaption");
                OnPropertyChanged("DisplayLogCards");
            }
        }



        private Caption()
        {
        }

        public static Caption GetInstance()
        {
            if (instance != null)
                return instance;
            instance = new Caption();
            return instance;
        }

        public string GetPreviousText(int count, TextType textType)
        {
            if (count <= 0 || Contexts.Count <= 1)
                return string.Empty;

            var prev = Contexts
                .SkipLast(1)
                .Skip(Math.Max(0, Contexts.Count - 1 - count))
                .Select(entry => entry == null || string.CompareOrdinal(entry.TranslatedText, "N/A") == 0 ||
                                 entry.TranslatedText.Contains("[ERROR]") || entry.TranslatedText.Contains("[WARNING]") ?
                    "" : (textType == TextType.Caption ? entry.SourceText : entry.TranslatedText))
                .Aggregate((accu, cur) =>
                {
                    if (!string.IsNullOrEmpty(accu))
                    {
                        if (Array.IndexOf(TextUtil.PUNC_EOS, accu[^1]) == -1)
                            accu += TextUtil.isCJChar(accu[^1]) ? "。" : ". ";
                        else
                            accu += TextUtil.isCJChar(accu[^1]) ? "" : " ";
                    }
                    cur = RegexPatterns.NoticePrefix().Replace(cur, "");
                    return accu + cur;
                });

            if (textType == TextType.Translation)
                prev = RegexPatterns.NoticePrefix().Replace(prev, "");
            if (!string.IsNullOrEmpty(prev) && Array.IndexOf(TextUtil.PUNC_EOS, prev[^1]) == -1)
                prev += TextUtil.isCJChar(prev[^1]) ? "。" : ".";
            if (!string.IsNullOrEmpty(prev) && Encoding.UTF8.GetByteCount(prev[^1].ToString()) < 2)
                prev += " ";
            return prev;
        }

        public IEnumerable<TranslationHistoryEntry> GetPreviousContexts(int count)
        {
            if (count <= 0 || Contexts.Count <= 1)
                return [];

            return Contexts
                .SkipLast(1)
                .Skip(Math.Max(0, Contexts.Count - 1 - count))
                .Where(entry => entry != null && string.CompareOrdinal(entry.TranslatedText, "N/A") != 0 &&
                                !entry.TranslatedText.Contains("[ERROR]") &&
                                !entry.TranslatedText.Contains("[WARNING]"));
        }

        public IEnumerable<TranslationHistoryEntry> GetCompletedContexts(int count)
        {
            if (count <= 0 || Contexts.Count == 0)
                return [];

            return Contexts
                .Skip(Math.Max(0, Contexts.Count - count))
                .Where(entry => entry != null && string.CompareOrdinal(entry.TranslatedText, "N/A") != 0 &&
                                !entry.TranslatedText.Contains("[ERROR]") &&
                                !entry.TranslatedText.Contains("[WARNING]"));
        }

        public void OnPropertyChanged([CallerMemberName] string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    public enum TextType
    {
        Caption,
        Translation
    }
}
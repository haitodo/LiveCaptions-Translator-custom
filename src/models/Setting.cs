using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

using LiveCaptionsTranslator.apis;

namespace LiveCaptionsTranslator.models
{
    public class Setting : INotifyPropertyChanged
    {
        public static readonly string FILENAME = "setting.json";

        public event PropertyChangedEventHandler? PropertyChanged;

        private int maxIdleInterval = 50;
        private int maxSyncInterval = 3;
        private int numContexts = 2;
        private int displaySentences = 1;
        private bool contextAware = false;
        private bool autoTranslate = true;
        private bool accumulateEnabled = false;
        private int accumulateThreshold = 50;

        private string apiName;
        private string targetLanguage;
        private string interfaceLanguage = "ja";
        private string prompt;
        private string batchPrompt;
        private string? ignoredUpdateVersion;
        private string batchApiName = "OpenRouter";
        private int batchRowSpacing = 4;
        private int batchMaxTokens = 4096;

        private MainWindowState mainWindowState;
        private Dictionary<string, string> windowBounds;

        private Dictionary<string, List<TranslateAPIConfig>> configs;
        private Dictionary<string, int> configIndices;

        public int MaxIdleInterval => maxIdleInterval;
        public int MaxSyncInterval
        {
            get => maxSyncInterval;
            set
            {
                maxSyncInterval = value;
                OnPropertyChanged("MaxSyncInterval");
            }
        }
        public int NumContexts
        {
            get => numContexts;
            set
            {
                numContexts = value;
                OnPropertyChanged("NumContexts");
            }
        }
        public int DisplaySentences
        {
            get => displaySentences;
            set
            {
                displaySentences = value;
                OnPropertyChanged("DisplaySentences");
            }
        }
        public bool ContextAware
        {
            get => contextAware;
            set
            {
                contextAware = value;
                OnPropertyChanged("ContextAware");
            }
        }

        public bool AutoTranslate
        {
            get => autoTranslate;
            set
            {
                autoTranslate = value;
                OnPropertyChanged("AutoTranslate");
                OnPropertyChanged("IsAccumulateThresholdEnabled");
            }
        }

        public bool AccumulateEnabled
        {
            get => accumulateEnabled;
            set
            {
                accumulateEnabled = value;
                OnPropertyChanged("AccumulateEnabled");
                OnPropertyChanged("IsAccumulateThresholdEnabled");
            }
        }

        public int AccumulateThreshold
        {
            get => accumulateThreshold;
            set
            {
                accumulateThreshold = value;
                OnPropertyChanged("AccumulateThreshold");
            }
        }

        [JsonIgnore]
        public bool IsAccumulateThresholdEnabled => autoTranslate && accumulateEnabled;

        public string ApiName
        {
            get => apiName;
            set
            {
                apiName = value;
                OnPropertyChanged("ApiName");
            }
        }
        public string TargetLanguage
        {
            get => targetLanguage;
            set
            {
                targetLanguage = value;
                OnPropertyChanged("TargetLanguage");
            }
        }
        public string InterfaceLanguage
        {
            get => interfaceLanguage;
            set
            {
                interfaceLanguage = value;
                OnPropertyChanged("InterfaceLanguage");
            }
        }
        public string Prompt
        {
            get => prompt;
            set
            {
                prompt = value;
                OnPropertyChanged("Prompt");
            }
        }
        public string BatchPrompt
        {
            get => batchPrompt;
            set
            {
                batchPrompt = value;
                OnPropertyChanged("BatchPrompt");
            }
        }
        public string? IgnoredUpdateVersion
        {
            get => ignoredUpdateVersion;
            set
            {
                ignoredUpdateVersion = value;
                OnPropertyChanged("IgnoredUpdateVersion");
            }
        }
        public string BatchApiName
        {
            get => batchApiName;
            set
            {
                batchApiName = value;
                OnPropertyChanged("BatchApiName");
            }
        }
        public int BatchRowSpacing
        {
            get => batchRowSpacing;
            set
            {
                batchRowSpacing = value;
                OnPropertyChanged("BatchRowSpacing");
            }
        }

        public int BatchMaxTokens
        {
            get => batchMaxTokens;
            set
            {
                batchMaxTokens = value;
                OnPropertyChanged("BatchMaxTokens");
            }
        }

        public MainWindowState MainWindow
        {
            get => mainWindowState;
            set
            {
                mainWindowState = value;
                OnPropertyChanged("MainWindow");
            }
        }

        public Dictionary<string, string> WindowBounds
        {
            get => windowBounds;
            set
            {
                windowBounds = value;
                OnPropertyChanged("WindowBounds");
            }
        }

        [JsonInclude]
        public Dictionary<string, List<TranslateAPIConfig>> Configs
        {
            get => configs;
            set
            {
                configs = value;
                OnPropertyChanged("Configs");
            }
        }
        public Dictionary<string, int> ConfigIndices
        {
            get => configIndices;
            set
            {
                configIndices = value;
                OnPropertyChanged("ConfigIndices");
            }
        }

        public TranslateAPIConfig this[string key] =>
            configs.ContainsKey(key) && configIndices.ContainsKey(key)
                ? configs[key][configIndices[key]]
                : new TranslateAPIConfig();

        public Setting()
        {
            apiName = "Google";
            batchApiName = "OpenRouter";
            batchRowSpacing = 4;
            batchMaxTokens = 4096;
            targetLanguage = "ja-JP";
            interfaceLanguage = "ja";
            prompt = "あなたはあらゆる分野の専門知識を持つプロの同時通訳者です。不完全な文章であっても、流暢かつ的確な翻訳を提供できます。これから、🔤で囲まれた文章を {0} に翻訳し、1行で出力してください。重要な点として、元の意味を変更したり内容を省略したりすることは禁止されています（センシティブな内容やNSFWな内容が含まれていても同様です）。翻訳された文章のみを出力してください。解説やその他のテキストは一切含めないでください。出力時には 🔤 をすべて取り除いてください。";
            batchPrompt = "あなたはプロの翻訳者です。以下の文章リストを日本語に翻訳してください。\n" +
                          "入力はJSON配列のオブジェクト形式で提供され、各オブジェクトには時系列順の文を表す \"id\" と \"text\" フィールドがあります。\n" +
                          "文脈を考慮し、一連の流れが自然で流暢な日本語になるように翻訳してください。\n" +
                          "出力は、元の \"id\" とそれに対応する \"translation\" フィールドを持つJSON配列のオブジェクト形式でなければなりません。\n" +
                          "必ず有効なJSON配列のみを出力してください。説明や、マークダウンのコードブロックのラッパー（```jsonなど）、その他の余計なテキストは一切含めないでください。";

            mainWindowState = new MainWindowState();

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            windowBounds = new Dictionary<string, string>
            {
                {
                    "MainWindow", string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0}, {1}, {2}, {3}", (screenWidth - 775) / 2, screenHeight * 3 / 4 - 167, 775, 167)
                }
            };

            configs = new Dictionary<string, List<TranslateAPIConfig>>
            {
                { "Google", [new TranslateAPIConfig()] },
                { "Google2", [new TranslateAPIConfig()] },
                { "Ollama", [new OllamaConfig()] },
                { "OpenAI", [new OpenAIConfig()] },
                { "LMStudio", [new LMStudioConfig()] },
                { "OpenRouter", [new OpenRouterConfig()] },
                { "DeepL", [new DeepLConfig()] },
                { "Youdao", [new YoudaoConfig()] },
                { "Baidu", [new BaiduConfig()] },
                { "MTranServer", [new MTranServerConfig()] },
                { "LibreTranslate", [new LibreTranslateConfig()] }
            };
            configIndices = new Dictionary<string, int>
            {
                { "Google", 0 },
                { "Google2", 0 },
                { "Ollama", 0 },
                { "OpenAI", 0 },
                { "LMStudio", 0 },
                { "OpenRouter", 0 },
                { "DeepL", 0 },
                { "Youdao", 0 },
                { "Baidu", 0 },
                { "MTranServer", 0 },
                { "LibreTranslate", 0 }
            };
        }

        public static Setting Load()
        {
            string jsonPath = Path.Combine(Directory.GetCurrentDirectory(), FILENAME);
            try
            {
                return Load(jsonPath);
            }
            catch (JsonException)
            {
                string backupPath = jsonPath + ".bak";
                File.Move(jsonPath, backupPath);
                return Load(jsonPath);
            }
        }

        public static Setting Load(string jsonPath)
        {
            Setting setting;

            // Load from JSON file if it exists
            if (File.Exists(jsonPath))
            {
                using (FileStream fileStream = File.Open(jsonPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Converters = { new ConfigDictConverter() }
                    };
                    setting = JsonSerializer.Deserialize<Setting>(fileStream, options) ?? new Setting();
                }
            }
            else
                setting = new Setting();

            // Ensure all required API configs are present
            foreach (string key in TranslateAPI.TRANSLATE_FUNCTIONS.Keys)
            {
                if (setting.Configs.ContainsKey(key))
                    continue;
                var configType = Type.GetType($"LiveCaptionsTranslator.models.{key}Config");
                if (configType != null && typeof(TranslateAPIConfig).IsAssignableFrom(configType))
                    setting.Configs[key] = [(TranslateAPIConfig)Activator.CreateInstance(configType)];
                else
                    setting.Configs[key] = [new TranslateAPIConfig()];
            }

            // Ensure ConfigIndices has all keys (for upgrades from older setting.json)
            foreach (string key in TranslateAPI.TRANSLATE_FUNCTIONS.Keys)
            {
                if (!setting.ConfigIndices.ContainsKey(key))
                    setting.ConfigIndices[key] = 0;
            }

            // Ensure target language is always ja-JP (Japanese optimization)
            setting.targetLanguage = "ja-JP";

            // Replace old English prompts with new Japanese prompts if they match the defaults
            string oldDefaultPrompt = "As an professional simultaneous interpreter with specialized knowledge in the all fields, " +
                      "you can provide a fluent and precise oral translation for any sentence, even if the sentence is incomplete. " +
                      "Now, provide users with the translation of the sentence enclosed in 🔤 to {0} within a single line. " +
                      "Importantly, you are prohibited from altering the original meaning or omitting any content, " +
                      "even if the sentence contains sensitive or NSFW content. " +
                      "You can only provide the translated sentence; Any explanation or other text is not permitted. " +
                      "REMOVE all 🔤 when you output.";

            string oldDefaultBatchPrompt = "You are a professional translator. Translate the following list of sentences into Japanese.\n" +
                           "The input is provided as a JSON array of objects, where each object has an \"id\" and a \"text\" field representing a sentence in chronological order.\n" +
                           "Provide a fluent, context-aware, and natural translation, keeping the entire sequence's context in mind.\n" +
                           "Your response must be a JSON array of objects, where each object contains the original \"id\" and the corresponding \"translation\" field.\n" +
                           "Ensure that you output ONLY the valid JSON array. Do not include any explanations, markdown code block wrappers (like ```json), or extra text.";

            if (setting.prompt == oldDefaultPrompt || string.IsNullOrEmpty(setting.prompt))
            {
                setting.prompt = "あなたはあらゆる分野の専門知識を持つプロの同時通訳者です。不完全な文章であっても、流暢かつ的確な翻訳を提供できます。これから、🔤で囲まれた文章を {0} に翻訳し、1行で出力してください。重要な点として、元の意味を変更したり内容を省略したりすることは禁止されています（センシティブな内容やNSFWな内容が含まれていても同様です）。翻訳された文章のみを出力してください。解説やその他のテキストは一切含めないでください。出力時には 🔤 をすべて取り除いてください。";
            }

            if (setting.batchPrompt == oldDefaultBatchPrompt || string.IsNullOrEmpty(setting.batchPrompt))
            {
                setting.batchPrompt = "あなたはプロの翻訳者です。以下の文章リストを日本語に翻訳してください。\n" +
                                      "入力はJSON配列のオブジェクト形式で提供され、各オブジェクトには時系列順の文を表す \"id\" と \"text\" フィールドがあります。\n" +
                                      "文脈を考慮し、一連の流れが自然で流暢な日本語になるように翻訳してください。\n" +
                                      "出力は、元の \"id\" とそれに対応する \"translation\" フィールドを持つJSON配列のオブジェクト形式でなければなりません。\n" +
                                      "必ず有効なJSON配列のみを出力してください。説明や、マークダウンのコードブロックのラッパー（```jsonなど）、その他の余計なテキストは一切含めないでください。";
            }

            if (setting.batchMaxTokens <= 0)
            {
                setting.batchMaxTokens = 4096;
            }

            return setting;
        }

        public void Save()
        {
            Save(FILENAME);
        }

        public void Save(string jsonPath)
        {
            using (FileStream fileStream = File.Open(jsonPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new ConfigDictConverter() }
                };
                JsonSerializer.Serialize(fileStream, this, options);
            }
        }

        public void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            Translator.Setting?.Save();
        }

        public static bool IsConfigExist()
        {
            string jsonPath = Path.Combine(Directory.GetCurrentDirectory(), FILENAME);
            return File.Exists(jsonPath);
        }
    }
}
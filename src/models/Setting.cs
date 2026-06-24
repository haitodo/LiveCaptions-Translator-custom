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

        // ▼ 追加: デフォルトプロンプトの定数定義
        public static readonly string DEFAULT_PROMPT =
            "あなたはあらゆる分野の専門知識を持つプロの同時通訳者です。音声認識による不完全な文章であっても、話者の感情やカジュアルさ、口調などのニュアンスをそのまま日本語に翻訳し、1行で出力してください。重要な点として、元の意味を変更したり内容を省略したりすることは禁止されています（センシティブな内容やNSFWな内容が含まれていても同様です）。翻訳された文章のみを出力してください。解説やその他のテキストは一切含めないでください。出力時には 🔤 をすべて取り除いてください。";

        public static readonly string DEFAULT_BATCH_PROMPT =
            "あなたは映画や動画、フリートークのニュアンスを完璧に捉えるプロの翻訳者です。以下の音声認識（文字起こし）による文章リストを、自然な日本語に翻訳してください。\n\n" +
            "【翻訳ルール】\n" +
            "1. 堅苦しい直訳は避け、話者の「言い方」「感情」「ニュアンス」「カジュアルさ」をできるだけそのまま生かした、自然な日本語（話し言葉・会話表現）に翻訳してください。過度に丁寧な表現（不自然な「です・ます調」）にする必要はありません。\n" +
            "2. 文字起こし特有の言い淀み（\"um\", \"ah\", \"like\" など）や、直後の言い直しによる重複は、日本語として最も自然に聞こえるようにうまく補完・省略して翻訳してください。\n" +
            "3. 入力はJSON配列のオブジェクト形式で提供され、各オブジェクトには時系列順の文を表す \"id\" と \"text\" フィールドがあります。\n" +
            "4. 出力は必ず \"translations\" というキーを持つJSONオブジェクトとし、その値として元の \"id\" と \"translation\" フィールドを持つ配列を含めてください。\n" +
            "5. 余計な説明、挨拶、マークダウンのコードブロック（```json など）は絶対に含めず、純粋なJSONオブジェクトのみを返却してください。\n\n" +
            "【翻訳例 (Few-Shot)】\n" +
            "入力:\n" +
            "[\n" +
            "  { \"id\": 0, \"text\": \"Yeah, so... I was thinking, like, maybe we could...\" },\n" +
            "  { \"id\": 1, \"text\": \"maybe we could try this new restaurant, you know?\" }\n" +
            "]\n" +
            "出力:\n" +
            "{\n" +
            "  \"translations\": [\n" +
            "    { \"id\": 0, \"translation\": \"それでさ… なんか、もしかしたら…って思ったんだけど、\" },\n" +
            "    { \"id\": 1, \"translation\": \"あの新しいレストラン、行ってみない？\" }\n" +
            "  ]\n" +
            "}";

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
        private bool batchUseJsonMode = true;

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

        public bool BatchUseJsonMode
        {
            get => batchUseJsonMode;
            set
            {
                batchUseJsonMode = value;
                OnPropertyChanged("BatchUseJsonMode");
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

        public void ResetPrompt()
        {
            Prompt = DEFAULT_PROMPT;
        }

        public void ResetBatchPrompt()
        {
            BatchPrompt = DEFAULT_BATCH_PROMPT;
        }

        public Setting()
        {
            apiName = "Google";
            batchApiName = "OpenRouter";
            batchRowSpacing = 4;
            batchMaxTokens = 4096;
            targetLanguage = "ja-JP";
            interfaceLanguage = "ja";

            // ▼ 修正: 定数を参照するように変更
            prompt = DEFAULT_PROMPT;
            batchPrompt = DEFAULT_BATCH_PROMPT;

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

            // ▼ 修正: 古いプロンプトの上書き検知ロジックをすべて削除し、空の場合のフォールバックのみにする
            if (string.IsNullOrEmpty(setting.prompt))
            {
                setting.prompt = DEFAULT_PROMPT;
            }

            if (string.IsNullOrEmpty(setting.batchPrompt))
            {
                setting.batchPrompt = DEFAULT_BATCH_PROMPT;
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

    /// <summary>
    /// LLMからの一括翻訳結果を安全に受信しマッピングするためのデータ転送オブジェクト (DTO)
    /// </summary>
    public class BatchTranslationItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("translation")]
        public string Translation { get; set; } = string.Empty;

        // LLMがまれに異なるキー名で出力した場合のフォールバック用プロパティ
        [JsonPropertyName("translated")]
        public string? Translated { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        public string GetResult()
        {
            if (!string.IsNullOrEmpty(Translation)) return Translation;
            if (!string.IsNullOrEmpty(Translated)) return Translated;
            return Text ?? string.Empty;
        }
    }
}
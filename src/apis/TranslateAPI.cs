using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator.apis
{
    public static class TranslateAPI
    {
        /*
         * The key of this field is used as the content for `translateAPIBox` in the `SettingPage`.
         * If you'd like to add a new API, please insert the key-value pair here.
         */
        public static readonly Dictionary<string, Func<string, CancellationToken, Task<string>>>
            TRANSLATE_FUNCTIONS = new()
        {
            { "Google", Google },
            { "Google2", Google2 },
            { "Ollama", Ollama },
            { "OpenAI", OpenAI },
            { "LMStudio", LMStudio },
            { "DeepL", DeepL },
            { "OpenRouter", OpenRouter },
            { "Youdao", Youdao },
            { "MTranServer", MTranServer },
            { "Baidu", Baidu },
            { "LibreTranslate", LibreTranslate },
        };
        public static readonly List<string> LLM_BASED_APIS = new()
        {
            "Ollama", "OpenAI", "OpenRouter", "LMStudio"
        };
        public static readonly List<string> BATCH_TRANSLATE_APIS = new()
        {
            "Google", "Google2", "Ollama", "OpenAI", "OpenRouter", "LMStudio"
        };
        public static readonly List<string> NO_CONFIG_APIS = new()
        {
            "Google", "Google2"
        };

        public static Func<string, CancellationToken, Task<string>> TranslateFunction =>
            TRANSLATE_FUNCTIONS[Translator.Setting.ApiName];
        public static bool IsLLMBased => LLM_BASED_APIS.Contains(Translator.Setting.ApiName);
        public static string Prompt => Translator.Setting.Prompt;

        private static readonly HttpClient client = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(100)
        };
        private static int openai_fallback_index = 0;

        public static async Task<string> OpenAI(string text, CancellationToken token = default)
        {
            var config = Translator.Setting["OpenAI"] as OpenAIConfig;
            string language = OpenAIConfig.SupportedLanguages.TryGetValue(
                Translator.Setting.TargetLanguage, out var langValue) ? langValue : Translator.Setting.TargetLanguage;

            var messages = new List<BaseLLMConfig.Message>
            {
                new BaseLLMConfig.Message { role = "system", content = string.Format(Prompt, language) },
                new BaseLLMConfig.Message { role = "user", content = $"🔤 {text} 🔤" }
            };

            if (Translator.Setting.ContextAware)
            {
                foreach (var entry in Translator.Caption.AwareContexts)
                {
                    string translatedText = entry.TranslatedText;
                    if (translatedText.Contains("[ERROR]") || translatedText.Contains("[WARNING]"))
                        continue;
                    translatedText = RegexPatterns.NoticePrefix().Replace(translatedText, "");

                    messages.InsertRange(1, [
                        new BaseLLMConfig.Message { role = "user", content = $"🔤 {entry.SourceText} 🔤" },
                        new BaseLLMConfig.Message { role = "assistant", content = $"{translatedText}" }
                    ]);
                }
            }

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

            HttpResponseMessage response;
            try
            {
                while (true)
                {
                    var requestData = LLMRequestDataFactory.Create(openai_fallback_index,
                        config.ModelName, messages, config.Temperature);
                    string jsonContent = JsonSerializer.Serialize(requestData, requestData.GetType());
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    response = await client.PostAsync(TextUtil.NormalizeUrl(config.ApiUrl), content, token);
                    if (response.StatusCode != HttpStatusCode.BadRequest &&
                        response.StatusCode != HttpStatusCode.UnprocessableEntity)
                        break;
                    Thread.Sleep(15);

                    openai_fallback_index++;
                    if (openai_fallback_index >= LLMRequestDataFactory.FallbackCount)
                    {
                        openai_fallback_index = 0;
                        break;
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                if (ex.Message.StartsWith("The request"))
                    return $"[ERROR] Translation Failed: The request was canceled due to timeout (> 100 seconds), " +
                           $"please use a faster API or check network connection.";
                throw;
            }
            catch (Exception ex)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<OpenAIConfig.Response>(responseString);
                var output = responseObj.choices[0].message.content;
                return RegexPatterns.ModelThinking().Replace(output, "");
            }
            else
                return $"[ERROR] Translation Failed: HTTP Error - {response.StatusCode}";
        }

        public static async Task<string> Ollama(string text, CancellationToken token = default)
        {
            var config = Translator.Setting["Ollama"] as OllamaConfig;
            string language = OllamaConfig.SupportedLanguages.TryGetValue(
                Translator.Setting.TargetLanguage, out var langValue) ? langValue : Translator.Setting.TargetLanguage;
            string apiUrl = TextUtil.NormalizeUrl(config.ApiUrl + "/api/chat");

            var messages = new List<BaseLLMConfig.Message>
            {
                new BaseLLMConfig.Message { role = "system", content = string.Format(Prompt, language) },
                new BaseLLMConfig.Message { role = "user", content = $"🔤 {text} 🔤" }
            };

            if (Translator.Setting.ContextAware)
            {
                foreach (var entry in Translator.Caption.AwareContexts)
                {
                    string translatedText = entry.TranslatedText;
                    if (translatedText.Contains("[ERROR]") || translatedText.Contains("[WARNING]"))
                        continue;
                    translatedText = RegexPatterns.NoticePrefix().Replace(translatedText, "");

                    messages.InsertRange(1, [
                        new BaseLLMConfig.Message { role = "user", content = $"🔤 {entry.SourceText} 🔤" },
                        new BaseLLMConfig.Message { role = "assistant", content = $"{translatedText}" }
                    ]);
                }
            }

            var requestData = LLMRequestDataFactory.Create("Ollama", config.ModelName, messages, config.Temperature);
            requestData.keep_alive = config.keep_alive;
            string jsonContent = JsonSerializer.Serialize(requestData, requestData.GetType());
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Clear();

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(apiUrl, content, token);
            }
            catch (OperationCanceledException ex)
            {
                if (ex.Message.StartsWith("The request"))
                    return $"[ERROR] Translation Failed: The request was canceled due to timeout (> 100 seconds), " +
                           $"please use a faster API or check network connection.";
                throw;
            }
            catch (Exception ex)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<OllamaConfig.Response>(responseString);
                var output = responseObj.message.content;
                return RegexPatterns.ModelThinking().Replace(output, "");
            }
            else
                return $"[ERROR] Translation Failed: HTTP Error - {response.StatusCode}";
        }

        public static async Task<string> LMStudio(string text, CancellationToken token = default)
        {
            var config = Translator.Setting["LMStudio"] as LMStudioConfig;
            string language = LMStudioConfig.SupportedLanguages.TryGetValue(
                Translator.Setting.TargetLanguage, out var langValue) ? langValue : Translator.Setting.TargetLanguage;
            string apiUrl = TextUtil.NormalizeUrl(config.ApiUrl) + "/chat";

            string systemPrompt = string.Format(Prompt, language);

            // Build input with optional context
            string input = $"🔤 {text} 🔤";
            if (Translator.Setting.ContextAware)
            {
                var contextLines = new List<string>();
                foreach (var entry in Translator.Caption.AwareContexts)
                {
                    string translatedText = entry.TranslatedText;
                    if (translatedText.Contains("[ERROR]") || translatedText.Contains("[WARNING]"))
                        continue;
                    translatedText = RegexPatterns.NoticePrefix().Replace(translatedText, "");
                    contextLines.Add($"🔤 {entry.SourceText} 🔤 → {translatedText}");
                }
                if (contextLines.Count > 0)
                    input = string.Join("\n", contextLines) + "\n" + input;
            }

            var requestData = new
            {
                model = config.ModelName,
                system_prompt = systemPrompt,
                input = input,
                temperature = config.Temperature
            };

            string jsonContent = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Clear();

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(apiUrl, content, token);
            }
            catch (OperationCanceledException ex)
            {
                if (ex.Message.StartsWith("The request"))
                    return $"[ERROR] Translation Failed: The request was canceled due to timeout (> 100 seconds), " +
                           $"please use a faster API or check network connection.";
                throw;
            }
            catch (Exception ex)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                // LMStudio native /api/v1/chat response:
                // { "output": [ { "type": "message", "content": "..." }, ... ] }
                if (root.TryGetProperty("output", out var outputArray) &&
                    outputArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in outputArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("type", out var typeProp) &&
                            typeProp.GetString() == "message" &&
                            item.TryGetProperty("content", out var contentProp))
                        {
                            return RegexPatterns.ModelThinking().Replace(contentProp.GetString() ?? "", "");
                        }
                    }
                }

                return "[ERROR] Translation Failed: Unexpected response format";
            }
            else
            {
                string body = await response.Content.ReadAsStringAsync();
                return $"[ERROR] Translation Failed: HTTP Error - {response.StatusCode}: {body}";
            }
        }

        public static async Task<string> OpenRouter(string text, CancellationToken token = default)
        {
            var config = Translator.Setting["OpenRouter"] as OpenRouterConfig;
            string language = OpenRouterConfig.SupportedLanguages.TryGetValue(
                Translator.Setting.TargetLanguage, out var langValue) ? langValue : Translator.Setting.TargetLanguage;

            // ▼ 修正: 固定文字列から config.ApiUrl をベースに構築する形に変更
            string apiUrl = TextUtil.NormalizeUrl(config.ApiUrl) + "/chat/completions";

            var messages = new List<BaseLLMConfig.Message>
            {
                new BaseLLMConfig.Message { role = "system", content = string.Format(Prompt, language) },
                new BaseLLMConfig.Message { role = "user", content = $"🔤 {text} 🔤" }
            };

            if (Translator.Setting.ContextAware)
            {
                foreach (var entry in Translator.Caption.AwareContexts)
                {
                    string translatedText = entry.TranslatedText;
                    if (translatedText.Contains("[ERROR]") || translatedText.Contains("[WARNING]"))
                        continue;
                    translatedText = RegexPatterns.NoticePrefix().Replace(translatedText, "");

                    messages.InsertRange(1, [
                        new BaseLLMConfig.Message { role = "user", content = $"🔤 {entry.SourceText} 🔤" },
                        new BaseLLMConfig.Message { role = "assistant", content = $"{translatedText}" }
                    ]);
                }
            }

            // （以下、既存のコードがそのまま続きます）
            var requestData = LLMRequestDataFactory.Create("OpenRouter", config.ModelName, messages, config.Temperature);

            string jsonContent = JsonSerializer.Serialize(requestData, requestData.GetType());
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config?.ApiKey}");

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(apiUrl, content, token);
            }
            catch (OperationCanceledException ex)
            {
                if (ex.Message.StartsWith("The request"))
                    return $"[ERROR] Translation Failed: The request was canceled due to timeout (> 100 seconds), " +
                           $"please use a faster API or check network connection.";
                throw;
            }
            catch (Exception ex)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var output = jsonResponse.GetProperty("choices")[0]
                                         .GetProperty("message")
                                         .GetProperty("content")
                                         .GetString() ?? string.Empty;
                return RegexPatterns.ModelThinking().Replace(output, "");
            }
            else
                return $"[ERROR] Translation Failed: HTTP Error - {response.StatusCode}";
        }

        public static async Task<string> Google(string text, CancellationToken token = default)
        {
            var language = Translator.Setting?.TargetLanguage;

            string encodedText = Uri.EscapeDataString(text);
            var url = $"https://clients5.google.com/translate_a/t?" +
                      $"client=dict-chrome-ex&sl=auto&" +
                      $"tl={language}&" +
                      $"q={encodedText}";

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(url, token);
            }
            catch (OperationCanceledException ex)
            {
                if (ex.Message.StartsWith("The request"))
                    return $"[ERROR] Translation Failed: The request was canceled due to timeout (> 100 seconds), " +
                           $"please use a faster API or check network connection.";
                throw;
            }
            catch (Exception ex)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();

                var responseObj = JsonSerializer.Deserialize<List<List<string>>>(responseString);

                string translatedText = responseObj[0][0];
                return translatedText;
            }
            else
                return $"[ERROR] Translation Failed: HTTP Error - {response.StatusCode}";
        }

        public static async Task<string> Google2(string text, CancellationToken token = default)
        {
            string apiKey = "AIzaSyA6EEtrDCfBkHV8uU2lgGY-N383ZgAOo7Y";
            var language = Translator.Setting?.TargetLanguage;
            string strategy = "2";

            string encodedText = Uri.EscapeDataString(text);
            string url = $"https://dictionaryextension-pa.googleapis.com/v1/dictionaryExtensionData?" +
                         $"language={language}&" +
                         $"key={apiKey}&" +
                         $"term={encodedText}&" +
                         $"strategy={strategy}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-referer", "chrome-extension://mgijmajocgfcbeboacabfgobmjgjcoja");

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, token);
            }
            catch (OperationCanceledException ex)
            {
                if (ex.Message.StartsWith("The request"))
                    return $"[ERROR] Translation Failed: The request was canceled due to timeout (> 100 seconds), " +
                           $"please use a faster API or check network connection.";
                throw;
            }
            catch (Exception ex)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }

            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();

                using var jsonDoc = JsonDocument.Parse(responseBody);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("translateResponse", out JsonElement translateResponse))
                {
                    string translatedText = translateResponse.GetProperty("translateText").GetString();
                    return translatedText;
                }
                else
                    return "[ERROR] Translation Failed: Unexpected API response format";
            }
            else
                return $"[ERROR] Translation Failed: HTTP Error - {response.StatusCode}";
        }

        public static async Task<string> DeepL(string text, CancellationToken token = default)
        {
            var config = Translator.Setting["DeepL"] as DeepLConfig;
            string language = DeepLConfig.SupportedLanguages.TryGetValue(
                Translator.Setting.TargetLanguage, out var langValue) ? langValue : Translator.Setting.TargetLanguage;
            string apiUrl = TextUtil.NormalizeUrl(config.ApiUrl);

            var requestData = new
            {
                text = new[] { text },
                target_lang = language
            };

            string jsonContent = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"DeepL-Auth-Key {config?.ApiKey}");

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(apiUrl, content, token);
            }
            catch (OperationCanceledException ex)
            {
                if (ex.Message.StartsWith("The request"))
                    return $"[ERROR] Translation Failed: The request was canceled due to timeout (> 100 seconds), " +
                           $"please use a faster API or check network connection.";
                throw;
            }
            catch (Exception ex)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);

                if (doc.RootElement.TryGetProperty("translations", out var translations) &&
                    translations.ValueKind == JsonValueKind.Array && translations.GetArrayLength() > 0)
                {
                    return translations[0].GetProperty("text").GetString();
                }
                return "[ERROR] Translation Failed: No valid feedback";
            }
            else
                return $"[ERROR] Translation Failed: HTTP Error - {response.StatusCode}";
        }


        public static async Task<string> Youdao(string text, CancellationToken token = default)
        {
            var config = Translator.Setting["Youdao"] as YoudaoConfig;
            string language = YoudaoConfig.SupportedLanguages.TryGetValue(
                Translator.Setting.TargetLanguage, out var langValue) ? langValue : Translator.Setting.TargetLanguage;

            string salt = DateTime.Now.Millisecond.ToString();
            string sign = BitConverter.ToString(
                MD5.Create().ComputeHash(
                    Encoding.UTF8.GetBytes($"{config.AppKey}{text}{salt}{config.AppSecret}"))).Replace("-", "").ToLower();

            var parameters = new Dictionary<string, string>
            {
                ["q"] = text,
                ["from"] = "auto",
                ["to"] = language,
                ["appKey"] = config.AppKey,
                ["salt"] = salt,
                ["sign"] = sign
            };

            var content = new FormUrlEncodedContent(parameters);
            client.DefaultRequestHeaders.Clear();

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(config.ApiUrl, content, token);
            }
            catch (OperationCanceledException ex)
            {
                if (ex.Message.StartsWith("The request"))
                    return $"[ERROR] Translation Failed: The request was canceled due to timeout (> 100 seconds), " +
                           $"please use a faster API or check network connection.";
                throw;
            }
            catch (Exception ex)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<YoudaoConfig.TranslationResult>(responseString);

                if (responseObj.errorCode != "0")
                    return $"[ERROR] Translation Failed: Youdao Error - {responseObj.errorCode}";

                return responseObj.translation?.FirstOrDefault() ?? "[ERROR] Translation Failed: No content";
            }
            else
            {
                return $"[ERROR] Translation Failed: HTTP Error - {response.StatusCode}";
            }
        }

        public static async Task<string> MTranServer(string text, CancellationToken token = default)
        {
            var config = Translator.Setting["MTranServer"] as MTranServerConfig;
            string targetLanguage = MTranServerConfig.SupportedLanguages.TryGetValue(
                Translator.Setting.TargetLanguage, out var langValue) ? langValue : Translator.Setting.TargetLanguage;
            string sourceLanguage = config.SourceLanguage;
            string apiUrl = TextUtil.NormalizeUrl(config.ApiUrl);

            var requestData = new
            {
                text = text,
                to = targetLanguage,
                from = sourceLanguage
            };

            string jsonContent = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config?.ApiKey}");

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(apiUrl, content, token);
            }
            catch (OperationCanceledException ex)
            {
                if (ex.Message.StartsWith("The request"))
                    return $"[ERROR] Translation Failed: The request was canceled due to timeout (> 100 seconds), " +
                           $"please use a faster API or check network connection.";
                throw;
            }
            catch (Exception ex)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<MTranServerConfig.Response>(responseString);
                return responseObj.result;
            }
            else
                return $"[ERROR] Translation Failed: HTTP Error - {response.StatusCode}";
        }

        public static async Task<string> Baidu(string text, CancellationToken token = default)
        {
            var config = Translator.Setting["Baidu"] as BaiduConfig;
            string language = BaiduConfig.SupportedLanguages.TryGetValue(
                Translator.Setting.TargetLanguage, out var langValue) ? langValue : Translator.Setting.TargetLanguage;

            string salt = DateTime.Now.Millisecond.ToString();
            string sign = BitConverter.ToString(
                MD5.Create().ComputeHash(
                    Encoding.UTF8.GetBytes($"{config.AppId}{text}{salt}{config.AppSecret}"))).Replace("-", "").ToLower();

            var parameters = new Dictionary<string, string>
            {
                ["q"] = text,
                ["from"] = "auto",
                ["to"] = language,
                ["appid"] = config.AppId,
                ["salt"] = salt,
                ["sign"] = sign
            };

            var content = new FormUrlEncodedContent(parameters);
            client.DefaultRequestHeaders.Clear();

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(config.ApiUrl, content, token);
            }
            catch (OperationCanceledException ex)
            {
                if (ex.Message.StartsWith("The request"))
                    return $"[ERROR] Translation Failed: The request was canceled due to timeout (> 100 seconds), " +
                           $"please use a faster API or check network connection.";
                throw;
            }
            catch (Exception ex)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<BaiduConfig.TranslationResult>(responseString);

                if (responseObj.error_code is not null && responseObj.error_code != "0")
                    return $"[ERROR] Translation Failed: Baidu Error - {responseObj.error_code}";

                return responseObj.trans_result?.FirstOrDefault()?.dst ?? "[ERROR] Translation Failed: No content";
            }
            else
            {
                return $"[ERROR] Translation Failed: HTTP Error - {response.StatusCode}";
            }
        }

        public static async Task<string> LibreTranslate(string text, CancellationToken token = default)
        {
            var config = Translator.Setting["LibreTranslate"] as LibreTranslateConfig;
            string targetLanguage = LibreTranslateConfig.SupportedLanguages.TryGetValue(
                Translator.Setting.TargetLanguage, out var langValue) ? langValue : Translator.Setting.TargetLanguage;
            string apiUrl = TextUtil.NormalizeUrl(config.ApiUrl);

            var requestData = new
            {
                q = text,
                target = targetLanguage,
                source = "auto",
                format = "text",
                api_key = config?.ApiKey
            };

            string jsonContent = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            client.DefaultRequestHeaders.Clear();

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(apiUrl, content, token);
            }
            catch (OperationCanceledException ex)
            {
                if (ex.Message.StartsWith("The request"))
                    return $"[ERROR] Translation Failed: The request was canceled due to timeout (> 100 seconds), " +
                           $"please use a faster API or check network connection.";
                throw;
            }
            catch (Exception ex)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<LibreTranslateConfig.Response>(responseString);
                return responseObj.translatedText;
            }
            else
                return $"[ERROR] Translation Failed: HTTP Error - {response.StatusCode}";
        }
        public static bool GetBatchLLMConfig(string batchApiName, out BaseLLMConfig? config)
        {
            config = null;
            if (Translator.Setting == null) return false;

            config = Translator.Setting[batchApiName] as BaseLLMConfig;
            if (config == null) return false;

            if (batchApiName == "OpenAI")
            {
                var openAi = config as OpenAIConfig;
                return openAi != null && !string.IsNullOrEmpty(openAi.ApiKey);
            }
            else if (batchApiName == "OpenRouter")
            {
                var openRouter = config as OpenRouterConfig;
                return openRouter != null && !string.IsNullOrEmpty(openRouter.ApiKey);
            }
            else if (batchApiName == "LMStudio")
            {
                var lmStudio = config as LMStudioConfig;
                return lmStudio != null && !string.IsNullOrEmpty(lmStudio.ModelName);
            }
            else if (batchApiName == "Ollama")
            {
                var ollama = config as OllamaConfig;
                return ollama != null && !string.IsNullOrEmpty(ollama.ModelName);
            }

            return false;
        }

        public static bool GetConfiguredLLM(out string apiName, out BaseLLMConfig? config)
        {
            apiName = string.Empty;
            config = null;

            if (Translator.Setting == null) return false;

            // If the current API is LLM-based, use it
            if (IsLLMBased)
            {
                apiName = Translator.Setting.ApiName;
                config = Translator.Setting[apiName] as BaseLLMConfig;
                return config != null;
            }

            // Check OpenRouter
            var openRouter = Translator.Setting["OpenRouter"] as OpenRouterConfig;
            if (openRouter != null && !string.IsNullOrEmpty(openRouter.ApiKey))
            {
                apiName = "OpenRouter";
                config = openRouter;
                return true;
            }

            // Check OpenAI
            var openAi = Translator.Setting["OpenAI"] as OpenAIConfig;
            if (openAi != null && !string.IsNullOrEmpty(openAi.ApiKey))
            {
                apiName = "OpenAI";
                config = openAi;
                return true;
            }

            // Check LMStudio
            var lmStudio = Translator.Setting["LMStudio"] as LMStudioConfig;
            if (lmStudio != null && !string.IsNullOrEmpty(lmStudio.ModelName))
            {
                apiName = "LMStudio";
                config = lmStudio;
                return true;
            }

            // Check Ollama
            var ollama = Translator.Setting["Ollama"] as OllamaConfig;
            if (ollama != null && !string.IsNullOrEmpty(ollama.ModelName))
            {
                apiName = "Ollama";
                config = ollama;
                return true;
            }

            return false;
        }

        private static string ExtractJsonArray(string output)
        {
            output = RegexPatterns.ModelThinking().Replace(output ?? "", "");

            try
            {
                // マークダウン等が含まれている場合を考慮し、JSONの { } を抽出
                int startIndex = output.IndexOf('{');
                int endIndex = output.LastIndexOf('}');
                if (startIndex != -1 && endIndex != -1 && endIndex >= startIndex)
                {
                    string jsonString = output.Substring(startIndex, endIndex - startIndex + 1);
                    using var doc = JsonDocument.Parse(jsonString);
                    if (doc.RootElement.TryGetProperty("translations", out var translations))
                    {
                        return translations.GetRawText(); // 配列部分のみを返す
                    }
                }
            }
            catch { }

            // フォールバック: 万が一直接配列が返ってきた場合
            try
            {
                int startIndex = output.IndexOf('[');
                int endIndex = output.LastIndexOf(']');
                if (startIndex != -1 && endIndex != -1 && endIndex >= startIndex)
                {
                    return output.Substring(startIndex, endIndex - startIndex + 1);
                }
            }
            catch { }

            return output; // パース失敗時はそのまま返す
        }

        public static async Task<string> TranslateBatchWithLLM(string apiName, BaseLLMConfig config, string text, string targetLanguage, CancellationToken token = default)
        {
            string language = targetLanguage;
            if (apiName == "OpenAI" && OpenAIConfig.SupportedLanguages.TryGetValue(targetLanguage, out var langValueOpenAI))
                language = langValueOpenAI;
            else if (apiName == "Ollama" && OllamaConfig.SupportedLanguages.TryGetValue(targetLanguage, out var langValueOllama))
                language = langValueOllama;
            else if (apiName == "OpenRouter" && OpenRouterConfig.SupportedLanguages.TryGetValue(targetLanguage, out var langValueOpenRouter))
                language = langValueOpenRouter;
            else if (apiName == "LMStudio" && LMStudioConfig.SupportedLanguages.TryGetValue(targetLanguage, out var langValueLMStudio))
                language = langValueLMStudio;

            string systemPrompt = Translator.Setting?.BatchPrompt;
            if (string.IsNullOrEmpty(systemPrompt))
            {
                systemPrompt = "あなたは映画や動画、フリートークのニュアンスを完璧に捉えるプロの翻訳者です。以下の音声認識（文字起こし）による文章リストを、自然な日本語に翻訳してください。\n\n" +
                               "【翻訳ルール】\n" +
                               "1. 堅苦しい直訳は避け、話者の「言い方」「感情」「ニュアンス」「カジュアルさ」をできるだけそのまま生かした、自然な日本語（話し言葉・会話表現）に翻訳してください。過度に丁寧な表現（不自然な「です・ます調」）にする必要はありません。\n" +
                               "2. 文字起こし特有の言い淀み（\"um\", \"ah\", \"like\" など）や、直後の言い直しによる重複は、日本語として最も自然に聞こえるようにうまく補完・省略して翻訳してください。\n" +
                               "3. 入力はJSON配列のオブジェクト形式で提供され、各オブジェクトには時系列順の文を表す \"id\" と \"text\" フィールドがあります。\n" +
                               "4. 出力は必ず \"translations\" というキーを持つJSONオブジェクトとし、その値として、元の \"id\" と \"translation\" フィールドを持つ配列を含めてください。\n" +
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
            }

            if (systemPrompt.Contains("{0}"))
            {
                try { systemPrompt = string.Format(systemPrompt, language); } catch { }
            }

            // OpenAI等はプロンプト内に「JSON」という単語が含まれていないとエラーを返すため保護処理を追加
            if (!systemPrompt.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                systemPrompt += "\n\n(Please output a valid JSON object containing a \"translations\" key with the array.)";
            }

            if (apiName == "OpenAI")
            {
                var openAiConfig = config as OpenAIConfig;
                var messages = new List<BaseLLMConfig.Message>
                {
                    new BaseLLMConfig.Message { role = "system", content = systemPrompt },
                    new BaseLLMConfig.Message { role = "user", content = text }
                };

                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiConfig.ApiKey}");

                var requestData = LLMRequestDataFactory.Create(0, openAiConfig.ModelName, messages, openAiConfig.Temperature);
                if (requestData != null)
                {
                    requestData.max_tokens = Translator.Setting?.BatchMaxTokens ?? 4096;

                    // ▼ ここを条件分岐に変更 ▼
                    if (Translator.Setting?.BatchUseJsonMode ?? true)
                    {
                        requestData.response_format = new { type = "json_object" }; // JSONモード
                    }
                }

                string jsonContent = JsonSerializer.Serialize(requestData, requestData.GetType());
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(TextUtil.NormalizeUrl(openAiConfig.ApiUrl), content, token);
                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    var responseObj = JsonSerializer.Deserialize<OpenAIConfig.Response>(responseString);
                    return ExtractJsonArray(responseObj.choices[0].message.content);
                }
                else
                {
                    string errBody = await response.Content.ReadAsStringAsync();
                    return $"[ERROR] HTTP Error - {response.StatusCode}: {errBody}";
                }
            }
            else if (apiName == "Ollama")
            {
                var ollamaConfig = config as OllamaConfig;
                string apiUrl = TextUtil.NormalizeUrl(ollamaConfig.ApiUrl + "/api/chat");
                var messages = new List<BaseLLMConfig.Message>
                {
                    new BaseLLMConfig.Message { role = "system", content = systemPrompt },
                    new BaseLLMConfig.Message { role = "user", content = text }
                };

                var requestData = LLMRequestDataFactory.Create("Ollama", ollamaConfig.ModelName, messages, ollamaConfig.Temperature);
                if (requestData != null)
                {
                    requestData.max_tokens = Translator.Setting?.BatchMaxTokens ?? 4096;
                    requestData.keep_alive = ollamaConfig.keep_alive;

                    // ▼ ここを条件分岐に変更 ▼
                    if (Translator.Setting?.BatchUseJsonMode ?? true)
                    {
                        if (requestData is OllamaRequestData oReq) oReq.format = "json"; // JSONモード
                    }
                }

                string jsonContent = JsonSerializer.Serialize(requestData, requestData.GetType());
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                client.DefaultRequestHeaders.Clear();

                HttpResponseMessage response = await client.PostAsync(apiUrl, content, token);
                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    var responseObj = JsonSerializer.Deserialize<OllamaConfig.Response>(responseString);
                    return ExtractJsonArray(responseObj.message.content);
                }
                else
                {
                    string errBody = await response.Content.ReadAsStringAsync();
                    return $"[ERROR] HTTP Error - {response.StatusCode}: {errBody}";
                }
            }
            else if (apiName == "OpenRouter")
            {
                var openRouterConfig = config as OpenRouterConfig;

                // ▼ 修正: 固定文字列から openRouterConfig.ApiUrl をベースに構築する形に変更
                string apiUrl = TextUtil.NormalizeUrl(openRouterConfig.ApiUrl) + "/chat/completions";

                var messages = new List<BaseLLMConfig.Message>
                {
                    new BaseLLMConfig.Message { role = "system", content = systemPrompt },
                    new BaseLLMConfig.Message { role = "user", content = text }
                };

                var requestData = LLMRequestDataFactory.Create("OpenRouter", openRouterConfig.ModelName, messages, openRouterConfig.Temperature);
                if (requestData != null)
                {
                    requestData.max_tokens = Translator.Setting?.BatchMaxTokens ?? 4096;

                    // ▼ ここを条件分岐に変更 ▼
                    if (Translator.Setting?.BatchUseJsonMode ?? true)
                    {
                        requestData.response_format = new { type = "json_object" }; // JSONモード
                    }
                }

                string jsonContent = JsonSerializer.Serialize(requestData, requestData.GetType());
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {openRouterConfig?.ApiKey}");

                HttpResponseMessage response = await client.PostAsync(apiUrl, content, token);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var output = jsonResponse.GetProperty("choices")[0]
                                             .GetProperty("message")
                                             .GetProperty("content")
                                             .GetString() ?? string.Empty;
                    return ExtractJsonArray(output);
                }
                else
                {
                    string errBody = await response.Content.ReadAsStringAsync();
                    return $"[ERROR] HTTP Error - {response.StatusCode}: {errBody}";
                }
            }
            else if (apiName == "LMStudio")
            {
                var lmStudioConfig = config as LMStudioConfig;
                string apiUrl = TextUtil.NormalizeUrl(lmStudioConfig.ApiUrl) + "/chat";

                object requestData;

                if (Translator.Setting?.BatchUseJsonMode ?? true)
                {
                    requestData = new
                    {
                        model = lmStudioConfig.ModelName,
                        system_prompt = systemPrompt,
                        input = text,
                        temperature = lmStudioConfig.Temperature,
                        max_tokens = Translator.Setting?.BatchMaxTokens ?? 4096,
                        response_format = new { type = "json_object" } // JSONモードあり
                    };
                }
                else
                {
                    requestData = new
                    {
                        model = lmStudioConfig.ModelName,
                        system_prompt = systemPrompt,
                        input = text,
                        temperature = lmStudioConfig.Temperature,
                        max_tokens = Translator.Setting?.BatchMaxTokens ?? 4096
                        // JSONモードなし
                    };
                }

                string jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                client.DefaultRequestHeaders.Clear();

                HttpResponseMessage response = await client.PostAsync(apiUrl, content, token);
                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseString);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("output", out var outputArray) &&
                        outputArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in outputArray.EnumerateArray())
                        {
                            if (item.TryGetProperty("type", out var typeProp) &&
                                typeProp.GetString() == "message" &&
                                item.TryGetProperty("content", out var contentProp))
                            {
                                return ExtractJsonArray(contentProp.GetString() ?? "");
                            }
                        }
                    }
                    return "[ERROR] Unexpected response format";
                }
                else
                {
                    string errBody = await response.Content.ReadAsStringAsync();
                    return $"[ERROR] HTTP Error - {response.StatusCode}: {errBody}";
                }
            }

            return "[ERROR] Unsupported API";
        }
    }

    public class ConfigDictConverter : JsonConverter<Dictionary<string, List<TranslateAPIConfig>>>
    {
        public override Dictionary<string, List<TranslateAPIConfig>> Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected a StartObject token.");
            var configs = new Dictionary<string, List<TranslateAPIConfig>>();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string key = reader.GetString();
                reader.Read();

                var configType = Type.GetType($"LiveCaptionsTranslator.models.{key}Config");
                TranslateAPIConfig config;

                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    var list = new List<TranslateAPIConfig>();
                    reader.Read();

                    while (reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (configType != null && typeof(TranslateAPIConfig).IsAssignableFrom(configType))
                            config = (TranslateAPIConfig)JsonSerializer.Deserialize(ref reader, configType, options);
                        else
                            config = (TranslateAPIConfig)JsonSerializer.Deserialize(ref reader, typeof(TranslateAPIConfig), options);

                        list.Add(config);
                        reader.Read();
                    }
                    configs[key] = list;
                }
                else
                    throw new JsonException("Expected a StartObject token or a StartArray token.");

                reader.Read();
            }

            if (reader.TokenType != JsonTokenType.EndObject)
                throw new JsonException("Expected an EndObject token.");
            return configs;
        }

        public override void Write(
            Utf8JsonWriter writer, Dictionary<string, List<TranslateAPIConfig>> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var kvp in value)
            {
                writer.WritePropertyName(kvp.Key);
                var configType = Type.GetType($"LiveCaptionsTranslator.models.{kvp.Key}Config");

                if (kvp.Value is IEnumerable<TranslateAPIConfig> configList)
                {
                    writer.WriteStartArray();
                    foreach (var config in configList)
                    {
                        if (configType != null && typeof(TranslateAPIConfig).IsAssignableFrom(configType))
                            JsonSerializer.Serialize(writer, config, configType, options);
                        else
                            JsonSerializer.Serialize(writer, config, typeof(TranslateAPIConfig), options);
                    }
                    writer.WriteEndArray();
                }
                else
                    throw new JsonException($"Unsupported config type: {kvp.Value.GetType()}");
            }
            writer.WriteEndObject();
        }
    }
}
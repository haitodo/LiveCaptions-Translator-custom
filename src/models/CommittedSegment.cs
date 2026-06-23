using System;

namespace LiveCaptionsTranslator.models
{
    /// <summary>
    /// 確定済みの字幕セグメントを表します。
    /// </summary>
    public class CommittedSegment
    {
        public string SourceText { get; set; } = string.Empty;
        public string TranslatedText { get; set; } = string.Empty;
        public string TargetLanguage { get; set; } = string.Empty;
        public string ApiUsed { get; set; } = string.Empty;
        public DateTime CommittedTime { get; set; }
        public bool IsPolished { get; set; }
    }
}

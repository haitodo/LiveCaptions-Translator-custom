using System;
using System.Collections.Generic;
using System.Linq;
using LiveCaptionsTranslator.models;

namespace LiveCaptionsTranslator.utils
{
    /// <summary>
    /// Live Captionsのテキストの重複・揺れ・微修正を吸収し、確定/未確定をコントロールするスタビライザー。
    /// </summary>
    public class CaptionStabilizer
    {
        private readonly List<CommittedSegment> committedSegments = new();
        private string pendingPreviewSource = string.Empty;
        private string lastRawText = string.Empty;
        private DateTime lastRawTextChangeTime = DateTime.UtcNow;
        private readonly object _lock = new object();

        public string PendingPreviewSource => pendingPreviewSource;

        public class StabilizeResult
        {
            /// <summary>
            /// 新規に確定されたセグメントのリスト
            /// </summary>
            public List<CommittedSegment> NewlyCommittedSegments { get; } = new();

            /// <summary>
            /// 既存の確定セグメントが更新・修正されたリスト（最後尾の更新）
            /// </summary>
            public List<CommittedSegment> UpdatedSegments { get; } = new();

            /// <summary>
            /// 現在の未確定プレビュー用原文
            /// </summary>
            public string PendingPreviewSource { get; set; } = string.Empty;

            /// <summary>
            /// プレビューに変化があったかどうか
            /// </summary>
            public bool PreviewChanged { get; set; }
        }

        public StabilizeResult Process(string fullText, TimeSpan idleThreshold)
        {
            lock (_lock)
            {
                var result = new StabilizeResult();

                // テキストの前処理と文分割
                var sentences = SplitIntoSentences(fullText);

                // 音声認識テキストが変化したタイムスタンプを追跡（アイドル判定用）
                if (fullText != lastRawText)
                {
                    lastRawText = fullText;
                    lastRawTextChangeTime = DateTime.UtcNow;
                }

                if (sentences.Count == 0)
                {
                    // 無音（アイドル）による強制確定チェック
                    if (!string.IsNullOrEmpty(pendingPreviewSource) && 
                        (DateTime.UtcNow - lastRawTextChangeTime) > idleThreshold)
                    {
                        var newSeg = new CommittedSegment
                        {
                            SourceText = pendingPreviewSource,
                            CommittedTime = DateTime.UtcNow
                        };
                        committedSegments.Add(newSeg);
                        result.NewlyCommittedSegments.Add(newSeg);
                        pendingPreviewSource = string.Empty;
                        result.PreviewChanged = true;
                    }
                    result.PendingPreviewSource = pendingPreviewSource;
                    return result;
                }

                // 確定履歴（committedSegments）とのアライメントを検索
                int alignedSentencesIndex = -1;
                int alignedCommittedIndex = -1;
                bool isExtension = false;

                // 確定履歴を最新のものから逆順にスキャンして一致箇所を探す
                for (int j = committedSegments.Count - 1; j >= 0; j--)
                {
                    var cs = committedSegments[j];
                    for (int i = 0; i < sentences.Count; i++)
                    {
                        if (IsMatchOrExtension(sentences[i], cs.SourceText, out isExtension))
                        {
                            alignedSentencesIndex = i;
                            alignedCommittedIndex = j;
                            break;
                        }
                    }
                    if (alignedSentencesIndex != -1)
                        break;
                }

                if (alignedSentencesIndex != -1)
                {
                    // アライメントが見つかった場合
                    var alignedCs = committedSegments[alignedCommittedIndex];

                    if (isExtension)
                    {
                        // 既存の確定文が拡張または語彙修正された
                        alignedCs.SourceText = sentences[alignedSentencesIndex].Trim();
                        alignedCs.IsPolished = false; // 再校正を促す
                        result.UpdatedSegments.Add(alignedCs);
                    }

                    // アライメント位置より後の文を処理
                    for (int i = alignedSentencesIndex + 1; i < sentences.Count; i++)
                    {
                        var s = sentences[i].Trim();
                        if (string.IsNullOrEmpty(s)) continue;

                        // 後続の確定文と一致するかチェック（アライメント順序維持）
                        bool matchesSubsequent = false;
                        for (int j = alignedCommittedIndex + 1; j < committedSegments.Count; j++)
                        {
                            if (IsMatchOrExtension(s, committedSegments[j].SourceText, out bool isExt))
                            {
                                matchesSubsequent = true;
                                if (isExt)
                                {
                                    committedSegments[j].SourceText = s;
                                    committedSegments[j].IsPolished = false;
                                    result.UpdatedSegments.Add(committedSegments[j]);
                                }
                                alignedCommittedIndex = j;
                                break;
                            }
                        }

                        if (!matchesSubsequent)
                        {
                            // 完全に新しいセンテンス
                            if (i == sentences.Count - 1)
                            {
                                // 末尾の文の場合
                                if (EndsWithEOS(s))
                                {
                                    var newSeg = new CommittedSegment
                                    {
                                        SourceText = s,
                                        CommittedTime = DateTime.UtcNow
                                    };
                                    committedSegments.Add(newSeg);
                                    result.NewlyCommittedSegments.Add(newSeg);
                                    pendingPreviewSource = string.Empty;
                                }
                                else
                                {
                                    // 未確定プレビュー
                                    pendingPreviewSource = s;
                                }
                            }
                            else
                            {
                                // 途中の文は必ず完了しているとみなす
                                var newSeg = new CommittedSegment
                                {
                                    SourceText = s,
                                    CommittedTime = DateTime.UtcNow
                                };
                                committedSegments.Add(newSeg);
                                result.NewlyCommittedSegments.Add(newSeg);
                            }
                        }
                    }
                }
                else
                {
                    // アライメントが全く見つからなかった場合（起動直後やスクロールアウト時）
                    // すべて新規として処理
                    for (int i = 0; i < sentences.Count; i++)
                    {
                        var s = sentences[i].Trim();
                        if (string.IsNullOrEmpty(s)) continue;

                        if (i == sentences.Count - 1)
                        {
                            if (EndsWithEOS(s))
                            {
                                var newSeg = new CommittedSegment
                                {
                                    SourceText = s,
                                    CommittedTime = DateTime.UtcNow
                                };
                                committedSegments.Add(newSeg);
                                result.NewlyCommittedSegments.Add(newSeg);
                                pendingPreviewSource = string.Empty;
                            }
                            else
                            {
                                pendingPreviewSource = s;
                            }
                        }
                        else
                        {
                            var newSeg = new CommittedSegment
                            {
                                SourceText = s,
                                CommittedTime = DateTime.UtcNow
                            };
                            committedSegments.Add(newSeg);
                            result.NewlyCommittedSegments.Add(newSeg);
                        }
                    }
                }

                // プレビューが残っている場合、アイドルによる強制確定判定
                if (!string.IsNullOrEmpty(pendingPreviewSource))
                {
                    if ((DateTime.UtcNow - lastRawTextChangeTime) > idleThreshold)
                    {
                        var newSeg = new CommittedSegment
                        {
                            SourceText = pendingPreviewSource,
                            CommittedTime = DateTime.UtcNow
                        };
                        committedSegments.Add(newSeg);
                        result.NewlyCommittedSegments.Add(newSeg);
                        pendingPreviewSource = string.Empty;
                        result.PreviewChanged = true;
                    }
                }

                // メモリリーク防止のため最大50件に制限
                if (committedSegments.Count > 50)
                {
                    committedSegments.RemoveRange(0, committedSegments.Count - 50);
                }

                result.PendingPreviewSource = pendingPreviewSource;
                return result;
            }
        }

        public List<CommittedSegment> GetRecentCommittedSegments(int count)
        {
            lock (_lock)
            {
                int start = Math.Max(0, committedSegments.Count - count);
                return committedSegments.Skip(start).ToList();
            }
        }

        public CommittedSegment? GetLastCommittedSegment()
        {
            lock (_lock)
            {
                return committedSegments.LastOrDefault();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                committedSegments.Clear();
                pendingPreviewSource = string.Empty;
                lastRawText = string.Empty;
                lastRawTextChangeTime = DateTime.UtcNow;
            }
        }

        public static List<string> SplitIntoSentences(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(text)) return list;

            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (Array.IndexOf(TextUtil.PUNC_EOS, c) != -1)
                {
                    // 句読点を含めて分割
                    list.Add(text.Substring(start, i - start + 1));
                    start = i + 1;
                }
            }

            if (start < text.Length)
            {
                string remaining = text.Substring(start);
                if (!string.IsNullOrWhiteSpace(remaining))
                {
                    list.Add(remaining);
                }
            }

            return list;
        }

        private bool EndsWithEOS(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            return Array.IndexOf(TextUtil.PUNC_EOS, s[^1]) != -1;
        }

        private bool IsMatchOrExtension(string s, string committed, out bool isExtension)
        {
            isExtension = false;
            string cleanS = CleanForComparison(s);
            string cleanC = CleanForComparison(committed);

            if (cleanS == cleanC)
            {
                return true;
            }

            // 開始部分が一致していれば拡張と判定
            if (cleanS.StartsWith(cleanC, StringComparison.OrdinalIgnoreCase))
            {
                isExtension = true;
                return true;
            }

            // 類似度が一定以上なら同一文の修正とみなす
            double sim = TextUtil.Similarity(cleanS, cleanC);
            if (sim > 0.8)
            {
                if (cleanS.Length > cleanC.Length)
                    isExtension = true;
                return true;
            }

            return false;
        }

        private string CleanForComparison(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string clean = text.Trim().ToLower();
            
            // 末尾の句読点やスペースをトリムして比較しやすくする
            char[] trimChars = ".?!,。？！，、 \n\r\t".ToCharArray();
            clean = clean.TrimEnd(trimChars);
            return clean;
        }
    }
}

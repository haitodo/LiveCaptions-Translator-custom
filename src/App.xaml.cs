using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator
{
    public partial class App : Application
    {
        App()
        {
            // ① アプリケーションの正規の終了イベントを登録（より安全なタイミングで実行されます）
            this.Exit += OnAppExit;
            // ② 強制終了などのためのプロセス終了イベントも念のため残す
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            Translator.Setting?.Save();

            Task.Run(() => Translator.SyncLoop());
            Task.Run(() => Translator.TranslateLoop());
            Task.Run(() => Translator.DisplayLoop());
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 起動時のUI言語を適用します
            string lang = Translator.Setting?.InterfaceLanguage ?? "ja";
            LocalizationManager.SetLanguage(lang);
        }

        private void OnAppExit(object sender, ExitEventArgs e)
        {
            ForceKillLiveCaptions();
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            ForceKillLiveCaptions();
        }

        // ③ ウィンドウ情報に依存せず、プロセス名で直接検索して確実に終了させる
        private static void ForceKillLiveCaptions()
        {
            try
            {
                var processes = Process.GetProcessesByName("LiveCaptions");
                foreach (var process in processes)
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit();
                    }
                }
            }
            catch
            {
                // アプリ終了時の例外（権限エラーや既に終了済みなど）は無視して安全に閉じる
            }
        }
    }
}
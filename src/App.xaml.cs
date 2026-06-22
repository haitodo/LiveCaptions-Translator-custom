using System.Windows;

using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator
{
    public partial class App : Application
    {
        App()
        {
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

        private static void OnProcessExit(object sender, EventArgs e)
        {
            if (Translator.Window != null)
            {
                LiveCaptionsHandler.RestoreLiveCaptions(Translator.Window);
                LiveCaptionsHandler.KillLiveCaptions(Translator.Window);
            }
        }
    }
}

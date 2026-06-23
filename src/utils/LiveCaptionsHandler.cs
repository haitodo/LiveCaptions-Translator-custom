using System.Diagnostics;
using System.Windows.Automation;

using LiveCaptionsTranslator.apis;

namespace LiveCaptionsTranslator.utils
{
    public static class LiveCaptionsHandler
    {
        public static readonly string PROCESS_NAME = "LiveCaptions";

        private static AutomationElement? captionsTextBlock = null;

        public static AutomationElement LaunchLiveCaptions()
        {
            // 安全な終了処理の実行
            KillAllProcessesByPName(PROCESS_NAME);

            // シンプルかつ確実に LiveCaptions を起動
            var process = Process.Start(PROCESS_NAME);
            if (process == null)
                throw new Exception("Failed to start LiveCaptions process!");

            AutomationElement? window = null;

            // 最大3秒（300回 * 10ms）のタイムアウトを設定し、
            // ウィンドウ初期化中の例外（COM切断など）を安全に処理する堅牢なループ
            for (int attemptCount = 0; attemptCount < 300; attemptCount++)
            {
                var foundWindow = FindWindowByPId(process.Id);
                if (foundWindow != null)
                {
                    try
                    {
                        // 起動直後はプロパティ取得エラーが起きやすいため、安全にアクセスします
                        string className = foundWindow.Current.ClassName;
                        if (string.Equals(className, "LiveCaptionsDesktopWindow", StringComparison.Ordinal))
                        {
                            window = foundWindow;
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        // ウィンドウが完全に初期化される前は無視してループを継続します
                    }
                }

                Thread.Sleep(10);
            }

            if (window == null)
                throw new Exception("Failed to launch LiveCaptions (Window not found within timeout)!");

            // 【対策】ウィンドウを検出した瞬間に、アニメーションを強制無効化し画面外（不可視領域）に退避
            try
            {
                nint hWnd = new nint((long)window.Current.NativeWindowHandle);

                int disableTransition = 1; // TRUE
                WindowsAPI.DwmSetWindowAttribute(hWnd, WindowsAPI.DWMWA_TRANSITIONS_FORCEDISABLED, ref disableTransition, sizeof(int));

                WindowsAPI.MoveWindow(hWnd, -32000, -32000, 100, 100, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to apply early offscreen positioning: {ex.Message}");
            }

            return window;
        }

        public static void KillLiveCaptions(AutomationElement window)
        {
            try
            {
                nint hWnd = new nint((long)window.Current.NativeWindowHandle);
                WindowsAPI.GetWindowThreadProcessId(hWnd, out int processId);
                var process = Process.GetProcessById(processId);

                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to kill LiveCaptions process: {ex.Message}");
            }
        }

        public static void HideLiveCaptions(AutomationElement window)
        {
            try
            {
                nint hWnd = new nint((long)window.Current.NativeWindowHandle);

                // DWMアニメーションを一時的にオフ
                int disableTransition = 1; // TRUE
                WindowsAPI.DwmSetWindowAttribute(hWnd, WindowsAPI.DWMWA_TRANSITIONS_FORCEDISABLED, ref disableTransition, sizeof(int));

                // 画面外に一瞬で移動
                WindowsAPI.MoveWindow(hWnd, -32000, -32000, 100, 100, false);

                int exStyle = WindowsAPI.GetWindowLong(hWnd, WindowsAPI.GWL_EXSTYLE);

                // アニメーションなしで最小化し、タスクバー非表示（ツールウィンドウ化）を適用
                WindowsAPI.ShowWindow(hWnd, WindowsAPI.SW_MINIMIZE);
                WindowsAPI.SetWindowLong(hWnd, WindowsAPI.GWL_EXSTYLE, exStyle | WindowsAPI.WS_EX_TOOLWINDOW);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to hide LiveCaptions!", ex);
            }
        }

        public static void RestoreLiveCaptions(AutomationElement window)
        {
            try
            {
                nint hWnd = new nint((long)window.Current.NativeWindowHandle);
                int exStyle = WindowsAPI.GetWindowLong(hWnd, WindowsAPI.GWL_EXSTYLE);

                // 復元時はアニメーション効果を再び有効に戻す
                int disableTransition = 0; // FALSE
                WindowsAPI.DwmSetWindowAttribute(hWnd, WindowsAPI.DWMWA_TRANSITIONS_FORCEDISABLED, ref disableTransition, sizeof(int));

                WindowsAPI.SetWindowLong(hWnd, WindowsAPI.GWL_EXSTYLE, exStyle & ~WindowsAPI.WS_EX_TOOLWINDOW);
                WindowsAPI.ShowWindow(hWnd, WindowsAPI.SW_RESTORE);
                WindowsAPI.SetForegroundWindow(hWnd);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to restore LiveCaptions!", ex);
            }
        }

        public static void FixLiveCaptions(AutomationElement window)
        {
            try
            {
                nint hWnd = new nint((long)window.Current.NativeWindowHandle);

                RECT rect;
                if (!WindowsAPI.GetWindowRect(hWnd, out rect))
                    throw new Exception("Unable to get the window rectangle of LiveCaptions!");

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                int x = rect.Left;
                int y = rect.Top;

                bool isSuccess = true;
                // 画面外（x < 0 || y < 0）にある場合でも、正常な位置に引き戻す
                if (x < 0 || y < 0 || width < 100 || height < 100)
                    isSuccess = WindowsAPI.MoveWindow(hWnd, 800, 600, 600, 200, true);

                if (!isSuccess)
                    throw new Exception("Failed to fix LiveCaptions position!");
            }
            catch (Exception ex)
            {
                throw new Exception("Error during fixing LiveCaptions!", ex);
            }
        }

        public static string GetCaptions(AutomationElement window)
        {
            if (captionsTextBlock == null)
                captionsTextBlock = FindElementByAId(window, "CaptionsTextBlock");
            try
            {
                return captionsTextBlock?.Current.Name ?? string.Empty;
            }
            catch (ElementNotAvailableException)
            {
                captionsTextBlock = null;
                throw;
            }
        }

        private static AutomationElement FindWindowByPId(int processId)
        {
            var condition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
            return AutomationElement.RootElement.FindFirst(TreeScope.Children, condition);
        }

        public static AutomationElement? FindElementByAId(
            AutomationElement window, string automationId, CancellationToken token = default)
        {
            try
            {
                PropertyCondition condition = new PropertyCondition(
                    AutomationElement.AutomationIdProperty, automationId);
                return window.FindFirst(TreeScope.Descendants, condition);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        public static void PrintAllElementsAId(AutomationElement window)
        {
            var treeWalker = TreeWalker.RawViewWalker;
            var stack = new Stack<AutomationElement>();
            stack.Push(window);

            while (stack.Count > 0)
            {
                var element = stack.Pop();
                try
                {
                    if (!string.IsNullOrEmpty(element.Current.AutomationId))
                        Console.WriteLine(element.Current.AutomationId);
                }
                catch (ElementNotAvailableException)
                {
                    continue;
                }

                var child = treeWalker.GetFirstChild(element);
                while (child != null)
                {
                    stack.Push(child);
                    child = treeWalker.GetNextSibling(child);
                }
            }
        }

        public static bool ClickSettingsButton(AutomationElement window)
        {
            var settingsButton = FindElementByAId(window, "SettingsButton");
            if (settingsButton != null)
            {
                if (settingsButton.GetCurrentPattern(InvokePattern.Pattern) is InvokePattern invokePattern)
                {
                    invokePattern.Invoke();
                    return true;
                }
            }
            return false;
        }

        private static void KillAllProcessesByPName(string processName)
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
                return;

            foreach (Process process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit();
                    }
                }
                catch (Exception)
                {
                    // 権限エラーや、既に終了している場合の例外を無視して次のプロセスへ進める
                }
            }
        }
    }
}
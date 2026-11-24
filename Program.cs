using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using JiahaoToolbox.Core;
using JiahaoToolbox.UI;

namespace JiahaoToolbox
{
    internal static class Program
    {
        private const string MutexName = "Local\\JiahaoToolbox_SingleInstance_Mutex";
        private const string RestoreMsgName = "JiahaoToolbox_RestoreWindow_Msg";

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                LogCrash(e.Exception);
                string msg = e.Exception != null ? e.Exception.Message : "未知错误";
                MessageBox.Show("运行出现异常（详情已写入 crash.log）：\r\n" + msg, "嘉豪工具箱", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                LogCrash(e.ExceptionObject as Exception);
            };

            bool startTray = false;
            bool resumeEco = false;
            string initialPage = "overview";
            int waitPid = 0;

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i] ?? "";
                    if (string.Equals(a, "--render-pages", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        return RenderAllPagesPreview(args[i + 1]);
                    }
                    if (string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase))
                    {
                        startTray = true;
                    }
                    else if (string.Equals(a, "--resume-eco", StringComparison.OrdinalIgnoreCase))
                    {
                        resumeEco = true;
                    }
                    else if (a.StartsWith("--page=", StringComparison.OrdinalIgnoreCase))
                    {
                        initialPage = a.Substring("--page=".Length).Trim();
                    }
                    else if (a.StartsWith("--wait-pid=", StringComparison.OrdinalIgnoreCase))
                    {
                        int parsed;
                        if (int.TryParse(a.Substring("--wait-pid=".Length).Trim(), out parsed))
                        {
                            waitPid = parsed;
                        }
                    }
                }
            }

            // 按需提权交接时，短暂等待旧的非管理员实例释放单实例互斥锁
            if (waitPid > 0)
            {
                try
                {
                    using (Process oldProc = Process.GetProcessById(waitPid))
                    {
                        oldProc.WaitForExit(3500);
                    }
                }
                catch { }
            }

            uint restoreMsg = Win32Native.RegisterWindowMessageW(RestoreMsgName);
            bool createdNew;
            Mutex mutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                // 可能是提权交接瞬间旧进程刚释放，短暂重试一次
                bool acquired = false;
                if (waitPid > 0)
                {
                    try { acquired = mutex.WaitOne(2000, false); } catch { acquired = false; }
                }
                if (!acquired)
                {
                    mutex.Dispose();
                    Win32Native.PostMessageW(new IntPtr(Win32Native.HWND_BROADCAST), restoreMsg, IntPtr.Zero, IntPtr.Zero);
                    return 0;
                }
            }

            bool mutexReleased = false;
            Action releaseMutex = delegate
            {
                if (!mutexReleased)
                {
                    mutexReleased = true;
                    try { mutex.ReleaseMutex(); } catch { }
                    try { mutex.Dispose(); } catch { }
                }
            };

            try
            {
                Application.Run(new MainForm(startTray, restoreMsg, initialPage, resumeEco, releaseMutex, false));
            }
            finally
            {
                releaseMutex();
            }
            return 0;
        }

        private static int RenderAllPagesPreview(string outDir)
        {
            try
            {
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                using (MainForm form = new MainForm(false, 0, "overview", false, null, true))
                {
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-3000, -3000);
                    form.ShowInTaskbar = false;
                    form.Show();
                    form.LoadReadOnlyPreviewSnapshot();

                    string[] pages = new string[] { "overview", "eco", "guard", "settings" };
                    for (int i = 0; i < pages.Length; i++)
                    {
                        form.NavigateTo(pages[i]);
                        Application.DoEvents();
                        Thread.Sleep(60);
                        Application.DoEvents();

                        using (Bitmap bmp = new Bitmap(form.Width, form.Height))
                        {
                            form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                            bmp.Save(Path.Combine(outDir, "page_" + pages[i] + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    form.Close();
                }
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(outDir, "render_error.txt"), ex.ToString(), System.Text.Encoding.UTF8);
                return 1;
            }
        }

        private static void LogCrash(Exception ex)
        {
            if (ex == null) return;
            try
            {
                string dir = ToolboxConfig.ConfigDir;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "crash.log");
                File.AppendAllText(path, string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}\r\n\r\n", DateTime.Now, ex), System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}

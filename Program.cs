using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyVersion("1.5.0.0")]
[assembly: AssemblyFileVersion("1.5.0.0")]
[assembly: AssemblyInformationalVersion("1.5.0")]

internal static class Program
{
    internal const string AppName = "CloseCaseLid";
    private const string MutexName = @"Local\CloseCaseLid";
    private const string AppFolderName = "CloseCaseLid";
    private const string ExeFileName = "CloseCaseLid.exe";

    [STAThread]
    private static void Main()
    {
        EnsureRunningFromInstalledLocation();

        bool createdNew;
        using (var mutex = new Mutex(true, MutexName, out createdNew))
        {
            if (!createdNew)
            {
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayContext());
        }
    }

    internal static string InstalledDirectoryPath
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName); }
    }

    internal static string InstalledExePath
    {
        get { return Path.Combine(InstalledDirectoryPath, ExeFileName); }
    }

    private static void EnsureRunningFromInstalledLocation()
    {
        var currentPath = Path.GetFullPath(Application.ExecutablePath);
        var installedPath = Path.GetFullPath(InstalledExePath);

        if (PathsEqual(currentPath, installedPath))
        {
            return;
        }

        StopRunningInstalledInstance(installedPath);
        Directory.CreateDirectory(InstalledDirectoryPath);
        File.Copy(currentPath, installedPath, true);

        Process.Start(new ProcessStartInfo
        {
            FileName = installedPath,
            UseShellExecute = true
        });
        Environment.Exit(0);
    }

    internal static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void StopRunningInstalledInstance(string installedPath)
    {
        var processName = Path.GetFileNameWithoutExtension(installedPath);
        var currentProcessId = Process.GetCurrentProcess().Id;

        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                if (process.Id == currentProcessId)
                {
                    continue;
                }

                string processPath;
                try
                {
                    processPath = process.MainModule.FileName;
                }
                catch
                {
                    continue;
                }

                if (!PathsEqual(processPath, installedPath))
                {
                    continue;
                }

                process.Kill();
                if (!process.WaitForExit(5000))
                {
                    throw new InvalidOperationException("Could not stop the installed app before updating it.");
                }
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = Program.AppName;
    private const string LidSettingsArguments = "/name Microsoft.PowerOptions /page pageGlobalSettings";
    private const int BalloonTimeoutMs = 2500;

    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip menu;
    private readonly Form menuHost;
    private readonly Icon sleepIcon;
    private readonly Icon doNothingIcon;
    private readonly Icon unknownIcon;
    private readonly ToolStripMenuItem versionMenuItem;
    private readonly ToolStripMenuItem sleepMenuItem;
    private readonly ToolStripMenuItem doNothingMenuItem;
    private readonly ToolStripMenuItem openSettingsMenuItem;
    private readonly ToolStripMenuItem addStartupMenuItem;
    private readonly ToolStripMenuItem removeStartupMenuItem;
    private readonly ToolStripMenuItem uninstallMenuItem;

    public TrayContext()
    {
        sleepIcon = LoadEmbeddedIcon(Program.AppName + ".icon-sleep.ico") ?? SystemIcons.Information;
        doNothingIcon = LoadEmbeddedIcon(Program.AppName + ".icon-do-nothing.ico") ?? SystemIcons.Application;
        unknownIcon = LoadEmbeddedIcon(Program.AppName + ".icon-unknown.ico") ?? SystemIcons.Question;

        versionMenuItem = new ToolStripMenuItem(Program.AppName + " " + GetDisplayVersion());
        versionMenuItem.Enabled = false;
        sleepMenuItem = CreateMenuItem("Sleep on lid close", delegate { SetLidAction(1, "Sleep"); });
        doNothingMenuItem = CreateMenuItem("Do nothing on lid close", delegate { SetLidAction(0, "Do nothing"); });
        openSettingsMenuItem = CreateMenuItem("Open lid settings", OpenLidSettings);
        addStartupMenuItem = CreateMenuItem("Add to auto start", delegate { SetStartupEnabled(true); });
        removeStartupMenuItem = CreateMenuItem("Remove from auto start", delegate { SetStartupEnabled(false); });
        uninstallMenuItem = CreateMenuItem("Uninstall", UninstallApplication);
        var exitMenuItem = CreateMenuItem("Exit", ExitThread);

        menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            versionMenuItem,
            new ToolStripSeparator(),
            sleepMenuItem,
            doNothingMenuItem,
            new ToolStripSeparator(),
            openSettingsMenuItem,
            new ToolStripSeparator(),
            addStartupMenuItem,
            removeStartupMenuItem,
            uninstallMenuItem,
            new ToolStripSeparator(),
            exitMenuItem
        });
        menu.Opening += (_, __) => RefreshState();
        menu.Closed += Menu_Closed;

        menuHost = CreateMenuHost();

        notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = sleepIcon,
            Visible = true
        };
        notifyIcon.MouseUp += NotifyIcon_MouseUp;

        RefreshState();
        ShowBalloon(Program.AppName, "Running in the system tray.");
    }

    protected override void ExitThreadCore()
    {
        notifyIcon.MouseUp -= NotifyIcon_MouseUp;
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        menuHost.Close();
        menuHost.Dispose();
        if (!ReferenceEquals(sleepIcon, SystemIcons.Information))
        {
            sleepIcon.Dispose();
        }
        if (!ReferenceEquals(doNothingIcon, SystemIcons.Application))
        {
            doNothingIcon.Dispose();
        }
        if (!ReferenceEquals(unknownIcon, SystemIcons.Question))
        {
            unknownIcon.Dispose();
        }
        base.ExitThreadCore();
    }

    private void NotifyIcon_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        RefreshState();
        ShowMenuAtCursor();
    }

    private void ShowMenuAtCursor()
    {
        var cursorPosition = Cursor.Position;
        menuHost.Location = new Point(cursorPosition.X, cursorPosition.Y);
        menuHost.Show();
        menuHost.Activate();
        menu.Show(menuHost, menuHost.PointToClient(cursorPosition));
    }

    private void Menu_Closed(object sender, ToolStripDropDownClosedEventArgs e)
    {
        menuHost.Hide();
    }

    private static Form CreateMenuHost()
    {
        var form = new Form();
        form.FormBorderStyle = FormBorderStyle.None;
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Size = new Size(1, 1);
        form.Opacity = 0;
        form.TopMost = true;
        return form;
    }

    private void SetLidAction(int value, string label)
    {
        try
        {
            RunPowerCfg("/SETACVALUEINDEX scheme_current SUB_BUTTONS LIDACTION " + value);
            RunPowerCfg("/SETDCVALUEINDEX scheme_current SUB_BUTTONS LIDACTION " + value);
            RunPowerCfg("/SETACTIVE scheme_current");
            RefreshState();
            ShowBalloon("Lid close action", "Set to " + label + ".");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void SetStartupEnabled(bool enabled)
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Could not open the startup registry key.");
                }

                if (enabled)
                {
                    key.SetValue(StartupValueName, "\"" + Program.InstalledExePath + "\"");
                    ShowBalloon("Startup", "Tray app will start when you sign in.");
                }
                else
                {
                    key.DeleteValue(StartupValueName, false);
                    ShowBalloon("Startup", "Tray app removed from sign-in startup.");
                }
            }

            RefreshState();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void OpenLidSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "control.exe",
                Arguments = LidSettingsArguments,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void UninstallApplication()
    {
        var result = MessageBox.Show(
            "Uninstall " + Program.AppName + ", remove it from startup, and delete it from LocalAppData?",
            "Uninstall " + Program.AppName,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            SetStartupEnabled(false);
            ScheduleInstalledFilesDeletion();
            ExitThread();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void RefreshState()
    {
        var state = GetLidActionState();
        var startupEnabled = IsStartupEnabled();

        sleepMenuItem.Checked = state.AC == 1 && state.DC == 1;
        doNothingMenuItem.Checked = state.AC == 0 && state.DC == 0;
        addStartupMenuItem.Visible = !startupEnabled;
        removeStartupMenuItem.Visible = startupEnabled;

        if (state.AC == state.DC)
        {
            ApplyTrayState(state.AC);
        }
        else
        {
            notifyIcon.Icon = unknownIcon;
            notifyIcon.Text = "Lid close: Mixed AC/DC";
        }
    }

    private void ScheduleInstalledFilesDeletion()
    {
        var exePath = Application.ExecutablePath;
        var installedDir = Path.GetDirectoryName(exePath);

        if (string.IsNullOrWhiteSpace(installedDir))
        {
            throw new InvalidOperationException("Could not determine the installed app directory.");
        }

        if (!Program.PathsEqual(exePath, Program.InstalledExePath))
        {
            throw new InvalidOperationException("Refusing to remove the app because it is not running from LocalAppData.");
        }

        var cleanupCommand = string.Format(
            "/c ping 127.0.0.1 -n 3 > nul && del /f /q \"{0}\" && rmdir /s /q \"{1}\"",
            exePath,
            installedDir);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = cleanupCommand,
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private void ApplyTrayState(int actionValue)
    {
        switch (actionValue)
        {
            case 0:
                notifyIcon.Icon = doNothingIcon;
                notifyIcon.Text = "Lid close: Do nothing";
                break;
            case 1:
                notifyIcon.Icon = sleepIcon;
                notifyIcon.Text = "Lid close: Sleep";
                break;
            default:
                notifyIcon.Icon = unknownIcon;
                notifyIcon.Text = "Lid close: Custom";
                break;
        }
    }

    private static ToolStripMenuItem CreateMenuItem(string text, Action onClick)
    {
        var menuItem = new ToolStripMenuItem(text);
        menuItem.Click += delegate { onClick(); };
        return menuItem;
    }

    private static string GetDisplayVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (informational != null && !string.IsNullOrWhiteSpace(informational.InformationalVersion))
        {
            return "v" + informational.InformationalVersion;
        }

        return "v" + assembly.GetName().Version;
    }

    private static Icon LoadEmbeddedIcon(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return null;
        }

        using (stream)
        using (var memory = new MemoryStream())
        {
            stream.CopyTo(memory);
            memory.Position = 0;
            return new Icon(memory);
        }
    }

    private bool IsStartupEnabled()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
        {
            if (key == null)
            {
                return false;
            }

            var value = key.GetValue(StartupValueName) as string;
            return !string.IsNullOrWhiteSpace(value) && value.IndexOf(Program.InstalledExePath, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    private static void ShowError(Exception ex)
    {
        MessageBox.Show(ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private LidState GetLidActionState()
    {
        var output = RunPowerCfgWithOutput("/qh scheme_current SUB_BUTTONS LIDACTION");
        var acMatch = Regex.Match(output, @"Current AC Power Setting Index:\s+0x([0-9a-fA-F]+)");
        var dcMatch = Regex.Match(output, @"Current DC Power Setting Index:\s+0x([0-9a-fA-F]+)");

        if (!acMatch.Success || !dcMatch.Success)
        {
            throw new InvalidOperationException("Could not read the current lid close action.");
        }

        return new LidState(
            Convert.ToInt32(acMatch.Groups[1].Value, 16),
            Convert.ToInt32(dcMatch.Groups[1].Value, 16));
    }

    private void RunPowerCfg(string arguments)
    {
        using (var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powercfg.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        }))
        {
            if (process == null)
            {
                throw new InvalidOperationException("Could not start powercfg.");
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("powercfg failed with exit code " + process.ExitCode + ".");
            }
        }
    }

    private string RunPowerCfgWithOutput(string arguments)
    {
        using (var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powercfg.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }))
        {
            if (process == null)
            {
                throw new InvalidOperationException("Could not start powercfg.");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "powercfg failed." : error.Trim());
            }

            return output;
        }
    }

    private void ShowBalloon(string title, string text)
    {
        notifyIcon.BalloonTipTitle = title;
        notifyIcon.BalloonTipText = text;
        notifyIcon.ShowBalloonTip(BalloonTimeoutMs);
    }

    private struct LidState
    {
        private readonly int ac;
        private readonly int dc;

        public LidState(int ac, int dc)
        {
            this.ac = ac;
            this.dc = dc;
        }

        public int AC { get { return ac; } }
        public int DC { get { return dc; } }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Anti-Peek")]
[assembly: AssemblyDescription("Camuflagem de opacidade para janelas privadas no Windows")]
[assembly: AssemblyProduct("Anti-Peek")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace AntiPeek
{
    internal class TrackedWindow
    {
        public IntPtr Handle;
        public int OriginalExStyle;
    }

    public class AntiPeekContext : ApplicationContext
    {
        // Constantes Win32 (USER32.dll)
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID_TOGGLE = 9001;

        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_F10 = 0x79;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const uint LWA_ALPHA = 0x00000002;

        private const string RUN_REGISTRY_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_REGISTRY_NAME = "AntiPeek";

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private readonly HotkeyMessageWindow _messageWindow;
        private readonly NotifyIcon _trayIcon;
        private readonly Dictionary<IntPtr, TrackedWindow> _camouflagedWindows = new Dictionary<IntPtr, TrackedWindow>();
        private readonly HashSet<string> _ignoredClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Progman",
            "WorkerW",
            "Shell_TrayWnd",
            "Shell_SecondaryTrayWnd",
            "MultitaskingViewFrame",
            "TaskListThumbnailWnd",
            "Windows.UI.Core.CoreWindow"
        };

        // Opacidade padrão: 45 (~18% de visibilidade, equivalente ao nível padrão do SecretScreen)
        private byte _targetAlpha = 76;
        private ToolStripMenuItem _startupMenuItem;
        private readonly List<ToolStripMenuItem> _opacityMenuItems = new List<ToolStripMenuItem>();
        private Icon _generatedIcon;

        public AntiPeekContext()
        {
            _messageWindow = new HotkeyMessageWindow(this);
            _generatedIcon = CreateShieldIcon();

            ContextMenuStrip menu = BuildContextMenu();

            _trayIcon = new NotifyIcon
            {
                Icon = _generatedIcon,
                Text = "Anti-Peek (Ctrl + Shift + F10)",
                ContextMenuStrip = menu,
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) => RestoreAllWindows();

            bool registered = RegisterHotKey(_messageWindow.Handle, HOTKEY_ID_TOGGLE, MOD_CONTROL | MOD_SHIFT, VK_F10);
            if (!registered)
            {
                _trayIcon.ShowBalloonTip(
                    4000,
                    "Anti-Peek — Aviso",
                    "Não foi possível registrar o atalho Ctrl + Shift + F10 (já em uso por outro programa).",
                    ToolTipIcon.Warning
                );
            }
        }

        private ContextMenuStrip BuildContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem headerItem = new ToolStripMenuItem("Anti-Peek (Ctrl + Shift + F10)")
            {
                Enabled = false,
                Font = new Font(SystemFonts.MenuFont, FontStyle.Bold)
            };
            menu.Items.Add(headerItem);
            menu.Items.Add(new ToolStripSeparator());

            // Submenu de Nível de Opacidade
            ToolStripMenuItem opacityMenu = new ToolStripMenuItem("Nível de Opacidade");
            AddOpacityOption(opacityMenu, "10%", 26);
            AddOpacityOption(opacityMenu, "18%", 45);
            AddOpacityOption(opacityMenu, "30%", 76);
            AddOpacityOption(opacityMenu, "50%", 128);
            menu.Items.Add(opacityMenu);

            ToolStripMenuItem restoreAllItem = new ToolStripMenuItem("Restaurar Todas as Janelas", null, (s, e) => RestoreAllWindows());
            menu.Items.Add(restoreAllItem);

            menu.Items.Add(new ToolStripSeparator());

            _startupMenuItem = new ToolStripMenuItem("Iniciar junto com o Windows", null, (s, e) => ToggleStartup())
            {
                Checked = IsStartupEnabled()
            };
            menu.Items.Add(_startupMenuItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Sair", null, (s, e) => ExitThread());
            menu.Items.Add(exitItem);

            return menu;
        }

        private void AddOpacityOption(ToolStripMenuItem parent, string label, byte alphaValue)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label)
            {
                Tag = alphaValue,
                Checked = (_targetAlpha == alphaValue)
            };

            item.Click += (s, e) =>
            {
                _targetAlpha = alphaValue;
                foreach (ToolStripMenuItem opt in _opacityMenuItems)
                {
                    opt.Checked = ((byte)opt.Tag == _targetAlpha);
                }
                UpdateActiveCamouflagedWindows();
            };

            _opacityMenuItems.Add(item);
            parent.DropDownItems.Add(item);
        }

        public void OnHotkeyPressed()
        {
            CleanupClosedWindows();

            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || hwnd == _messageWindow.Handle)
                return;

            if (IsExceptionWindow(hwnd))
                return;

            if (_camouflagedWindows.ContainsKey(hwnd))
            {
                RestoreWindow(hwnd);
            }
            else
            {
                CamouflageWindow(hwnd);
            }
        }

        private bool IsExceptionWindow(IntPtr hwnd)
        {
            StringBuilder sb = new StringBuilder(256);
            if (GetClassName(hwnd, sb, sb.Capacity) > 0)
            {
                string className = sb.ToString();
                if (_ignoredClasses.Contains(className))
                    return true;
            }
            return false;
        }

        private void CamouflageWindow(IntPtr hwnd)
        {
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
            SetLayeredWindowAttributes(hwnd, 0, _targetAlpha, LWA_ALPHA);

            _camouflagedWindows[hwnd] = new TrackedWindow
            {
                Handle = hwnd,
                OriginalExStyle = exStyle
            };
        }

        private void RestoreWindow(IntPtr hwnd)
        {
            TrackedWindow tracked;
            if (_camouflagedWindows.TryGetValue(hwnd, out tracked))
            {
                if (IsWindow(hwnd))
                {
                    SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
                    if ((tracked.OriginalExStyle & WS_EX_LAYERED) == 0)
                    {
                        SetWindowLong(hwnd, GWL_EXSTYLE, tracked.OriginalExStyle);
                    }
                }
                _camouflagedWindows.Remove(hwnd);
            }
        }

        private void UpdateActiveCamouflagedWindows()
        {
            CleanupClosedWindows();
            foreach (KeyValuePair<IntPtr, TrackedWindow> kvp in _camouflagedWindows)
            {
                SetLayeredWindowAttributes(kvp.Key, 0, _targetAlpha, LWA_ALPHA);
            }
        }

        private void RestoreAllWindows()
        {
            List<IntPtr> handles = new List<IntPtr>(_camouflagedWindows.Keys);
            foreach (IntPtr hwnd in handles)
            {
                RestoreWindow(hwnd);
            }
        }

        private void CleanupClosedWindows()
        {
            List<IntPtr> deadHandles = new List<IntPtr>();
            foreach (IntPtr hwnd in _camouflagedWindows.Keys)
            {
                if (!IsWindow(hwnd))
                {
                    deadHandles.Add(hwnd);
                }
            }
            foreach (IntPtr dead in deadHandles)
            {
                _camouflagedWindows.Remove(dead);
            }
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RUN_REGISTRY_KEY, false))
                {
                    if (key == null) return false;
                    object val = key.GetValue(APP_REGISTRY_NAME);
                    return val != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private void ToggleStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RUN_REGISTRY_KEY, true))
                {
                    if (key == null) return;

                    if (IsStartupEnabled())
                    {
                        key.DeleteValue(APP_REGISTRY_NAME, false);
                        _startupMenuItem.Checked = false;
                    }
                    else
                    {
                        string exePath = "\"" + Application.ExecutablePath + "\"";
                        key.SetValue(APP_REGISTRY_NAME, exePath);
                        _startupMenuItem.Checked = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao alterar inicialização com o Windows:\n" + ex.Message, "Anti-Peek", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static Icon CreateShieldIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Desenha um escudo estilizado azul-escuro/ciano
                Point[] shieldPoints = new Point[]
                {
                    new Point(16, 2),
                    new Point(28, 7),
                    new Point(28, 16),
                    new Point(16, 29),
                    new Point(4, 16),
                    new Point(4, 7)
                };

                using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(230, 30, 41, 59)))
                using (Pen borderPen = new Pen(Color.FromArgb(255, 56, 189, 248), 2.2f))
                {
                    g.FillPolygon(fillBrush, shieldPoints);
                    g.DrawPolygon(borderPen, shieldPoints);
                }

                // Desenha um ícone de olho cortado (anti-peek) no centro
                using (Pen eyePen = new Pen(Color.White, 2.0f))
                using (Pen slashPen = new Pen(Color.FromArgb(255, 248, 113, 113), 2.2f))
                {
                    g.DrawEllipse(eyePen, 10, 11, 12, 8);
                    g.DrawLine(slashPen, 9, 21, 23, 9);
                }

                IntPtr hIcon = bmp.GetHicon();
                Icon iconCopy = (Icon)Icon.FromHandle(hIcon).Clone();
                DestroyIcon(hIcon);
                return iconCopy;
            }
        }

        protected override void ExitThreadCore()
        {
            UnregisterHotKey(_messageWindow.Handle, HOTKEY_ID_TOGGLE);
            RestoreAllWindows();

            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            if (_generatedIcon != null)
            {
                _generatedIcon.Dispose();
            }
            _messageWindow.DestroyHandle();

            base.ExitThreadCore();
        }

        private class HotkeyMessageWindow : NativeWindow
        {
            private readonly AntiPeekContext _context;

            public HotkeyMessageWindow(AntiPeekContext context)
            {
                _context = context;
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID_TOGGLE)
                {
                    _context.OnHotkeyPressed();
                }
                base.WndProc(ref m);
            }
        }

        [STAThread]
        public static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, @"Local\AntiPeek_SingleInstance_Mutex", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AntiPeekContext());
            }
        }
    }
}

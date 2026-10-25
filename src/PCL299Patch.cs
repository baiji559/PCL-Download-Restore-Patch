using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace PCL299Patch
{
    /// <summary>
    /// 针对 PCL 2.99.0.0 官方单文件构建的字节级补丁参数。
    /// 2.99 把 RestrictMcDownload 写成 Const，编译期就删掉了正常下载分支，
    /// 所以只能改编译后的 IL：把下载页的“关闭下载”任务换回正常的 DlClientListLoader，
    /// 并让“修改”页的 Minecraft 卡片重新可见。
    /// </summary>
    internal static class PatchInfo
    {
        public const long FileSize = 5980160L;
        public const string OfficialSha256 = "6B6E1260CB5A1D6A303855F1C60CAB3D0BC1AEBBA7E40A964180188B271E7386";
        public const string PatchedSha256 = "98B494DD68B953988F445BFC29485C7D2014D472D8A7A685488A136FEA4D3F5F";

        // PageDownloadInstall.LoaderInit 的 IL_0019 起：把构造 LoaderTask 的 49 字节换成 ldsfld DlClientListLoader
        public const int BodyOffset = 0x2C0D9;
        public const int BodyLength = 0x31;
        public static readonly byte[] BodyPrefix = new byte[] { 0x14, 0x7E, 0x0F, 0x04, 0x00, 0x04 };

        // PageDownloadModify.LoadInstanceFinished：CardMinecraft 的 Visibility 常量
        public const int VisibilityOffset = 0x322E6;
        public const byte VisibilityOriginal = 0x18; // ldc.i4.2 (Collapsed)
        public const byte VisibilityPatched = 0x16;  // ldc.i4.0 (Visible)
    }

    internal static class PatchEngine
    {
        public static string Sha256(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(data);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("X2"));
                return sb.ToString();
            }
        }

        public static string Sha256OfFile(string path)
        {
            return Sha256(File.ReadAllBytes(path));
        }

        /// <summary>检查文件是否为受支持的官方/已修补 2.99 构建。</summary>
        public static bool IsSupportedBuild(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Length != PatchInfo.FileSize) return false;
                var sha = Sha256OfFile(path);
                return sha == PatchInfo.OfficialSha256 || sha == PatchInfo.PatchedSha256;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>返回 null 表示成功，否则返回错误说明。</summary>
        public static string Apply(string path)
        {
            if (!File.Exists(path)) return "找不到文件：" + path;

            var bytes = File.ReadAllBytes(path);
            var sha = Sha256(bytes);

            if (sha == PatchInfo.PatchedSha256) return "ALREADY_PATCHED";

            if (bytes.Length != PatchInfo.FileSize || sha != PatchInfo.OfficialSha256)
            {
                return "VERSION_MISMATCH|" + bytes.Length + "|" + sha;
            }

            if (bytes[PatchInfo.BodyOffset] != 0x14 ||
                bytes[PatchInfo.VisibilityOffset] != PatchInfo.VisibilityOriginal)
            {
                return "BYTES_MISMATCH";
            }

            var backup = path + ".pcl299.bak";
            if (!File.Exists(backup)) File.Copy(path, backup, false);

            var patched = (byte[])bytes.Clone();
            for (var i = 0; i < PatchInfo.BodyLength; i++) patched[PatchInfo.BodyOffset + i] = 0x00; // nop
            Buffer.BlockCopy(PatchInfo.BodyPrefix, 0, patched, PatchInfo.BodyOffset, PatchInfo.BodyPrefix.Length);
            patched[PatchInfo.VisibilityOffset] = PatchInfo.VisibilityPatched;

            var temp = path + ".pcl299.tmp";
            File.WriteAllBytes(temp, patched);
            File.Copy(temp, path, true);
            File.Delete(temp);

            var after = Sha256OfFile(path);
            if (after != PatchInfo.PatchedSha256) return "VERIFY_FAILED|" + after;
            return null;
        }

        /// <summary>返回 null 表示成功，否则返回错误说明。</summary>
        public static string Restore(string path)
        {
            var backup = path + ".pcl299.bak";
            if (!File.Exists(backup)) return "NO_BACKUP";
            var sha = Sha256OfFile(backup);
            if (sha != PatchInfo.OfficialSha256) return "BACKUP_INVALID|" + sha;
            File.Copy(backup, path, true);
            return null;
        }
    }

    public class MainForm : Form
    {
        private readonly TextBox _pathBox;
        private readonly TextBox _logBox;
        private readonly Label _statusLabel;

        public MainForm(string initialPath)
        {
            Text = "PCL 2.99 下载功能恢复补丁（非官方）";
            ClientSize = new Size(660, 440);
            MinimumSize = new Size(660, 440);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F);

            var title = new Label();
            title.Text = "PCL 2.99 下载功能恢复补丁";
            title.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(18, 14);
            Controls.Add(title);

            var subtitle = new Label();
            subtitle.Text = "直接修改你电脑上已安装的 PCL 2.99（不产生新启动器）：退出 PCL，点“开始修补”，之后照旧用原来的快捷方式启动。"
                + "只支持 2.99.0.0 官方单文件构建，修改前会自动备份为 .pcl299.bak。";
            subtitle.Location = new Point(20, 48);
            subtitle.Size = new Size(620, 36);
            subtitle.ForeColor = Color.DimGray;
            Controls.Add(subtitle);

            var fileLabel = new Label();
            fileLabel.Text = "PCL 主程序：";
            fileLabel.AutoSize = true;
            fileLabel.Location = new Point(20, 94);
            Controls.Add(fileLabel);

            _pathBox = new TextBox();
            _pathBox.Location = new Point(100, 91);
            _pathBox.Size = new Size(430, 25);
            _pathBox.Text = initialPath ?? string.Empty;
            Controls.Add(_pathBox);

            var browseButton = new Button();
            browseButton.Text = "浏览...";
            browseButton.Location = new Point(540, 89);
            browseButton.Size = new Size(100, 28);
            browseButton.Click += delegate { Browse(); };
            Controls.Add(browseButton);

            var patchButton = new Button();
            patchButton.Text = "开始修补";
            patchButton.Location = new Point(20, 130);
            patchButton.Size = new Size(150, 36);
            patchButton.Click += delegate { Patch(); };
            Controls.Add(patchButton);

            var restoreButton = new Button();
            restoreButton.Text = "还原备份";
            restoreButton.Location = new Point(180, 130);
            restoreButton.Size = new Size(120, 36);
            restoreButton.Click += delegate { Restore(); };
            Controls.Add(restoreButton);

            _statusLabel = new Label();
            _statusLabel.Text = "请确认 PCL 已关闭，然后点击“开始修补”。";
            _statusLabel.Location = new Point(316, 138);
            _statusLabel.Size = new Size(330, 22);
            _statusLabel.ForeColor = Color.Gray;
            Controls.Add(_statusLabel);

            _logBox = new TextBox();
            _logBox.Multiline = true;
            _logBox.ReadOnly = true;
            _logBox.ScrollBars = ScrollBars.Vertical;
            _logBox.Location = new Point(20, 180);
            _logBox.Size = new Size(620, 240);
            _logBox.BackColor = Color.FromArgb(250, 250, 250);
            _logBox.Font = new Font("Consolas", 9F);
            Controls.Add(_logBox);

            if (string.IsNullOrEmpty(_pathBox.Text))
            {
                var auto = FindInstalledPcl();
                if (auto != null)
                {
                    _pathBox.Text = auto;
                    Log("已自动找到已安装的 PCL：" + auto);
                }
                else
                {
                    Log("没有自动找到 PCL，请点“浏览”选择 Plain Craft Launcher 2.exe。");
                }
            }

            Log("PCL 2.99 下载功能恢复补丁（非官方），只支持 2.99.0.0 官方单文件构建。");
            Log("修改内容：");
            Log("  1) 下载页 LoaderInit：改用正常的 DlClientListLoader，恢复原版游戏与加载器列表。");
            Log("  2) 修改页 Minecraft 卡片：恢复显示，可进入安装/补全界面。");
            Log("提示：补丁不修改游戏文件；若 PCL 自动更新，需要重新运行本补丁。");
        }

        private static string FindInstalledPcl()
        {
            try
            {
                var dir = Path.GetDirectoryName(Application.ExecutablePath);
                var candidate = Path.Combine(dir, "Plain Craft Launcher 2.exe");
                if (File.Exists(candidate) && PatchEngine.IsSupportedBuild(candidate)) return candidate;
            }
            catch { }

            var shortcutRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
            };
            foreach (var root in shortcutRoots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                try
                {
                    foreach (var lnk in Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileNameWithoutExtension(lnk);
                        if (name.IndexOf("PCL", StringComparison.OrdinalIgnoreCase) < 0 &&
                            name.IndexOf("Plain Craft", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var target = ResolveShortcut(lnk);
                        if (!string.IsNullOrEmpty(target) && File.Exists(target) &&
                            string.Equals(Path.GetFileName(target), "Plain Craft Launcher 2.exe", StringComparison.OrdinalIgnoreCase) &&
                            PatchEngine.IsSupportedBuild(target)) return target;
                    }
                }
                catch { }
            }

            var common = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PCL"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PCL"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PCL"),
                @"D:\PCL", @"D:\PCL2", @"C:\PCL", @"C:\PCL2", @"E:\PCL2"
            };
            foreach (var dir in common)
            {
                try
                {
                    var candidate = Path.Combine(dir, "Plain Craft Launcher 2.exe");
                    if (File.Exists(candidate) && PatchEngine.IsSupportedBuild(candidate)) return candidate;
                }
                catch { }
            }

            return null;
        }

        private static string ResolveShortcut(string lnkPath)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;
                var shell = Activator.CreateInstance(shellType);
                var shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                if (shortcut == null) return null;
                return shortcut.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
            }
            catch { return null; }
        }

        private void Browse()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择 Plain Craft Launcher 2.exe";
                dialog.Filter = "PCL 主程序|Plain Craft Launcher 2.exe|可执行文件|*.exe";
                if (dialog.ShowDialog(this) == DialogResult.OK) _pathBox.Text = dialog.FileName;
            }
        }

        private void Patch()
        {
            var path = _pathBox.Text.Trim().Trim('"');
            if (path.Length == 0)
            {
                SetStatus("请先选择 PCL 主程序。", Color.Firebrick);
                return;
            }

            try
            {
                var result = PatchEngine.Apply(path);
                if (result == "ALREADY_PATCHED")
                {
                    Log("这个文件已经是修补后的版本，无需重复操作。");
                    SetStatus("已经是修补后的版本。", Color.SeaGreen);
                    return;
                }
                if (result != null && result.StartsWith("VERSION_MISMATCH|"))
                {
                    var parts = result.Split('|');
                    Log("错误：目标文件不是受支持的 PCL 2.99.0.0 官方构建。");
                    Log("  文件大小：" + parts[1] + "（期望 " + PatchInfo.FileSize + "）");
                    Log("  SHA-256：" + parts[2]);
                    Log("为避免改坏文件，已停止。");
                    SetStatus("版本不匹配，已停止。", Color.Firebrick);
                    return;
                }
                if (result == "BYTES_MISMATCH")
                {
                    Log("错误：关键字节与预期不符，可能已被其他工具修改。已停止。");
                    SetStatus("关键字节不符，已停止。", Color.Firebrick);
                    return;
                }
                if (result != null && result.StartsWith("VERIFY_FAILED|"))
                {
                    Log("写入后的校验值异常：" + result.Split('|')[1]);
                    Log("请使用“还原备份”恢复，或手动把 " + path + ".pcl299.bak 复制回去。");
                    SetStatus("校验失败。", Color.Firebrick);
                    return;
                }
                if (result != null)
                {
                    Log("失败：" + result);
                    SetStatus("失败。", Color.Firebrick);
                    return;
                }

                Log("已备份原文件：" + path + ".pcl299.bak");
                Log("修补完成，校验通过。");
                Log("  新 SHA-256：" + PatchInfo.PatchedSha256);
                Log("现在可以启动 PCL，进入“下载”页查看原有的版本与加载器列表。");
                SetStatus("修补成功。", Color.SeaGreen);
            }
            catch (IOException ex)
            {
                Log("写入失败：" + ex.Message);
                Log("请先完全退出 PCL（包括托盘图标）后重试。");
                SetStatus("写入失败，请关闭 PCL。", Color.Firebrick);
            }
            catch (UnauthorizedAccessException)
            {
                Log("写入失败：没有权限（PCL 可能装在 Program Files 等受保护目录）。");
                SetStatus("没有写入权限。", Color.Firebrick);
                if (MessageBox.Show(this, "没有权限写入这个文件。\r\n\r\n是否以管理员身份重新打开补丁，然后重试？",
                    "需要管理员权限", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        var psi = new ProcessStartInfo(Application.ExecutablePath, "\"" + path + "\"");
                        psi.UseShellExecute = true;
                        psi.Verb = "runas";
                        Process.Start(psi);
                        Close();
                    }
                    catch (Exception ex) { Log("以管理员身份重启失败：" + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                Log("发生错误：" + ex.Message);
                SetStatus("发生错误。", Color.Firebrick);
            }
        }

        private void Restore()
        {
            var path = _pathBox.Text.Trim().Trim('"');
            try
            {
                var result = PatchEngine.Restore(path);
                if (result == "NO_BACKUP") { Log("没有找到备份：" + path + ".pcl299.bak"); SetStatus("没有备份可还原。", Color.Firebrick); return; }
                if (result != null && result.StartsWith("BACKUP_INVALID|")) { Log("备份文件的校验值不是官方版本，已停止还原。"); SetStatus("备份无效。", Color.Firebrick); return; }
                if (result != null) { Log("还原失败：" + result); SetStatus("还原失败。", Color.Firebrick); return; }
                Log("已还原为官方原版。");
                SetStatus("已还原。", Color.SeaGreen);
            }
            catch (Exception ex)
            {
                Log("还原失败：" + ex.Message);
                SetStatus("还原失败。", Color.Firebrick);
            }
        }

        private void SetStatus(string text, Color color)
        {
            _statusLabel.Text = text;
            _statusLabel.ForeColor = color;
        }

        private void Log(string text)
        {
            _logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length >= 1)
            {
                var command = args[0];
                if (command == "--silent" && args.Length >= 2) return RunSilent(args[1]);
                if (command == "--restore" && args.Length >= 2) return RunRestore(args[1]);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args.Length >= 1 ? args[0] : null));
            return 0;
        }

        private static int RunSilent(string path)
        {
            var logPath = path + ".patch.log";
            try
            {
                var result = PatchEngine.Apply(path);
                File.WriteAllText(logPath, (result == null ? "PATCHED " + PatchInfo.PatchedSha256 : result) + Environment.NewLine);
                return result == null ? 0 : (result == "ALREADY_PATCHED" ? 0 : 2);
            }
            catch (Exception ex)
            {
                File.WriteAllText(logPath, "ERROR " + ex.Message + Environment.NewLine);
                return 4;
            }
        }

        private static int RunRestore(string path)
        {
            var logPath = path + ".patch.log";
            try
            {
                var result = PatchEngine.Restore(path);
                File.WriteAllText(logPath, (result == null ? "RESTORED " + PatchInfo.OfficialSha256 : result) + Environment.NewLine);
                return result == null ? 0 : 2;
            }
            catch (Exception ex)
            {
                File.WriteAllText(logPath, "ERROR " + ex.Message + Environment.NewLine);
                return 4;
            }
        }
    }
}

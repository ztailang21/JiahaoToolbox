using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using JiahaoToolbox.Core;

namespace JiahaoToolbox.UI
{
    public sealed class GuardUpdatePage : Panel
    {
        private readonly ToolboxConfig _config;
        private readonly ToolTip _toolTip;
        private readonly List<Control> _actionControls = new List<Control>();
        private bool _loadingConfig;
        private bool _isBusy;
        private SmartAppControlState _lastSacState = SmartAppControlState.Unsupported;

        private Panel _scrollHost;
        private RoundedCardPanel _elevBanner;

        // Defender
        private StatusPill _defPill;
        private Label _lblTamperBadge;
        private FluentButton _btnDisableDef;
        private FluentButton _btnEnableDef;
        private FluentButton _btnAddExcl;

        // Windows Update
        private StatusPill _wuPill;
        private Label _lblStoreBadge;
        private FluentButton _btnDisableWu;
        private FluentButton _btnEnableWu;
        private FluentButton _btnRepairWu;
        private CheckBox _chkProtectServices;
        private CheckBox _chkHideTrayIcon;
        private CheckBox _chkDisableStore;

        // Smart App Control
        private StatusPill _sacPill;
        private FluentButton _btnDisableSac;
        private FluentButton _btnEvalSac;

        // Log
        private TextBox _txtLog;

        public event Action RequestElevateToGuard;
        public event Action<DefenderStatusInfo, WindowsUpdateStatusInfo, SmartAppControlStatusInfo> StatusesUpdated;

        public GuardUpdatePage(ToolboxConfig config)
        {
            _config = config;
            _toolTip = new ToolTip();
            _toolTip.InitialDelay = 250;
            _toolTip.AutoPopDelay = 12000;

            BackColor = Theme.ContentBg;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;

            BuildLayout();
            LoadConfigToUi();
        }

        private void BuildLayout()
        {
            Label lblTitle = new Label();
            lblTitle.Text = "防护与更新";
            lblTitle.Font = Theme.FontPageTitle();
            lblTitle.ForeColor = Theme.TextPrimary;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(26, 18);
            Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "管理 Microsoft Defender、Windows 自动更新与智能应用控制";
            lblSub.Font = Theme.FontPageSub();
            lblSub.ForeColor = Theme.TextMuted;
            lblSub.AutoSize = true;
            lblSub.Location = new Point(28, 50);
            Controls.Add(lblSub);

            FluentButton btnRefresh = FluentFactory.Button("刷新状态", 418, 20, 90, 32, FluentButtonStyle.Outline);
            btnRefresh.Font = Theme.FontSmall();
            btnRefresh.Click += async delegate { await RefreshStatusesAsync(true); };
            Controls.Add(btnRefresh);

            FluentButton btnRestoreDefault = FluentFactory.Button("一键恢复默认", 516, 20, 112, 32, FluentButtonStyle.SoftBlue);
            btnRestoreDefault.Font = Theme.FontSmall();
            btnRestoreDefault.Click += async delegate { await HandleRestoreAllAsync(); };
            Controls.Add(btnRestoreDefault);
            RegisterAction(btnRefresh, btnRestoreDefault);

            _scrollHost = new Panel();
            _scrollHost.Location = new Point(0, 74);
            _scrollHost.Size = new Size(654, 454);
            _scrollHost.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _scrollHost.BackColor = Theme.ContentBg;
            _scrollHost.AutoScroll = true;
            Controls.Add(_scrollHost);

            int y = 4;
            bool isAdmin = ElevationHelper.IsAdministrator();
            if (!isAdmin)
            {
                _elevBanner = new RoundedCardPanel();
                _elevBanner.CornerRadius = 10;
                _elevBanner.CardFill = Theme.AmberBg;
                _elevBanner.BorderColor = Theme.AmberBorder;
                _elevBanner.Location = new Point(26, y);
                _elevBanner.Size = new Size(588, 42);
                _scrollHost.Controls.Add(_elevBanner);

                Label lblWarn = new Label();
                lblWarn.Text = "当前为普通模式（仅可查看状态），修改系统配置需管理员权限";
                lblWarn.Font = Theme.FontSmall();
                lblWarn.ForeColor = Theme.AmberText;
                lblWarn.BackColor = Theme.AmberBg;
                lblWarn.AutoSize = true;
                lblWarn.Location = new Point(14, 13);
                _elevBanner.Controls.Add(lblWarn);

                FluentButton btnElev = FluentFactory.Button("以管理员身份重启", 434, 7, 142, 28, FluentButtonStyle.PrimaryBlue);
                btnElev.Font = new Font("Microsoft YaHei UI", 8.25F, FontStyle.Bold);
                btnElev.Click += delegate { if (RequestElevateToGuard != null) RequestElevateToGuard(); };
                _elevBanner.Controls.Add(btnElev);

                y += 52;
            }

            // Card 1: Microsoft Defender
            RoundedCardPanel cardDef = new RoundedCardPanel();
            cardDef.Location = new Point(26, y);
            cardDef.Size = new Size(588, 106);
            _scrollHost.Controls.Add(cardDef);

            Label lblDefTitle = new Label();
            lblDefTitle.Text = "Microsoft Defender（微软防病毒）";
            lblDefTitle.Font = Theme.FontBodyBold();
            lblDefTitle.ForeColor = Theme.TextPrimary;
            lblDefTitle.BackColor = Color.White;
            lblDefTitle.AutoSize = true;
            lblDefTitle.Location = new Point(16, 11);
            cardDef.Controls.Add(lblDefTitle);

            _lblTamperBadge = new Label();
            _lblTamperBadge.Text = "篡改保护：检测中";
            _lblTamperBadge.Font = Theme.FontSmall();
            _lblTamperBadge.ForeColor = Theme.TextMuted;
            _lblTamperBadge.BackColor = Color.White;
            _lblTamperBadge.TextAlign = ContentAlignment.MiddleRight;
            _lblTamperBadge.Location = new Point(374, 11);
            _lblTamperBadge.Size = new Size(198, 18);
            cardDef.Controls.Add(_lblTamperBadge);

            _defPill = new StatusPill();
            _defPill.Location = new Point(16, 34);
            _defPill.Size = new Size(556, 26);
            _defPill.PillText = "正在检测 Defender 状态...";
            cardDef.Controls.Add(_defPill);

            _btnDisableDef = FluentFactory.Button("停用 Defender", 16, 68, 118, 28, FluentButtonStyle.SoftDanger);
            _btnDisableDef.Font = Theme.FontSmall();
            _btnDisableDef.Click += async delegate { await HandleDisableDefenderAsync(); };
            cardDef.Controls.Add(_btnDisableDef);

            _btnEnableDef = FluentFactory.Button("启用 Defender", 142, 68, 118, 28, FluentButtonStyle.SoftSuccess);
            _btnEnableDef.Font = Theme.FontSmall();
            _btnEnableDef.Click += async delegate { await HandleEnableDefenderAsync(); };
            cardDef.Controls.Add(_btnEnableDef);

            FluentButton btnSecCenter = FluentFactory.Button("安全中心", 378, 68, 90, 28, FluentButtonStyle.Outline);
            btnSecCenter.Font = Theme.FontSmall();
            btnSecCenter.Click += delegate { DefenderManager.OpenSecurityCenter(); };
            cardDef.Controls.Add(btnSecCenter);

            _btnAddExcl = FluentFactory.Button("加入排除项", 476, 68, 96, 28, FluentButtonStyle.Outline);
            _btnAddExcl.Font = Theme.FontSmall();
            _btnAddExcl.Click += async delegate { await HandleAddExclusionAsync(); };
            cardDef.Controls.Add(_btnAddExcl);
            RegisterAction(_btnDisableDef, _btnEnableDef, _btnAddExcl);

            y += 116;

            // Card 2: Windows Update
            RoundedCardPanel cardWu = new RoundedCardPanel();
            cardWu.Location = new Point(26, y);
            cardWu.Size = new Size(588, 178);
            _scrollHost.Controls.Add(cardWu);

            Label lblWuTitle = new Label();
            lblWuTitle.Text = "Windows 自动更新（Windows Update）";
            lblWuTitle.Font = Theme.FontBodyBold();
            lblWuTitle.ForeColor = Theme.TextPrimary;
            lblWuTitle.BackColor = Color.White;
            lblWuTitle.AutoSize = true;
            lblWuTitle.Location = new Point(16, 11);
            cardWu.Controls.Add(lblWuTitle);

            _lblStoreBadge = new Label();
            _lblStoreBadge.Text = "微软商店服务：检测中";
            _lblStoreBadge.Font = Theme.FontSmall();
            _lblStoreBadge.ForeColor = Theme.TextMuted;
            _lblStoreBadge.BackColor = Color.White;
            _lblStoreBadge.TextAlign = ContentAlignment.MiddleRight;
            _lblStoreBadge.Location = new Point(366, 11);
            _lblStoreBadge.Size = new Size(206, 18);
            cardWu.Controls.Add(_lblStoreBadge);

            _wuPill = new StatusPill();
            _wuPill.Location = new Point(16, 34);
            _wuPill.Size = new Size(556, 26);
            _wuPill.PillText = "正在检测 Windows 更新状态...";
            cardWu.Controls.Add(_wuPill);

            _btnDisableWu = FluentFactory.Button("禁用更新", 16, 68, 108, 28, FluentButtonStyle.SoftDanger);
            _btnDisableWu.Font = Theme.FontSmall();
            _btnDisableWu.Click += async delegate { await HandleDisableWuAsync(); };
            cardWu.Controls.Add(_btnDisableWu);

            _btnEnableWu = FluentFactory.Button("启用更新", 132, 68, 108, 28, FluentButtonStyle.SoftSuccess);
            _btnEnableWu.Font = Theme.FontSmall();
            _btnEnableWu.Click += async delegate { await HandleEnableWuAsync(); };
            cardWu.Controls.Add(_btnEnableWu);

            _btnRepairWu = FluentFactory.Button("修复更新服务", 248, 68, 114, 28, FluentButtonStyle.SoftBlue);
            _btnRepairWu.Font = Theme.FontSmall();
            _btnRepairWu.Click += async delegate { await HandleRepairWuAsync(); };
            cardWu.Controls.Add(_btnRepairWu);

            _chkProtectServices = new CheckBox();
            _chkProtectServices.Text = "保护服务设置（强化锁定防自动恢复）";
            _chkProtectServices.AutoSize = true;
            _chkProtectServices.BackColor = Color.White;
            _chkProtectServices.ForeColor = Theme.TextPrimary;
            _chkProtectServices.Font = Theme.FontSmall();
            _chkProtectServices.Location = new Point(18, 102);
            _chkProtectServices.CheckedChanged += OnOptionChanged;
            cardWu.Controls.Add(_chkProtectServices);

            _chkHideTrayIcon = new CheckBox();
            _chkHideTrayIcon.Text = "隐藏任务栏更新图标";
            _chkHideTrayIcon.AutoSize = true;
            _chkHideTrayIcon.BackColor = Color.White;
            _chkHideTrayIcon.ForeColor = Theme.TextPrimary;
            _chkHideTrayIcon.Font = Theme.FontSmall();
            _chkHideTrayIcon.Location = new Point(328, 102);
            _chkHideTrayIcon.CheckedChanged += OnOptionChanged;
            cardWu.Controls.Add(_chkHideTrayIcon);

            _chkDisableStore = new CheckBox();
            _chkDisableStore.Text = "同时禁用微软商店安装服务（InstallService，默认不勾选以保留商店下载更新）";
            _chkDisableStore.AutoSize = true;
            _chkDisableStore.BackColor = Color.White;
            _chkDisableStore.ForeColor = Theme.TextSecondary;
            _chkDisableStore.Font = Theme.FontSmall();
            _chkDisableStore.Location = new Point(18, 123);
            _chkDisableStore.CheckedChanged += OnStoreOptionChanged;
            cardWu.Controls.Add(_chkDisableStore);

            FluentButton btnWuSettings = FluentFactory.Button("更新设置", 16, 146, 132, 24, FluentButtonStyle.Outline);
            btnWuSettings.Font = new Font("Microsoft YaHei UI", 8F);
            btnWuSettings.Click += delegate { WindowsUpdateManager.OpenUpdateSettings(); };
            cardWu.Controls.Add(btnWuSettings);

            FluentButton btnServices = FluentFactory.Button("系统服务", 157, 146, 132, 24, FluentButtonStyle.Outline);
            btnServices.Font = new Font("Microsoft YaHei UI", 8F);
            btnServices.Click += delegate { WindowsUpdateManager.OpenServicesConsole(); };
            cardWu.Controls.Add(btnServices);

            FluentButton btnTasks = FluentFactory.Button("计划任务", 298, 146, 132, 24, FluentButtonStyle.Outline);
            btnTasks.Font = new Font("Microsoft YaHei UI", 8F);
            btnTasks.Click += delegate { WindowsUpdateManager.OpenTaskScheduler(); };
            cardWu.Controls.Add(btnTasks);

            FluentButton btnTrouble = FluentFactory.Button("疑难解答", 440, 146, 132, 24, FluentButtonStyle.Outline);
            btnTrouble.Font = new Font("Microsoft YaHei UI", 8F);
            btnTrouble.Click += delegate { WindowsUpdateManager.OpenTroubleshooter(); };
            cardWu.Controls.Add(btnTrouble);

            RegisterAction(_btnDisableWu, _btnEnableWu, _btnRepairWu, _chkProtectServices, _chkHideTrayIcon, _chkDisableStore);

            y += 188;

            // Card 3: Smart App Control
            RoundedCardPanel cardSac = new RoundedCardPanel();
            cardSac.Location = new Point(26, y);
            cardSac.Size = new Size(588, 120);
            _scrollHost.Controls.Add(cardSac);

            Label lblSacTitle = new Label();
            lblSacTitle.Text = "智能应用控制（Smart App Control · Windows 11）";
            lblSacTitle.Font = Theme.FontBodyBold();
            lblSacTitle.ForeColor = Theme.TextPrimary;
            lblSacTitle.BackColor = Color.White;
            lblSacTitle.AutoSize = true;
            lblSacTitle.Location = new Point(16, 11);
            cardSac.Controls.Add(lblSacTitle);

            _sacPill = new StatusPill();
            _sacPill.Location = new Point(16, 34);
            _sacPill.Size = new Size(556, 26);
            _sacPill.PillText = "正在检测智能应用控制...";
            cardSac.Controls.Add(_sacPill);

            _btnDisableSac = FluentFactory.Button("关闭智能应用控制", 16, 68, 142, 28, FluentButtonStyle.SoftDanger);
            _btnDisableSac.Font = Theme.FontSmall();
            _btnDisableSac.Click += async delegate { await HandleSetSacAsync(0); };
            cardSac.Controls.Add(_btnDisableSac);

            _btnEvalSac = FluentFactory.Button("设为评估模式", 166, 68, 114, 28, FluentButtonStyle.SoftWarning);
            _btnEvalSac.Font = Theme.FontSmall();
            _btnEvalSac.Click += async delegate { await HandleSetSacAsync(2); };
            cardSac.Controls.Add(_btnEvalSac);

            FluentButton btnSacSettings = FluentFactory.Button("打开系统设置", 458, 68, 114, 28, FluentButtonStyle.Outline);
            btnSacSettings.Font = Theme.FontSmall();
            btnSacSettings.Click += delegate { SmartAppControlManager.OpenSmartAppSettings(); };
            cardSac.Controls.Add(btnSacSettings);

            Label lblSacHint = new Label();
            lblSacHint.Text = "提示：修改后需重启电脑生效；部分 Win11 版本关闭后系统设置界面内可能无法直接重新开启。";
            lblSacHint.Font = new Font("Microsoft YaHei UI", 7.8F);
            lblSacHint.ForeColor = Theme.TextMuted;
            lblSacHint.BackColor = Color.White;
            lblSacHint.AutoSize = true;
            lblSacHint.Location = new Point(16, 100);
            cardSac.Controls.Add(lblSacHint);

            RegisterAction(_btnDisableSac, _btnEvalSac);

            y += 130;

            // Card 4: Operation Log
            RoundedCardPanel cardLog = new RoundedCardPanel();
            cardLog.Location = new Point(26, y);
            cardLog.Size = new Size(588, 104);
            _scrollHost.Controls.Add(cardLog);

            Label lblLogTitle = new Label();
            lblLogTitle.Text = "操作日志";
            lblLogTitle.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold);
            lblLogTitle.ForeColor = Theme.TextSecondary;
            lblLogTitle.BackColor = Color.White;
            lblLogTitle.AutoSize = true;
            lblLogTitle.Location = new Point(16, 8);
            cardLog.Controls.Add(lblLogTitle);

            FluentButton btnCopyLog = FluentFactory.Button("复制", 472, 5, 48, 20, FluentButtonStyle.Outline);
            btnCopyLog.CornerRadius = 5;
            btnCopyLog.Font = new Font("Microsoft YaHei UI", 7.75F);
            btnCopyLog.Click += delegate
            {
                if (!string.IsNullOrEmpty(_txtLog.Text))
                {
                    try { Clipboard.SetText(_txtLog.Text); } catch { }
                    AppendLog("提示", "操作日志已复制到剪贴板。");
                }
            };
            cardLog.Controls.Add(btnCopyLog);

            FluentButton btnClearLog = FluentFactory.Button("清空", 524, 5, 48, 20, FluentButtonStyle.Outline);
            btnClearLog.CornerRadius = 5;
            btnClearLog.Font = new Font("Microsoft YaHei UI", 7.75F);
            btnClearLog.Click += delegate { _txtLog.Clear(); };
            cardLog.Controls.Add(btnClearLog);

            _txtLog = new TextBox();
            _txtLog.Multiline = true;
            _txtLog.ReadOnly = true;
            _txtLog.ScrollBars = ScrollBars.Vertical;
            _txtLog.WordWrap = true;
            _txtLog.BorderStyle = BorderStyle.FixedSingle;
            _txtLog.BackColor = Color.FromArgb(248, 250, 252);
            _txtLog.ForeColor = Theme.TextPrimary;
            _txtLog.Font = Theme.FontMono();
            _txtLog.Location = new Point(16, 30);
            _txtLog.Size = new Size(556, 64);
            cardLog.Controls.Add(_txtLog);

            // Bottom breathing padding inside scroll host
            Panel spacer = new Panel();
            spacer.Location = new Point(0, y + 108);
            spacer.Size = new Size(10, 12);
            spacer.BackColor = Theme.ContentBg;
            _scrollHost.Controls.Add(spacer);
        }

        private void RegisterAction(params Control[] controls)
        {
            for (int i = 0; i < controls.Length; i++) _actionControls.Add(controls[i]);
        }

        public void LoadConfigToUi()
        {
            _loadingConfig = true;
            try
            {
                _chkProtectServices.Checked = _config.ProtectServices;
                _chkHideTrayIcon.Checked = _config.HideTrayIcon;
                _chkDisableStore.Checked = _config.DisableStoreInstallService;
                UpdateStoreCheckboxVisual();
            }
            finally
            {
                _loadingConfig = false;
            }
        }

        private void OnOptionChanged(object sender, EventArgs e)
        {
            if (_loadingConfig) return;
            _config.ProtectServices = _chkProtectServices.Checked;
            _config.HideTrayIcon = _chkHideTrayIcon.Checked;
            _config.Save();
        }

        private void OnStoreOptionChanged(object sender, EventArgs e)
        {
            if (_loadingConfig) return;
            _config.DisableStoreInstallService = _chkDisableStore.Checked;
            _config.Save();
            UpdateStoreCheckboxVisual();
        }

        private void UpdateStoreCheckboxVisual()
        {
            _chkDisableStore.ForeColor = _chkDisableStore.Checked ? Theme.AmberText : Theme.TextSecondary;
        }

        private bool EnsureAdminBeforeModify(string actionName)
        {
            if (ElevationHelper.IsAdministrator()) return true;

            DialogResult dr = MessageBox.Show(
                FindForm(),
                string.Format("执行「{0}」需要管理员权限。\r\n\r\n是否立即以管理员身份重启「嘉豪工具箱」并自动回到「防护与更新」页面？", actionName),
                "需要管理员权限",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (dr == DialogResult.Yes && RequestElevateToGuard != null)
            {
                RequestElevateToGuard();
            }
            return false;
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            for (int i = 0; i < _actionControls.Count; i++)
            {
                _actionControls[i].Enabled = !busy;
            }
            if (!busy) ReapplySacAvailability();
        }

        private void ReapplySacAvailability()
        {
            if (_isBusy) return;
            bool supported = (_lastSacState != SmartAppControlState.Unsupported);
            _btnDisableSac.Enabled = supported;
            _btnEvalSac.Enabled = supported;
        }

        public void AppendLog(string level, string message)
        {
            if (_txtLog == null || _txtLog.IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, string>(AppendLog), level, message);
                return;
            }
            string line = string.Format("[{0:HH:mm:ss}] [{1}] {2}", DateTime.Now, level, message);
            if (_txtLog.TextLength > 0) _txtLog.AppendText(Environment.NewLine);
            _txtLog.AppendText(line);
            _txtLog.SelectionStart = _txtLog.TextLength;
            _txtLog.ScrollToCaret();
        }

        public async Task RefreshStatusesAsync(bool writeLog)
        {
            if (_isBusy) return;
            SetBusy(true);
            try
            {
                DefenderStatusInfo def = null;
                WindowsUpdateStatusInfo wu = null;
                SmartAppControlStatusInfo sac = null;

                await Task.Run(delegate
                {
                    def = DefenderManager.GetStatus();
                    wu = WindowsUpdateManager.GetStatus();
                    sac = SmartAppControlManager.GetStatus();
                });

                ApplyStatuses(def, wu, sac);
                if (writeLog && def != null && wu != null && sac != null)
                {
                    AppendLog("成功", "已刷新状态：Defender=" + def.SummaryText + " | 更新=" + wu.SummaryText + " | SAC=" + sac.SummaryText);
                }
            }
            catch (Exception ex)
            {
                AppendLog("失败", "检测系统状态异常: " + ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        public void ApplyStatuses(DefenderStatusInfo def, WindowsUpdateStatusInfo wu, SmartAppControlStatusInfo sac)
        {
            if (def != null)
            {
                _defPill.Tone = def.Tone;
                _defPill.PillText = def.SummaryText;
                _toolTip.SetToolTip(_defPill, def.DetailText);

                if (def.Tamper == TamperState.Enabled)
                {
                    _lblTamperBadge.Text = "篡改保护：已开启";
                    _lblTamperBadge.ForeColor = Theme.AmberText;
                }
                else if (def.Tamper == TamperState.Disabled)
                {
                    _lblTamperBadge.Text = "篡改保护：已关闭";
                    _lblTamperBadge.ForeColor = Theme.TextMuted;
                }
                else
                {
                    _lblTamperBadge.Text = "篡改保护：未知/无";
                    _lblTamperBadge.ForeColor = Theme.TextMuted;
                }
            }

            if (wu != null)
            {
                _wuPill.Tone = wu.Tone;
                _wuPill.PillText = wu.SummaryText;
                _toolTip.SetToolTip(_wuPill, wu.DetailText);

                if (wu.IsStoreInstallDisabled)
                {
                    _lblStoreBadge.Text = "微软商店服务：已禁用";
                    _lblStoreBadge.ForeColor = Theme.RedText;
                }
                else
                {
                    _lblStoreBadge.Text = "微软商店服务：正常";
                    _lblStoreBadge.ForeColor = Theme.GreenText;
                }
            }

            if (sac != null)
            {
                _lastSacState = sac.State;
                _sacPill.Tone = sac.Tone;
                _sacPill.PillText = sac.SummaryText;
                _toolTip.SetToolTip(_sacPill, sac.DetailText);
                ReapplySacAvailability();
            }

            if (StatusesUpdated != null) StatusesUpdated(def, wu, sac);
        }

        private async Task HandleDisableDefenderAsync()
        {
            if (_isBusy || !EnsureAdminBeforeModify("停用 Defender")) return;
            SetBusy(true);
            OperationResult res = null;
            try
            {
                AppendLog("执行", "开始停用 Microsoft Defender...");
                res = await Task.Run(() => DefenderManager.DisableDefender(AppendLog));
            }
            catch (Exception ex) { AppendLog("失败", ex.Message); }
            finally { SetBusy(false); }

            if (res != null && res.RequiresTamperDisable)
            {
                DialogResult dr = MessageBox.Show(
                    FindForm(),
                    "检测到系统已开启「篡改保护」，必须先在 Windows 安全中心手动关闭后才能停用 Defender。\r\n\r\n是否立即打开安全中心设置页面？",
                    "需先关闭篡改保护",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (dr == DialogResult.Yes) DefenderManager.OpenTamperProtectionSettings();
                return;
            }

            await RefreshStatusesAsync(false);
            if (res != null)
            {
                AppendLog(res.Success ? "完成" : "失败", res.Message);
                if (res.RebootRecommended)
                {
                    MessageBox.Show(FindForm(), res.Message, "操作完成（建议重启）", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private async Task HandleEnableDefenderAsync()
        {
            if (_isBusy || !EnsureAdminBeforeModify("启用 Defender")) return;
            SetBusy(true);
            OperationResult res = null;
            try
            {
                AppendLog("执行", "开始恢复启用 Microsoft Defender...");
                res = await Task.Run(() => DefenderManager.EnableDefender(AppendLog));
            }
            catch (Exception ex) { AppendLog("失败", ex.Message); }
            finally { SetBusy(false); }

            await RefreshStatusesAsync(false);
            if (res != null)
            {
                AppendLog(res.Success ? "完成" : "失败", res.Message);
                if (res.RebootRecommended)
                {
                    MessageBox.Show(FindForm(), res.Message, "操作完成（建议重启）", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private async Task HandleAddExclusionAsync()
        {
            if (_isBusy || !EnsureAdminBeforeModify("加入 Defender 排除项")) return;
            SetBusy(true);
            try
            {
                await Task.Run(() => DefenderManager.AddSelfToExclusions(AppendLog));
            }
            catch (Exception ex) { AppendLog("失败", ex.Message); }
            finally { SetBusy(false); }
        }

        private async Task HandleDisableWuAsync()
        {
            if (_isBusy || !EnsureAdminBeforeModify("禁用 Windows 更新")) return;
            WindowsUpdateOptions opts = new WindowsUpdateOptions();
            opts.ProtectServices = _chkProtectServices.Checked;
            opts.DisableStoreInstallService = _chkDisableStore.Checked;
            opts.HideTrayIcon = _chkHideTrayIcon.Checked;

            SetBusy(true);
            OperationResult res = null;
            try
            {
                AppendLog("执行", "开始禁用 Windows 自动更新（锁定=" + (opts.ProtectServices ? "开" : "关") + "，禁用微软商店=" + (opts.DisableStoreInstallService ? "是" : "否") + "）...");
                res = await Task.Run(() => WindowsUpdateManager.DisableUpdates(opts, AppendLog));
            }
            catch (Exception ex) { AppendLog("失败", ex.Message); }
            finally { SetBusy(false); }

            await RefreshStatusesAsync(false);
            if (res != null) AppendLog(res.Success ? "完成" : "失败", res.Message);
        }

        private async Task HandleEnableWuAsync()
        {
            if (_isBusy || !EnsureAdminBeforeModify("启用 Windows 更新")) return;
            SetBusy(true);
            OperationResult res = null;
            try
            {
                AppendLog("执行", "开始恢复启用 Windows 自动更新...");
                res = await Task.Run(() => WindowsUpdateManager.EnableUpdates(AppendLog));
            }
            catch (Exception ex) { AppendLog("失败", ex.Message); }
            finally { SetBusy(false); }

            await RefreshStatusesAsync(false);
            if (res != null) AppendLog(res.Success ? "完成" : "失败", res.Message);
        }

        private async Task HandleRepairWuAsync()
        {
            if (_isBusy || !EnsureAdminBeforeModify("修复 Windows 更新服务")) return;
            SetBusy(true);
            OperationResult res = null;
            try
            {
                AppendLog("执行", "开始全面修复 Windows 更新服务、权限与计划任务...");
                res = await Task.Run(() => WindowsUpdateManager.RepairServices(AppendLog));
            }
            catch (Exception ex) { AppendLog("失败", ex.Message); }
            finally { SetBusy(false); }

            await RefreshStatusesAsync(false);
            if (res != null) AppendLog(res.Success ? "完成" : "失败", res.Message);
        }

        private async Task HandleSetSacAsync(int targetValue)
        {
            if (_isBusy || !EnsureAdminBeforeModify("修改智能应用控制")) return;
            if (targetValue == 0)
            {
                DialogResult dr = MessageBox.Show(
                    FindForm(),
                    "确定要关闭「智能应用控制（Smart App Control）」吗？\r\n\r\n注意：\r\n1. 修改后需要重启电脑才能生效。\r\n2. 部分 Windows 11 版本关闭后可能无法直接在安全中心界面重新开启。",
                    "确认关闭智能应用控制",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (dr != DialogResult.Yes) return;
            }
            else
            {
                DialogResult dr = MessageBox.Show(
                    FindForm(),
                    "将尝试把智能应用控制策略设为「评估模式 (2)」，需重启电脑验证是否生效（取决于当前 Win11 版本限制）。\r\n\r\n是否继续？",
                    "设为评估模式",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                if (dr != DialogResult.Yes) return;
            }

            SetBusy(true);
            OperationResult res = null;
            try
            {
                res = await Task.Run(() => SmartAppControlManager.SetPolicyState(targetValue, AppendLog));
            }
            catch (Exception ex) { AppendLog("失败", ex.Message); }
            finally { SetBusy(false); }

            await RefreshStatusesAsync(false);
            if (res != null)
            {
                MessageBox.Show(FindForm(), res.Message, res.Success ? "操作完成（需重启生效）" : "操作提示",
                    MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
        }

        private async Task HandleRestoreAllAsync()
        {
            if (_isBusy || !EnsureAdminBeforeModify("一键恢复默认配置")) return;
            DialogResult dr = MessageBox.Show(
                FindForm(),
                "即将一键恢复以下默认系统配置：\r\n• 启用并修复 Windows 自动更新（含微软商店安装服务）\r\n• 启用 Microsoft Defender 服务、驱动与策略\r\n\r\n（智能应用控制将保持现状不强行改动）\r\n\r\n是否继续？",
                "确认一键恢复默认",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            SetBusy(true);
            OperationResult defRes = null;
            try
            {
                AppendLog("执行", "开始一键恢复系统默认配置...");
                await Task.Run(() => WindowsUpdateManager.RepairServices(AppendLog));
                defRes = await Task.Run(() => DefenderManager.EnableDefender(AppendLog));
            }
            catch (Exception ex) { AppendLog("失败", ex.Message); }
            finally { SetBusy(false); }

            await RefreshStatusesAsync(false);
            string msg = "已完成默认配置恢复。" + (defRes != null && defRes.RebootRecommended ? "\r\n\r\n因 Defender 内核驱动需随开机引导加载，建议重启电脑以完全生效。" : "");
            AppendLog("完成", "一键恢复完成。");
            MessageBox.Show(FindForm(), msg, "一键恢复完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

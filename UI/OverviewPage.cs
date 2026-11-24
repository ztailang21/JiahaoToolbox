using System;
using System.Drawing;
using System.Windows.Forms;
using JiahaoToolbox.Core;

namespace JiahaoToolbox.UI
{
    public sealed class OverviewPage : Panel
    {
        private KeyValueRow _rowWatcher;
        private KeyValueRow _rowSelectedApps;
        private KeyValueRow _rowFgExempt;

        private KeyValueRow _rowDefender;
        private KeyValueRow _rowWindowsUpdate;
        private KeyValueRow _rowSac;

        private Label _lblFooterTip;

        public event Action NavigateToEco;
        public event Action NavigateToGuard;
        public event Action RequestRefreshAll;

        public OverviewPage()
        {
            BackColor = Theme.ContentBg;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            BuildLayout();
        }

        private void BuildLayout()
        {
            Label lblTitle = new Label();
            lblTitle.Text = "总览";
            lblTitle.Font = Theme.FontPageTitle();
            lblTitle.ForeColor = Theme.TextPrimary;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(26, 22);
            Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "快速查看后台效率与 Windows 状态";
            lblSub.Font = Theme.FontPageSub();
            lblSub.ForeColor = Theme.TextMuted;
            lblSub.AutoSize = true;
            lblSub.Location = new Point(28, 56);
            Controls.Add(lblSub);

            // Left Card: 效率管理 (faithful to mockup)
            RoundedCardPanel cardEco = new RoundedCardPanel();
            cardEco.Location = new Point(26, 94);
            cardEco.Size = new Size(292, 344);
            Controls.Add(cardEco);

            IconBadgeBox icoEco = new IconBadgeBox();
            icoEco.IconKind = NavIconKind.Leaf;
            icoEco.Location = new Point(22, 22);
            icoEco.Size = new Size(38, 38);
            cardEco.Controls.Add(icoEco);

            Label lblEcoTitle = new Label();
            lblEcoTitle.Text = "效率管理";
            lblEcoTitle.Font = Theme.FontCardTitle();
            lblEcoTitle.ForeColor = Theme.TextPrimary;
            lblEcoTitle.BackColor = Color.White;
            lblEcoTitle.AutoSize = true;
            lblEcoTitle.Location = new Point(22, 74);
            cardEco.Controls.Add(lblEcoTitle);

            Label lblEcoSub = new Label();
            lblEcoSub.Text = "为选定应用管理后台效率模式";
            lblEcoSub.Font = Theme.FontCardSub();
            lblEcoSub.ForeColor = Theme.TextMuted;
            lblEcoSub.BackColor = Color.White;
            lblEcoSub.AutoSize = true;
            lblEcoSub.Location = new Point(22, 100);
            cardEco.Controls.Add(lblEcoSub);

            _rowWatcher = new KeyValueRow();
            _rowWatcher.LabelText = "自动监控";
            _rowWatcher.Location = new Point(22, 136);
            _rowWatcher.Size = new Size(248, 40);
            _rowWatcher.SetValue("检测中", StatusTone.Gray, true);
            cardEco.Controls.Add(_rowWatcher);

            _rowSelectedApps = new KeyValueRow();
            _rowSelectedApps.LabelText = "已选应用";
            _rowSelectedApps.Location = new Point(22, 176);
            _rowSelectedApps.Size = new Size(248, 40);
            _rowSelectedApps.SetValue("0 个", StatusTone.Gray, false);
            cardEco.Controls.Add(_rowSelectedApps);

            _rowFgExempt = new KeyValueRow();
            _rowFgExempt.LabelText = "前台自动豁免";
            _rowFgExempt.Location = new Point(22, 216);
            _rowFgExempt.Size = new Size(248, 40);
            _rowFgExempt.SetValue("开启", StatusTone.Gray, false);
            cardEco.Controls.Add(_rowFgExempt);

            FluentButton btnManageEco = FluentFactory.Button("管理应用  →", 22, 282, 122, 38, FluentButtonStyle.PrimaryBlue);
            btnManageEco.Font = new Font("Microsoft YaHei UI", 9.25F, FontStyle.Bold);
            btnManageEco.Click += delegate { if (NavigateToEco != null) NavigateToEco(); };
            cardEco.Controls.Add(btnManageEco);

            // Right Card: 防护与更新 (faithful to mockup)
            RoundedCardPanel cardGuard = new RoundedCardPanel();
            cardGuard.Location = new Point(336, 94);
            cardGuard.Size = new Size(292, 344);
            Controls.Add(cardGuard);

            IconBadgeBox icoGuard = new IconBadgeBox();
            icoGuard.IconKind = NavIconKind.Shield;
            icoGuard.Location = new Point(22, 22);
            icoGuard.Size = new Size(38, 38);
            cardGuard.Controls.Add(icoGuard);

            Label lblGuardTitle = new Label();
            lblGuardTitle.Text = "防护与更新";
            lblGuardTitle.Font = Theme.FontCardTitle();
            lblGuardTitle.ForeColor = Theme.TextPrimary;
            lblGuardTitle.BackColor = Color.White;
            lblGuardTitle.AutoSize = true;
            lblGuardTitle.Location = new Point(22, 74);
            cardGuard.Controls.Add(lblGuardTitle);

            Label lblGuardSub = new Label();
            lblGuardSub.Text = "查看安全防护及系统更新状态";
            lblGuardSub.Font = Theme.FontCardSub();
            lblGuardSub.ForeColor = Theme.TextMuted;
            lblGuardSub.BackColor = Color.White;
            lblGuardSub.AutoSize = true;
            lblGuardSub.Location = new Point(22, 100);
            cardGuard.Controls.Add(lblGuardSub);

            _rowDefender = new KeyValueRow();
            _rowDefender.LabelText = "Microsoft Defender";
            _rowDefender.Location = new Point(22, 136);
            _rowDefender.Size = new Size(248, 40);
            _rowDefender.SetValue("检测中", StatusTone.Gray, true);
            cardGuard.Controls.Add(_rowDefender);

            _rowWindowsUpdate = new KeyValueRow();
            _rowWindowsUpdate.LabelText = "Windows 更新";
            _rowWindowsUpdate.Location = new Point(22, 176);
            _rowWindowsUpdate.Size = new Size(248, 40);
            _rowWindowsUpdate.SetValue("检测中", StatusTone.Gray, true);
            cardGuard.Controls.Add(_rowWindowsUpdate);

            _rowSac = new KeyValueRow();
            _rowSac.LabelText = "智能应用控制";
            _rowSac.Location = new Point(22, 216);
            _rowSac.Size = new Size(248, 40);
            _rowSac.SetValue("检测中", StatusTone.Gray, false);
            cardGuard.Controls.Add(_rowSac);

            FluentButton btnViewGuard = FluentFactory.Button("查看状态  →", 22, 282, 122, 38, FluentButtonStyle.Outline);
            btnViewGuard.Font = new Font("Microsoft YaHei UI", 9.25F, FontStyle.Regular);
            btnViewGuard.Click += delegate { if (NavigateToGuard != null) NavigateToGuard(); };
            cardGuard.Controls.Add(btnViewGuard);

            // Subtle compact status strip at bottom
            RoundedCardPanel footerCard = new RoundedCardPanel();
            footerCard.CornerRadius = 10;
            footerCard.Location = new Point(26, 456);
            footerCard.Size = new Size(602, 48);
            Controls.Add(footerCard);

            _lblFooterTip = new Label();
            _lblFooterTip.Text = "正在同步系统状态...";
            _lblFooterTip.Font = Theme.FontSmall();
            _lblFooterTip.ForeColor = Theme.TextSecondary;
            _lblFooterTip.BackColor = Color.White;
            _lblFooterTip.Location = new Point(16, 15);
            _lblFooterTip.Size = new Size(468, 18);
            footerCard.Controls.Add(_lblFooterTip);

            FluentButton btnQuickRefresh = FluentFactory.Button("刷新状态", 494, 10, 92, 28, FluentButtonStyle.Outline);
            btnQuickRefresh.Font = Theme.FontSmall();
            btnQuickRefresh.Click += delegate { if (RequestRefreshAll != null) RequestRefreshAll(); };
            footerCard.Controls.Add(btnQuickRefresh);
        }

        public void UpdateEcoSummary(bool watcherActive, int enabledAppCount, bool fgExempt, int ecoActiveProcs, int trackedProcs)
        {
            if (watcherActive)
            {
                _rowWatcher.SetValue("运行中", StatusTone.Green, true);
            }
            else
            {
                _rowWatcher.SetValue("已停止", StatusTone.Gray, true);
            }

            _rowSelectedApps.SetValue(string.Format("{0} 个", enabledAppCount), StatusTone.Gray, false);
            _rowFgExempt.SetValue(fgExempt ? "开启" : "关闭", fgExempt ? StatusTone.Green : StatusTone.Gray, false);

            _ecoActiveCached = ecoActiveProcs;
            _trackedProcsCached = trackedProcs;
            RefreshFooterText();
        }

        private int _ecoActiveCached;
        private int _trackedProcsCached;
        private string _storeStatusCached = "正常";

        public void UpdateGuardSummary(DefenderStatusInfo def, WindowsUpdateStatusInfo wu, SmartAppControlStatusInfo sac)
        {
            if (def != null)
            {
                string txt = "未知";
                switch (def.State)
                {
                    case DefenderState.Running: txt = "已开启"; break;
                    case DefenderState.RealtimeOff: txt = "实时保护关闭"; break;
                    case DefenderState.Disabled: txt = "已停用"; break;
                    case DefenderState.StoppedButEnabled: txt = "待重启生效"; break;
                    case DefenderState.NotFound: txt = "未安装"; break;
                }
                _rowDefender.SetValue(txt, def.Tone, true);
            }

            if (wu != null)
            {
                string txt = "未知";
                switch (wu.State)
                {
                    case WindowsUpdateState.EnabledRunning:
                    case WindowsUpdateState.EnabledStopped:
                        txt = "已开启";
                        break;
                    case WindowsUpdateState.DisabledProtected:
                        txt = "已禁用(锁定)";
                        break;
                    case WindowsUpdateState.DisabledUnprotected:
                        txt = "已禁用";
                        break;
                    case WindowsUpdateState.NotFound:
                        txt = "服务缺失";
                        break;
                    default:
                        txt = "权限受限";
                        break;
                }
                _rowWindowsUpdate.SetValue(txt, wu.Tone, true);
                _storeStatusCached = wu.IsStoreInstallDisabled ? "已禁用" : "正常可用";
            }

            if (sac != null)
            {
                string txt = "不支持";
                bool colored = false;
                switch (sac.State)
                {
                    case SmartAppControlState.On: txt = "已开启"; colored = true; break;
                    case SmartAppControlState.Evaluation: txt = "评估模式"; colored = false; break;
                    case SmartAppControlState.Off: txt = "已关闭"; colored = true; break;
                    default: txt = "不支持"; colored = false; break;
                }
                _rowSac.SetValue(txt, sac.Tone, colored);
            }

            RefreshFooterText();
        }

        private void RefreshFooterText()
        {
            _lblFooterTip.Text = string.Format(
                "后台效率进程：{0} / {1} 个生效  ·  微软商店安装服务：{2}",
                _ecoActiveCached, _trackedProcsCached, _storeStatusCached);
        }
    }
}

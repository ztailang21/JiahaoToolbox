using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using JiahaoToolbox.Core;

namespace JiahaoToolbox.UI
{
    public sealed class RoundedCardPanel : Panel
    {
        public int CornerRadius { get; set; }
        public Color CardFill { get; set; }
        public Color BorderColor { get; set; }

        public RoundedCardPanel()
        {
            CornerRadius = 12;
            CardFill = Theme.CardBg;
            BorderColor = Theme.CardBorder;
            BackColor = Theme.ContentBg;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.ContentBg);

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = VectorIcons.CreateRoundedRect(rect, CornerRadius))
            {
                using (SolidBrush br = new SolidBrush(CardFill))
                {
                    g.FillPath(br, path);
                }
                using (Pen pen = new Pen(BorderColor, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }
        }
    }

    public sealed class IconBadgeBox : Control
    {
        public NavIconKind IconKind { get; set; }
        public int CornerRadius { get; set; }
        public Color BadgeBackColor { get; set; }
        public Color IconColor { get; set; }

        public IconBadgeBox()
        {
            IconKind = NavIconKind.AppLogo;
            CornerRadius = 9;
            BadgeBackColor = Theme.IconBadgeBg;
            IconColor = Theme.BrandBlueHover;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = VectorIcons.CreateRoundedRect(rect, CornerRadius))
            using (SolidBrush br = new SolidBrush(BadgeBackColor))
            {
                g.FillPath(br, path);
            }

            float pad = Width * 0.22f;
            RectangleF iconRect = new RectangleF(pad, pad, Width - pad * 2f, Height - pad * 2f);
            VectorIcons.DrawIcon(g, IconKind, iconRect, IconColor, 1.6f);
        }
    }

    public sealed class SidebarNavItem : Control
    {
        private bool _active;
        private bool _hovered;

        public NavIconKind IconKind { get; set; }
        public string Title { get; set; }
        public string PageKey { get; set; }

        public bool Active
        {
            get { return _active; }
            set
            {
                if (_active != value)
                {
                    _active = value;
                    Invalidate();
                }
            }
        }

        public SidebarNavItem()
        {
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Theme.SidebarBg;
            Font = Theme.FontBody();
            Size = new Size(162, 38);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.SidebarBg);

            if (_active || _hovered)
            {
                Color bg = _active ? Theme.NavActiveBg : Theme.NavHoverBg;
                RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
                using (GraphicsPath path = VectorIcons.CreateRoundedRect(rect, 8f))
                using (SolidBrush br = new SolidBrush(bg))
                {
                    g.FillPath(br, path);
                }
            }

            Color fg = _active ? Theme.BrandBlueText : Theme.TextPrimary;
            Color iconCol = _active ? Theme.BrandBlueText : Theme.TextSecondary;

            float iconSize = 16f;
            RectangleF iconRect = new RectangleF(13f, (Height - iconSize) / 2f, iconSize, iconSize);
            VectorIcons.DrawIcon(g, IconKind, iconRect, iconCol, 1.5f);

            using (Font f = new Font("Microsoft YaHei UI", 9.5F, _active ? FontStyle.Bold : FontStyle.Regular))
            {
                Rectangle textRect = new Rectangle(38, 0, Width - 44, Height);
                TextRenderer.DrawText(
                    g,
                    Title ?? "",
                    f,
                    textRect,
                    fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }
    }

    public enum FluentButtonStyle
    {
        PrimaryBlue,
        Outline,
        SoftDanger,
        SoftSuccess,
        SoftWarning,
        SoftBlue
    }

    public sealed class FluentButton : Button
    {
        private bool _hovered;
        private bool _pressed;
        private FluentButtonStyle _style = FluentButtonStyle.Outline;

        public FluentButtonStyle ButtonStyle
        {
            get { return _style; }
            set
            {
                _style = value;
                Invalidate();
            }
        }

        public int CornerRadius { get; set; }

        public FluentButton()
        {
            CornerRadius = 8;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            _pressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            _pressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Color.White);

            Color fill;
            Color border;
            Color fg;
            ResolveColors(out fill, out border, out fg);

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = VectorIcons.CreateRoundedRect(rect, CornerRadius))
            {
                using (SolidBrush br = new SolidBrush(fill))
                {
                    g.FillPath(br, path);
                }
                if (border != Color.Transparent)
                {
                    using (Pen pen = new Pen(border, 1f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            Rectangle textRect = new Rectangle(4, 0, Width - 8, Height);
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                textRect,
                fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        private void ResolveColors(out Color fill, out Color border, out Color fg)
        {
            if (!Enabled)
            {
                fill = Color.FromArgb(241, 245, 249);
                border = Color.FromArgb(226, 232, 240);
                fg = Color.FromArgb(148, 163, 184);
                return;
            }

            switch (_style)
            {
                case FluentButtonStyle.PrimaryBlue:
                    fill = _pressed ? Theme.BrandBluePressed : (_hovered ? Theme.BrandBlueHover : Theme.BrandBlue);
                    border = fill;
                    fg = Color.White;
                    break;
                case FluentButtonStyle.SoftDanger:
                    fill = _pressed ? Color.FromArgb(254, 202, 202) : (_hovered ? Color.FromArgb(254, 226, 226) : Theme.RedBg);
                    border = Theme.RedBorder;
                    fg = Theme.RedText;
                    break;
                case FluentButtonStyle.SoftSuccess:
                    fill = _pressed ? Color.FromArgb(187, 247, 208) : (_hovered ? Color.FromArgb(220, 252, 231) : Theme.GreenBg);
                    border = Theme.GreenBorder;
                    fg = Theme.GreenText;
                    break;
                case FluentButtonStyle.SoftWarning:
                    fill = _pressed ? Color.FromArgb(253, 230, 138) : (_hovered ? Color.FromArgb(254, 243, 199) : Theme.AmberBg);
                    border = Theme.AmberBorder;
                    fg = Theme.AmberText;
                    break;
                case FluentButtonStyle.SoftBlue:
                    fill = _pressed ? Color.FromArgb(191, 219, 254) : (_hovered ? Color.FromArgb(219, 234, 254) : Color.FromArgb(239, 246, 255));
                    border = Color.FromArgb(147, 197, 253);
                    fg = Theme.BrandBlueText;
                    break;
                default: // Outline
                    fill = _pressed ? Color.FromArgb(226, 232, 240) : (_hovered ? Color.FromArgb(248, 250, 252) : Color.White);
                    border = Color.FromArgb(203, 213, 225);
                    fg = Theme.TextPrimary;
                    break;
            }
        }
    }

    public sealed class KeyValueRow : Control
    {
        private string _labelText = "";
        private string _valueText = "";
        private StatusTone _valueTone = StatusTone.Gray;
        private bool _useColoredValue;

        public string LabelText
        {
            get { return _labelText; }
            set { _labelText = value ?? ""; Invalidate(); }
        }

        public string ValueText
        {
            get { return _valueText; }
            set { _valueText = value ?? ""; Invalidate(); }
        }

        public bool ShowDivider { get; set; }

        public void SetValue(string text, StatusTone tone, bool colored)
        {
            _valueText = text ?? "";
            _valueTone = tone;
            _useColoredValue = colored;
            Invalidate();
        }

        public KeyValueRow()
        {
            ShowDivider = true;
            DoubleBuffered = true;
            BackColor = Color.White;
            Height = 36;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);

            using (Font fLabel = new Font("Microsoft YaHei UI", 9.25F, FontStyle.Regular))
            using (Font fVal = new Font("Microsoft YaHei UI", 9.25F, _useColoredValue ? FontStyle.Bold : FontStyle.Regular))
            {
                Size valSize = TextRenderer.MeasureText(g, _valueText, fVal, new Size(Width, Height), TextFormatFlags.NoPrefix);
                int valW = Math.Min(Width - 60, Math.Max(64, valSize.Width + 6));
                int labelW = Math.Max(40, Width - valW - 4);

                Rectangle leftRect = new Rectangle(0, 0, labelW, Height - 1);
                Rectangle rightRect = new Rectangle(Width - valW, 0, valW, Height - 1);

                TextRenderer.DrawText(g, _labelText, fLabel, leftRect, Theme.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                Color valColor = Theme.TextPrimary;
                if (_useColoredValue)
                {
                    switch (_valueTone)
                    {
                        case StatusTone.Green: valColor = Theme.GreenText; break;
                        case StatusTone.Red: valColor = Theme.RedText; break;
                        case StatusTone.Amber: valColor = Theme.AmberText; break;
                        default: valColor = Theme.GrayText; break;
                    }
                }

                TextRenderer.DrawText(g, _valueText, fVal, rightRect, valColor,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }

            if (ShowDivider)
            {
                using (Pen p = new Pen(Theme.RowHairline, 1f))
                {
                    g.DrawLine(p, 0, Height - 1, Width, Height - 1);
                }
            }
        }
    }

    public sealed class StatusPill : Control
    {
        private StatusTone _tone = StatusTone.Gray;
        private string _pillText = "";

        public StatusTone Tone
        {
            get { return _tone; }
            set { _tone = value; Invalidate(); }
        }

        public string PillText
        {
            get { return _pillText; }
            set { _pillText = value ?? ""; Invalidate(); }
        }

        public StatusPill()
        {
            DoubleBuffered = true;
            Height = 24;
            Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Color.White);

            Color bg, border, dot, fg;
            switch (_tone)
            {
                case StatusTone.Green:
                    bg = Theme.GreenBg; border = Theme.GreenBorder; dot = Color.FromArgb(22, 163, 74); fg = Theme.GreenText; break;
                case StatusTone.Red:
                    bg = Theme.RedBg; border = Theme.RedBorder; dot = Color.FromArgb(220, 38, 38); fg = Theme.RedText; break;
                case StatusTone.Amber:
                    bg = Theme.AmberBg; border = Theme.AmberBorder; dot = Color.FromArgb(217, 119, 6); fg = Theme.AmberText; break;
                default:
                    bg = Theme.GrayBg; border = Theme.GrayBorder; dot = Color.FromArgb(100, 116, 139); fg = Theme.GrayText; break;
            }

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath gp = VectorIcons.CreateRoundedRect(rect, Height / 2f))
            {
                using (SolidBrush br = new SolidBrush(bg)) g.FillPath(br, gp);
                using (Pen pen = new Pen(border, 1f)) g.DrawPath(pen, gp);
            }

            int dotSize = 7;
            using (SolidBrush db = new SolidBrush(dot))
            {
                g.FillEllipse(db, 10, (Height - dotSize) / 2, dotSize, dotSize);
            }

            Rectangle textRect = new Rectangle(22, 0, Width - 28, Height);
            TextRenderer.DrawText(g, _pillText, Font, textRect, fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    public static class FluentFactory
    {
        public static FluentButton Button(string text, int x, int y, int w, int h, FluentButtonStyle style)
        {
            FluentButton b = new FluentButton();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, h);
            b.ButtonStyle = style;
            return b;
        }
    }
}

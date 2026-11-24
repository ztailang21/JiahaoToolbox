using System.Drawing;

namespace JiahaoToolbox.UI
{
    public static class Theme
    {
        // Canvas & Surfaces (derived from Fluent mockup)
        public static readonly Color HeaderBg = Color.White;
        public static readonly Color SidebarBg = Color.FromArgb(241, 245, 251);
        public static readonly Color ContentBg = Color.FromArgb(247, 249, 252);
        public static readonly Color CardBg = Color.White;
        public static readonly Color Divider = Color.FromArgb(229, 233, 240);
        public static readonly Color CardBorder = Color.FromArgb(226, 232, 240);
        public static readonly Color RowHairline = Color.FromArgb(238, 242, 246);

        // Brand & Navigation Accents
        public static readonly Color BrandBlue = Color.FromArgb(22, 82, 184);
        public static readonly Color BrandBlueHover = Color.FromArgb(29, 95, 214);
        public static readonly Color BrandBluePressed = Color.FromArgb(18, 67, 152);
        public static readonly Color BrandBlueText = Color.FromArgb(29, 78, 216);
        public static readonly Color IconBadgeBg = Color.FromArgb(220, 234, 254);
        public static readonly Color NavActiveBg = Color.FromArgb(219, 234, 254);
        public static readonly Color NavHoverBg = Color.FromArgb(232, 239, 250);

        // Text Hierarchy
        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        public static readonly Color TextSecondary = Color.FromArgb(71, 85, 105);
        public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);

        // Status Semantics
        public static readonly Color GreenText = Color.FromArgb(21, 128, 61);
        public static readonly Color GreenBg = Color.FromArgb(240, 253, 244);
        public static readonly Color GreenBorder = Color.FromArgb(187, 247, 208);

        public static readonly Color RedText = Color.FromArgb(185, 28, 28);
        public static readonly Color RedBg = Color.FromArgb(254, 242, 242);
        public static readonly Color RedBorder = Color.FromArgb(254, 202, 202);

        public static readonly Color AmberText = Color.FromArgb(180, 83, 9);
        public static readonly Color AmberBg = Color.FromArgb(255, 251, 235);
        public static readonly Color AmberBorder = Color.FromArgb(253, 230, 138);

        public static readonly Color GrayText = Color.FromArgb(100, 116, 139);
        public static readonly Color GrayBg = Color.FromArgb(241, 245, 249);
        public static readonly Color GrayBorder = Color.FromArgb(203, 213, 225);

        // Typography
        public static Font FontAppTitle() { return new Font("Microsoft YaHei UI", 11F, FontStyle.Bold); }
        public static Font FontPageTitle() { return new Font("Microsoft YaHei UI", 15.5F, FontStyle.Bold); }
        public static Font FontPageSub() { return new Font("Microsoft YaHei UI", 9.25F, FontStyle.Regular); }
        public static Font FontCardTitle() { return new Font("Microsoft YaHei UI", 10.75F, FontStyle.Bold); }
        public static Font FontCardSub() { return new Font("Microsoft YaHei UI", 8.75F, FontStyle.Regular); }
        public static Font FontBody() { return new Font("Microsoft YaHei UI", 9F, FontStyle.Regular); }
        public static Font FontBodyBold() { return new Font("Microsoft YaHei UI", 9F, FontStyle.Bold); }
        public static Font FontSmall() { return new Font("Microsoft YaHei UI", 8.25F, FontStyle.Regular); }
        public static Font FontMono() { return new Font("Consolas", 8.5F, FontStyle.Regular); }
    }
}

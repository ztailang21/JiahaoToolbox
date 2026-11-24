using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace JiahaoToolbox.UI
{
    public enum NavIconKind
    {
        AppLogo,
        Overview,
        Leaf,
        Shield,
        Settings
    }

    public static class VectorIcons
    {
        public static GraphicsPath CreateRoundedRect(RectangleF r, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d <= 0.1f)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void DrawIcon(Graphics g, NavIconKind kind, RectangleF bounds, Color color, float strokeWidth)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(color, strokeWidth))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                float x = bounds.X;
                float y = bounds.Y;
                float w = bounds.Width;
                float h = bounds.Height;

                switch (kind)
                {
                    case NavIconKind.AppLogo:
                    {
                        RectangleF r = new RectangleF(x + w * 0.12f, y + h * 0.16f, w * 0.76f, h * 0.68f);
                        using (GraphicsPath gp = CreateRoundedRect(r, w * 0.12f))
                        {
                            g.DrawPath(pen, gp);
                        }
                        float topBar = r.Y + r.Height * 0.34f;
                        float sideX = r.X + r.Width * 0.35f;
                        g.DrawLine(pen, r.X, topBar, r.Right, topBar);
                        g.DrawLine(pen, sideX, topBar, sideX, r.Bottom);
                        break;
                    }
                    case NavIconKind.Overview:
                    {
                        float gap = w * 0.12f;
                        float cell = (w * 0.76f - gap) / 2f;
                        float ox = x + w * 0.12f;
                        float oy = y + h * 0.12f;
                        float rad = Math.Max(1.5f, w * 0.08f);
                        RectangleF[] cells = new RectangleF[]
                        {
                            new RectangleF(ox, oy, cell, cell),
                            new RectangleF(ox + cell + gap, oy, cell, cell),
                            new RectangleF(ox, oy + cell + gap, cell, cell),
                            new RectangleF(ox + cell + gap, oy + cell + gap, cell, cell)
                        };
                        for (int i = 0; i < cells.Length; i++)
                        {
                            using (GraphicsPath gp = CreateRoundedRect(cells[i], rad))
                            {
                                g.DrawPath(pen, gp);
                            }
                        }
                        break;
                    }
                    case NavIconKind.Leaf:
                    {
                        // Organic clean leaf outline + midrib
                        using (GraphicsPath leaf = new GraphicsPath())
                        {
                            PointF tip = new PointF(x + w * 0.84f, y + h * 0.16f);
                            PointF basePt = new PointF(x + w * 0.22f, y + h * 0.78f);
                            leaf.AddBezier(
                                basePt,
                                new PointF(x + w * 0.14f, y + h * 0.36f),
                                new PointF(x + w * 0.44f, y + h * 0.14f),
                                tip);
                            leaf.AddBezier(
                                tip,
                                new PointF(x + w * 0.86f, y + h * 0.56f),
                                new PointF(x + w * 0.64f, y + h * 0.86f),
                                basePt);
                            g.DrawPath(pen, leaf);
                        }
                        // Stem + vein
                        g.DrawLine(pen, x + w * 0.15f, y + h * 0.85f, x + w * 0.62f, y + h * 0.38f);
                        break;
                    }
                    case NavIconKind.Shield:
                    {
                        using (GraphicsPath shield = new GraphicsPath())
                        {
                            PointF top = new PointF(x + w * 0.50f, y + h * 0.10f);
                            PointF rightTop = new PointF(x + w * 0.84f, y + h * 0.24f);
                            PointF rightMid = new PointF(x + w * 0.82f, y + h * 0.54f);
                            PointF bottom = new PointF(x + w * 0.50f, y + h * 0.89f);
                            PointF leftMid = new PointF(x + w * 0.18f, y + h * 0.54f);
                            PointF leftTop = new PointF(x + w * 0.16f, y + h * 0.24f);

                            shield.AddLine(top, rightTop);
                            shield.AddLine(rightTop, rightMid);
                            shield.AddBezier(
                                rightMid,
                                new PointF(x + w * 0.80f, y + h * 0.72f),
                                new PointF(x + w * 0.64f, y + h * 0.83f),
                                bottom);
                            shield.AddBezier(
                                bottom,
                                new PointF(x + w * 0.36f, y + h * 0.83f),
                                new PointF(x + w * 0.20f, y + h * 0.72f),
                                leftMid);
                            shield.AddLine(leftMid, leftTop);
                            shield.CloseFigure();
                            g.DrawPath(pen, shield);
                        }
                        break;
                    }
                    case NavIconKind.Settings:
                    {
                        float cx = x + w * 0.5f;
                        float cy = y + h * 0.5f;
                        float innerR = w * 0.15f;
                        float outerR = w * 0.31f;
                        float toothR = w * 0.41f;
                        g.DrawEllipse(pen, cx - innerR, cy - innerR, innerR * 2f, innerR * 2f);
                        g.DrawEllipse(pen, cx - outerR, cy - outerR, outerR * 2f, outerR * 2f);
                        for (int i = 0; i < 8; i++)
                        {
                            double ang = i * Math.PI / 4.0;
                            float x1 = cx + (float)(Math.Cos(ang) * outerR);
                            float y1 = cy + (float)(Math.Sin(ang) * outerR);
                            float x2 = cx + (float)(Math.Cos(ang) * toothR);
                            float y2 = cy + (float)(Math.Sin(ang) * toothR);
                            g.DrawLine(pen, x1, y1, x2, y2);
                        }
                        break;
                    }
                }
            }
        }
    }
}

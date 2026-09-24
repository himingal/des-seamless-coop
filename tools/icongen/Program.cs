using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var OUT = @"C:\Users\migue\AppData\Local\Temp\claude\C--\0ccd669e-b420-4c12-8617-5da22bd1d801\scratchpad\icons\";
Directory.CreateDirectory(OUT);

const int S = 4, W = 64, H = 96;          // draw at 4x, downscale to 64x96

Bitmap Draw(bool host)
{
    var big = new Bitmap(W * S, H * S, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(big);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.Clear(Color.Transparent);
    var rng = new Random(host ? 7 : 11);

    float cx = 128, cy = 196, rx = 100, ry = 150;
    // Irregular stone outline (tall talisman).
    var pts = new List<PointF>();
    for (int i = 0; i < 72; i++)
    {
        double a = i * Math.PI * 2 / 72;
        double wob = 1 + (rng.NextDouble() - 0.5) * 0.06;
        pts.Add(new PointF(cx + (float)(Math.Cos(a) * rx * wob), cy + (float)(Math.Sin(a) * ry * wob)));
    }
    using var stone = new GraphicsPath();
    stone.AddClosedCurve(pts.ToArray(), 0.4f);

    // Stone body gradient (warm bronze for host, cold slate for join).
    Color lit = host ? Color.FromArgb(255, 150, 118, 70) : Color.FromArgb(255, 88, 112, 140);
    Color dark = host ? Color.FromArgb(255, 38, 26, 14) : Color.FromArgb(255, 14, 20, 32);
    using (var pb = new PathGradientBrush(stone) { CenterColor = lit, SurroundColors = [dark], CenterPoint = new PointF(cx - 34, cy - 60) })
        g.FillPath(pb, stone);

    // Carved inner rim.
    using (var rimPen = new Pen(Color.FromArgb(150, 0, 0, 0), 10))
        g.DrawEllipse(rimPen, cx - rx * 0.72f, cy - ry * 0.72f, rx * 1.44f, ry * 1.44f);
    using (var rimHi = new Pen(host ? Color.FromArgb(90, 255, 214, 150) : Color.FromArgb(90, 170, 210, 255), 3))
        g.DrawEllipse(rimHi, cx - rx * 0.72f + 3, cy - ry * 0.72f + 3, rx * 1.44f, ry * 1.44f);

    // A couple of cracks.
    using (var crack = new Pen(Color.FromArgb(140, 0, 0, 0), 3))
    {
        g.DrawLines(crack, [new PointF(cx + 60, cy - 110), new PointF(cx + 42, cy - 80), new PointF(cx + 50, cy - 52)]);
        g.DrawLines(crack, [new PointF(cx - 70, cy + 96), new PointF(cx - 48, cy + 80), new PointF(cx - 56, cy + 58)]);
    }

    // Glowing rune.
    Color core = host ? Color.FromArgb(255, 255, 236, 170) : Color.FromArgb(255, 210, 245, 255);
    Color glow = host ? Color.FromArgb(255, 255, 150, 30) : Color.FromArgb(255, 40, 140, 255);
    using var rune = new GraphicsPath();
    if (host)
    {
        // Flame
        rune.AddBezier(cx, cy + 62, cx - 52, cy + 30, cx - 30, cy - 34, cx, cy - 58);
        rune.AddBezier(cx, cy - 58, cx + 30, cy - 34, cx + 52, cy + 30, cx, cy + 62);
        rune.CloseFigure();
        // Crown above the flame
        rune.AddPolygon([
            new PointF(cx - 40, cy - 70), new PointF(cx - 40, cy - 104), new PointF(cx - 22, cy - 84),
            new PointF(cx, cy - 116), new PointF(cx + 22, cy - 84), new PointF(cx + 40, cy - 104), new PointF(cx + 40, cy - 70)]);
    }
    else
    {
        // Portal ring
        rune.AddEllipse(cx - 46, cy - 46, 92, 92);
        rune.AddEllipse(cx - 30, cy - 30, 60, 60);
        // Converging chevrons
        rune.AddPolygon([new PointF(cx - 86, cy - 26), new PointF(cx - 56, cy), new PointF(cx - 86, cy + 26), new PointF(cx - 74, cy)]);
        rune.AddPolygon([new PointF(cx + 86, cy - 26), new PointF(cx + 56, cy), new PointF(cx + 86, cy + 26), new PointF(cx + 74, cy)]);
        // Center
        rune.AddEllipse(cx - 12, cy - 12, 24, 24);
        rune.FillMode = FillMode.Alternate;
    }
    // Bloom: wide soft strokes, then the core fill.
    for (int w = 34; w >= 6; w -= 7)
        using (var gp = new Pen(Color.FromArgb(28, glow), w) { LineJoin = LineJoin.Round })
            g.DrawPath(gp, rune);
    using (var fill = new SolidBrush(glow)) g.FillPath(fill, rune);
    using (var inner = new Pen(core, 3)) g.DrawPath(inner, rune);
    if (host)
    {
        // Bright inner flame core for depth.
        using var coreFlame = new GraphicsPath();
        coreFlame.AddBezier(cx, cy + 46, cx - 26, cy + 26, cx - 14, cy - 10, cx, cy - 28);
        coreFlame.AddBezier(cx, cy - 28, cx + 14, cy - 10, cx + 26, cy + 26, cx, cy + 46);
        coreFlame.CloseFigure();
        using var cb = new SolidBrush(Color.FromArgb(235, 255, 246, 214));
        g.FillPath(cb, coreFlame);
    }

    // Downscale 4x -> 64x96.
    var small = new Bitmap(W, H, PixelFormat.Format32bppArgb);
    using (var gs = Graphics.FromImage(small))
    {
        gs.InterpolationMode = InterpolationMode.HighQualityBicubic;
        gs.PixelOffsetMode = PixelOffsetMode.HighQuality;
        gs.CompositingMode = CompositingMode.SourceCopy;
        gs.DrawImage(big, new Rectangle(0, 0, W, H));
    }
    // Painterly grain on opaque pixels; keep the outer 1px ring transparent (sprite samples the inner 62x94).
    for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            var c = small.GetPixel(x, y);
            if (x == 0 || y == 0 || x == W - 1 || y == H - 1) { small.SetPixel(x, y, Color.Transparent); continue; }
            if (c.A == 0) continue;
            int n = rng.Next(-10, 11);
            small.SetPixel(x, y, Color.FromArgb(c.A, Math.Clamp(c.R + n, 0, 255), Math.Clamp(c.G + n, 0, 255), Math.Clamp(c.B + n, 0, 255)));
        }
    return small;
}

// ---------------- BC3 (DXT5) encoder ----------------
static ushort To565(float r, float g, float b) =>
    (ushort)((Math.Clamp((int)MathF.Round(r * 31 / 255), 0, 31) << 11) | (Math.Clamp((int)MathF.Round(g * 63 / 255), 0, 63) << 5) | Math.Clamp((int)MathF.Round(b * 31 / 255), 0, 31));
static (float, float, float) From565(ushort c) => ((c >> 11 & 31) * 255f / 31, (c >> 5 & 63) * 255f / 63, (c & 31) * 255f / 31);

byte[] EncodeBc3(Bitmap bmp)
{
    int bw = bmp.Width / 4, bh = bmp.Height / 4;
    var outb = new byte[bw * bh * 16];
    for (int by = 0; by < bh; by++)
        for (int bx = 0; bx < bw; bx++)
        {
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = bmp.GetPixel(bx * 4 + i % 4, by * 4 + i / 4);
            int o = (by * bw + bx) * 16;

            // Alpha (8-value mode when a0 > a1).
            byte amax = px.Max(p => p.A), amin = px.Min(p => p.A);
            var apal = new int[8];
            if (amax == amin) { outb[o] = amax; outb[o + 1] = amin; }
            else
            {
                outb[o] = amax; outb[o + 1] = amin;
                apal[0] = amax; apal[1] = amin;
                for (int i = 1; i < 7; i++) apal[i + 1] = ((7 - i) * amax + i * amin) / 7;
                ulong bits = 0;
                for (int i = 0; i < 16; i++)
                {
                    int best = 0, bd = int.MaxValue;
                    for (int k = 0; k < 8; k++) { int d = Math.Abs(apal[k] - px[i].A); if (d < bd) { bd = d; best = k; } }
                    bits |= (ulong)best << (3 * i);
                }
                for (int i = 0; i < 6; i++) outb[o + 2 + i] = (byte)(bits >> (8 * i));
            }

            // Color: principal axis over visible pixels, then least-squares refinement.
            var vis = px.Where(p => p.A > 8).Select(p => new[] { (float)p.R, p.G, p.B }).ToList();
            if (vis.Count == 0) continue; // c0=c1=0, indices 0
            float[] mean = [vis.Average(v => v[0]), vis.Average(v => v[1]), vis.Average(v => v[2])];
            var cov = new float[3, 3];
            foreach (var v in vis) for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) cov[a, b] += (v[a] - mean[a]) * (v[b] - mean[b]);
            float[] axis = [1, 1, 1];
            for (int it = 0; it < 8; it++)
            {
                var n = new float[3];
                for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) n[a] += cov[a, b] * axis[b];
                float len = MathF.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
                if (len < 1e-6f) break;
                axis = [n[0] / len, n[1] / len, n[2] / len];
            }
            float tmin = float.MaxValue, tmax = float.MinValue;
            foreach (var v in vis) { float t = (v[0] - mean[0]) * axis[0] + (v[1] - mean[1]) * axis[1] + (v[2] - mean[2]) * axis[2]; tmin = Math.Min(tmin, t); tmax = Math.Max(tmax, t); }
            float[] e0 = [mean[0] + axis[0] * tmax, mean[1] + axis[1] * tmax, mean[2] + axis[2] * tmax];
            float[] e1 = [mean[0] + axis[0] * tmin, mean[1] + axis[1] * tmin, mean[2] + axis[2] * tmin];
            for (int iter = 0; iter < 3; iter++)
            {
                // Assign t in {0,1/3,2/3,1}, then solve for e0,e1.
                float aa = 0, bb = 0, ab = 0; var ax = new float[3]; var bxv = new float[3];
                foreach (var v in vis)
                {
                    int best = 0; float bd = float.MaxValue;
                    for (int k = 0; k < 4; k++)
                    {
                        float t = k / 3f, d = 0;
                        for (int c = 0; c < 3; c++) { float p = e0[c] * (1 - t) + e1[c] * t - v[c]; d += p * p; }
                        if (d < bd) { bd = d; best = k; }
                    }
                    float tt = best / 3f, al = 1 - tt;
                    aa += al * al; bb += tt * tt; ab += al * tt;
                    for (int c = 0; c < 3; c++) { ax[c] += al * v[c]; bxv[c] += tt * v[c]; }
                }
                float det = aa * bb - ab * ab;
                if (MathF.Abs(det) < 1e-6f) break;
                for (int c = 0; c < 3; c++)
                {
                    e0[c] = Math.Clamp((bb * ax[c] - ab * bxv[c]) / det, 0, 255);
                    e1[c] = Math.Clamp((aa * bxv[c] - ab * ax[c]) / det, 0, 255);
                }
            }
            ushort c0 = To565(e0[0], e0[1], e0[2]), c1 = To565(e1[0], e1[1], e1[2]);
            if (c0 < c1) (c0, c1) = (c1, c0);
            var p0 = From565(c0); var p1 = From565(c1);
            var pal = new (float r, float g, float b)[4];
            pal[0] = p0; pal[1] = p1;
            pal[2] = ((2 * p0.Item1 + p1.Item1) / 3, (2 * p0.Item2 + p1.Item2) / 3, (2 * p0.Item3 + p1.Item3) / 3);
            pal[3] = ((p0.Item1 + 2 * p1.Item1) / 3, (p0.Item2 + 2 * p1.Item2) / 3, (p0.Item3 + 2 * p1.Item3) / 3);
            uint idx = 0;
            if (c0 != c1)
                for (int i = 0; i < 16; i++)
                {
                    int best = 0; float bd = float.MaxValue;
                    for (int k = 0; k < 4; k++) { float dr = pal[k].r - px[i].R, dg = pal[k].g - px[i].G, db = pal[k].b - px[i].B, d = dr * dr + dg * dg + db * db; if (d < bd) { bd = d; best = k; } }
                    idx |= (uint)best << (2 * i);
                }
            outb[o + 8] = (byte)c0; outb[o + 9] = (byte)(c0 >> 8);
            outb[o + 10] = (byte)c1; outb[o + 11] = (byte)(c1 >> 8);
            outb[o + 12] = (byte)idx; outb[o + 13] = (byte)(idx >> 8); outb[o + 14] = (byte)(idx >> 16); outb[o + 15] = (byte)(idx >> 24);
        }
    return outb;
}

Bitmap DecodeBc3(byte[] d, int w, int h)
{
    var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb); int bw = w / 4;
    for (int by = 0; by < h / 4; by++) for (int bx = 0; bx < bw; bx++)
    {
        int off = (by * bw + bx) * 16; byte a0 = d[off], a1 = d[off + 1]; ulong ab = 0; for (int i = 0; i < 6; i++) ab |= (ulong)d[off + 2 + i] << (8 * i);
        var al = new int[8]; al[0] = a0; al[1] = a1; if (a0 > a1) for (int i = 1; i < 7; i++) al[i + 1] = ((7 - i) * a0 + i * a1) / 7; else { for (int i = 1; i < 5; i++) al[i + 1] = ((5 - i) * a0 + i * a1) / 5; al[6] = 0; al[7] = 255; }
        ushort c0 = (ushort)(d[off + 8] | d[off + 9] << 8), c1 = (ushort)(d[off + 10] | d[off + 11] << 8); uint cb = (uint)(d[off + 12] | d[off + 13] << 8 | d[off + 14] << 16 | d[off + 15] << 24);
        var p0 = From565(c0); var p1 = From565(c1);
        var pal = new[] { p0, p1, ((2 * p0.Item1 + p1.Item1) / 3, (2 * p0.Item2 + p1.Item2) / 3, (2 * p0.Item3 + p1.Item3) / 3), ((p0.Item1 + 2 * p1.Item1) / 3, (p0.Item2 + 2 * p1.Item2) / 3, (p0.Item3 + 2 * p1.Item3) / 3) };
        for (int i = 0; i < 16; i++) { var c = pal[cb >> (2 * i) & 3]; bmp.SetPixel(bx * 4 + i % 4, by * 4 + i / 4, Color.FromArgb(al[ab >> (3 * i) & 7], (int)c.Item1, (int)c.Item2, (int)c.Item3)); }
    }
    return bmp;
}

var preview = new Bitmap(W * 4 * 2 + 24, H * 4 + 0, PixelFormat.Format32bppArgb);
using (var gp = Graphics.FromImage(preview)) gp.Clear(Color.FromArgb(255, 34, 30, 26));
int slot = 0;
foreach (var (name, host) in new[] { ("host", true), ("join", false) })
{
    var bmp = Draw(host);
    bmp.Save(OUT + $"{name}_src.png");
    var bc3 = EncodeBc3(bmp);
    File.WriteAllBytes(OUT + $"{name}.bc3", bc3);
    var back = DecodeBc3(bc3, W, H);
    using (var gp = Graphics.FromImage(preview)) { gp.InterpolationMode = InterpolationMode.NearestNeighbor; gp.PixelOffsetMode = PixelOffsetMode.Half; gp.DrawImage(back, new Rectangle(slot * (W * 4 + 24), 0, W * 4, H * 4)); }
    Console.WriteLine($"{name}: bc3 {bc3.Length} bytes");
    slot++;
}
preview.Save(OUT + "new_icons_preview.png");

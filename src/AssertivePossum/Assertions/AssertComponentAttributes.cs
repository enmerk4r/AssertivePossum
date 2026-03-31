using System.Drawing;
using System.Drawing.Drawing2D;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Assertions;

/// <summary>
/// Custom attributes for Assert components that draw a colored drop shadow
/// indicating pass (green) or fail (red) status.
/// </summary>
public class AssertComponentAttributes : GH_ComponentAttributes
{
    private const float ShadowOffsetX = 8f;
    private const float ShadowOffsetY = 8f;
    private const float ShadowCornerRadius = 6f;
    private const int ShadowAlpha = 80;

    private static readonly Color PassColor = Color.FromArgb(ShadowAlpha, 76, 175, 80);
    private static readonly Color FailColor = Color.FromArgb(ShadowAlpha, 229, 57, 53);

    public AssertComponentAttributes(GH_Component owner) : base(owner) { }

    protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
    {
        if (channel == GH_CanvasChannel.Objects)
        {
            var shadowColor = GetShadowColor();
            if (shadowColor.HasValue)
            {
                var oldSmoothing = graphics.SmoothingMode;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                var bounds = Bounds;
                var shadowRect = new RectangleF(
                    bounds.X + ShadowOffsetX,
                    bounds.Y + ShadowOffsetY,
                    bounds.Width,
                    bounds.Height);

                using var path = CreateRoundedRect(shadowRect, ShadowCornerRadius);
                using var brush = new SolidBrush(shadowColor.Value);
                graphics.FillPath(brush, path);

                graphics.SmoothingMode = oldSmoothing;
            }
        }

        base.Render(canvas, graphics, channel);
    }

    private Color? GetShadowColor()
    {
        if (Owner.Params.Output.Count == 0) return null;

        var output = Owner.Params.Output[0];
        if (output.VolatileDataCount == 0) return null;

        var data = output.VolatileData.AllData(false);
        foreach (var item in data)
        {
            if (item is TestResultGoo goo && goo.Value != null)
            {
                return goo.Value.Status switch
                {
                    TestStatus.Pass => PassColor,
                    TestStatus.Fail => FailColor,
                    _ => null
                };
            }
        }

        return null;
    }

    private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2f;

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }
}

using System;


namespace Unknown6656.Imaging.Rendering;


public class ANSIRenderer
    : Renderer
{
    public ANSIRenderer(RenderingOptions options) : base(options) => throw new NotImplementedException();
    protected override (int width, int height) GetOutputDimensions() => throw new NotImplementedException();
    protected override void RenderBitmap(RGBAColor[,] colors, RenderingOptions options_override) => throw new NotImplementedException();
}

﻿using System.Collections.Generic;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Drawing;
using System.Linq;
using System.IO;
using System;

using Unknown6656.Mathematics.LinearAlgebra;
using Unknown6656.Mathematics.Numerics;

using Random = Unknown6656.Mathematics.Numerics.Random;

namespace Unknown6656.Imaging.Effects;


/// <summary>
/// Represents an abstract color gradient effect.
/// A gradient effect generates a coordinate-based color, which is blended with the original bitmap using the <see cref="Blending"/>-property.
/// <para/>
/// Known implementations of this class are <see cref="ConstantColor"/>, <see cref="LinearGradient"/>, <see cref="RadialGradient"/>,
/// <see cref="MultiPointGradient"/>, <see cref="VoronoiGradient"/>, etc.
/// </summary>
/// <completionlist cref="Gradient"/>
public abstract class Gradient
    : CoordinateColorEffect
{
    public BlendMode Blending { get; set; } = BlendMode.Top;


    private protected abstract RGBAColor ProcessCoordinate(int x, int y, int w, int h);

    private protected sealed override RGBAColor ProcessCoordinate(int x, int y, int w, int h, RGBAColor source) =>
        RGBAColor.Blend(source, ProcessCoordinate(x, y, w, h), Blending);


    // TODO : add static methods
}

/// <summary>
/// An effect which fills a given bitmap with a specified <see cref="RGBAColor"/> using a specified <see cref="BlendMode"/>.
/// </summary>
public sealed class ConstantColor
    : Gradient
{
    public RGBAColor Color { get; }


    public ConstantColor(RGBAColor color) => Color = color;

    private protected override RGBAColor ProcessCoordinate(int x, int y, int w, int h) => Color;
}

public sealed class LinearGradient
    : Gradient
{
    public ColorMap Colors { get; }
    public Vector2 Start { get; }
    public Vector2 End { get; }

    private readonly Vector2 _to;
    private readonly Scalar _min;
    private readonly Scalar _max;


    public LinearGradient(Vector2 start, Vector2 end, params RGBAColor[] colors)
        : this(start, end, ColorMap.Uniform(colors))
    {
    }

    public LinearGradient(Vector2 start, Vector2 end, IEnumerable<RGBAColor> colors)
        : this(start, end, colors as RGBAColor[] ?? colors.ToArray())
    {
    }

    public LinearGradient(Vector2 start, Vector2 end, ColorMap colors)
    {
        if (colors is DiscreteColorMap { Size: 0 })
            throw new ArgumentException("The color map must not be empty.", nameof(colors));

        Colors = colors;
        Start = start;
        End = end;

        _to = End - Start;
        _min = _to * Start;
        _max = _to * End;
    }

    private protected override RGBAColor ProcessCoordinate(int x, int y, int w, int h)
    {
        Scalar progress = _to * (x, y);

        return Colors[progress <= _min ? 0 : progress >= _max ? 1 : (progress - _min) / (_max - _min)];
    }
}

public sealed class RadialGradient
    : Gradient
{
    public ColorMap Colors { get; }
    public Vector2? Center { get; }
    public Scalar? Size { get; }


    public RadialGradient(Vector2? center, Scalar? size, params RGBAColor[] colors)
        : this(center, size, ColorMap.Uniform(colors))
    {
    }

    public RadialGradient(Vector2? center, Scalar? size, IEnumerable<RGBAColor> colors)
        : this(center, size, colors as RGBAColor[] ?? colors.ToArray())
    {
    }

    public RadialGradient(Vector2? center, Scalar? size, ColorMap colors)
    {
        if (colors is DiscreteColorMap { Size: 0 })
            throw new ArgumentException("The color map must not be empty.", nameof(colors));
        else if (size is Scalar s && (s.IsNegative || !s.IsFinite))
            throw new ArgumentException("The size must either be null, zero, or a positive, finite number.", nameof(size));

        Colors = colors;
        Center = center;
        Size = size;
    }

    private protected override RGBAColor ProcessCoordinate(int x, int y, int w, int h)
    {
        Vector2 mid = new Vector2(w, h).Multiply(.5);
        Scalar dist = (Center ?? mid).DistanceTo((x, y));
        Scalar size = Size ?? mid.Length;

        return size > Scalar.ComputationalEpsilon ? Colors[dist, Scalar.Zero, size] : Colors[Scalar.One];
    }
}

public sealed class MultiPointGradient
    : Gradient
{
    public Scalar PowerParameter { set; get; } = Scalar.One;
    public VectorNorm VectorDistanceMetric { get; set; } = VectorNorm.EucledianNorm;
    public (Vector2 Position, RGBAColor Color)[] Points { get; }


    public MultiPointGradient(IEnumerable<(Vector2 Position, RGBAColor Color)> points)
        : this(points as (Vector2, RGBAColor)[] ?? points.ToArray())
    {
    }

    public MultiPointGradient(params (Vector2 Position, RGBAColor Color)[] points) =>
        Points = points.Length is 0 ? throw new ArgumentException("At least one point has to be provided.", nameof(points)) : points;

    private protected override RGBAColor ProcessCoordinate(int x, int y, int w, int h)
    {
        Vector2 pixel = (x, y);
        Vector4 color = Vector4.Zero;
        Scalar scale = Scalar.Zero;

        for (int i = 0; i < Points.Length; ++i)
        {
            (Vector2 pos, RGBAColor col) = Points[i];

            if (pos.Is(pixel))
                return col;

            Scalar f = pos.DistanceTo(in pixel, VectorDistanceMetric).Power(-PowerParameter);

            color += f * (Vector4)col;
            scale += f;
        }

        return color / scale;
    }
}

public sealed class HyperbolicGradient
    : Gradient
{
    public (Vector2 Position, RGBAColor Color) From { get; }
    public (Vector2 Position, RGBAColor Color) To { get; }


    public HyperbolicGradient((Vector2 Position, RGBAColor Color) from, (Vector2 Position, RGBAColor Color) to)
    {
        From = from;
        To = to;
    }

    private protected override RGBAColor ProcessCoordinate(int x, int y, int w, int h)
    {
        Scalar f = From.Position.Subtract((x, y)).Length;
        Scalar t = To.Position.Subtract((x, y)).Length;

        return RGBAColor.LinearInterpolate(From.Color, To.Color, f / (f + t));
    }
}

public sealed class VoronoiGradient
    : CoordinateColorEffect
{
    public VectorNorm VectorDistanceMetric { get; set; } = VectorNorm.EucledianNorm;
    public (Vector2 Position, RGBAColor Color)[] Points { get; }


    public VoronoiGradient(IEnumerable<(Vector2 Position, RGBAColor Color)> points)
        : this(points as (Vector2, RGBAColor)[] ?? points.ToArray())
    {
    }

    public VoronoiGradient(params (Vector2 Position, RGBAColor Color)[] points) =>
        Points = points.Length is 0 ? throw new ArgumentException("At least one point has to be provided.", nameof(points)) : points;

    private protected override RGBAColor ProcessCoordinate(int x, int y, int w, int h, RGBAColor source)
    {
        Vector2 pos = (x, y);

        return Points.OrderBy(p => p.Position.DistanceTo(in pos, VectorDistanceMetric)).FirstOrDefault().Color;
    }
}

public sealed class NoiseEffect
    : CoordinateColorEffect
{
    public NoiseMode Mode { get; init; } = NoiseMode.Regular;
    public Random RandomNumberGenerator { get; }

    public long Seed => RandomNumberGenerator.Seed;


    public NoiseEffect(Random rng)
        : this(rng, NoiseMode.Regular)
    {
    }

    public NoiseEffect(Random rng, NoiseMode mode)
    {
        RandomNumberGenerator = rng;
        Mode = mode;
    }

    private protected override RGBAColor ProcessCoordinate(int x, int y, int w, int h, RGBAColor source)
    {
        bool gray = Mode.HasFlag(NoiseMode.Grayscale);
        bool alpha = Mode.HasFlag(NoiseMode.AlphaNoise);
        RGBAColor c = new(RandomNumberGenerator.NextByte(), RandomNumberGenerator.NextByte(), RandomNumberGenerator.NextByte(), source.A);

        if (gray)
            c = new(c.R, c.R, c.R, c.A);

        if (alpha)
            c.A = RandomNumberGenerator.NextByte();

        return c;
    }
}

[Flags, Serializable]
public enum NoiseMode
    : byte
{
    Regular = 0,
    Grayscale = 1,
    AlphaNoise = 2,
}

public sealed class PerlinNoiseEffect
    : CoordinateColorEffect
{
    public PerlinNoise Noise { get; }


    public PerlinNoiseEffect(PerlinNoise noise) => Noise = noise;

    public PerlinNoiseEffect(PerlinNoiseSettings settings) => Noise = new PerlinNoise(settings);

    private protected override RGBAColor ProcessCoordinate(int x, int y, int w, int h, RGBAColor source)
    {
        Vector2 pos = new(x, y);
        double value = Noise[pos / Math.Max(Math.Max(w, h), 1)];

        return new(.5 * (1 + value), 1);
    }
}

public sealed class Duotone
    : Multitone
{
    public Duotone(RGBAColor tint)
        : this(RGBAColor.Black, tint)
    {
    }

    public Duotone(RGBAColor black, RGBAColor tint)
        : base(black, [], tint)
    {
    }
}

public sealed class Tritone
    : Multitone
{
    public Tritone(RGBAColor tint)
        : this(RGBAColor.Black, tint, RGBAColor.White)
    {
    }

    public Tritone(RGBAColor black, RGBAColor tint, RGBAColor white)
        : base(black, new[] { tint }, white)
    {
    }
}

public class Multitone
    : ColorEffect
{
    private readonly DiscreteColorMap _map;
    public RGBAColor Black { get; }
    public RGBAColor[] Tones { get; }
    public RGBAColor White { get; }


    public Multitone(params RGBAColor[] tones)
        : this(RGBAColor.Black, tones, RGBAColor.White)
    {
    }

    public Multitone(RGBAColor black, IEnumerable<RGBAColor>? tones, RGBAColor white)
    {
        Tones = tones as RGBAColor[] ?? tones?.ToArray() ?? [];
        Black = black;
        White = white;
        _map = ColorMap.Uniform(Tones.Prepend(black).Append(white).ToArray());
    }

    private protected sealed override RGBAColor ProcessColor(RGBAColor input) => _map[input.Average];
}

/// <summary>
/// Represents a grayscale bitmap color effect.
/// </summary>
public sealed class Grayscale
    : RGBColorEffect
{
    /// <summary>
    /// Creates a new instance
    /// </summary>
    public Grayscale()
        : this(Scalar.One)
    {
    }

    public Grayscale(Scalar amount)
        : base(new Func<Matrix3>(delegate
        {
            amount = amount.Clamp();

            return new Matrix3(
                1, amount, amount,
                amount, 1, amount,
                amount, amount, 1
            ) / (1 + 2 * amount);
        })())
    {
    }
}

/// <summary>
/// Represents an alpha-opacity bitmap effect.
/// </summary>
public sealed class Opacity
    : RGBAColorEffect
{
    /// <summary>
    /// Creates a new instance, which applies the current effect to the given amount
    /// </summary>
    /// <param name="amount">Amount [0..1]</param>
    public Opacity(Scalar amount)
        : base((
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, amount.Clamp()
        ))
    {
    }
}

/// <summary>
/// Represents an color inversion bitmap effect.
/// </summary>
public sealed class Invert
    : ColorEffect.Delegated
{
    /// <summary>
    /// Creates a new instance
    /// </summary>
    public Invert()
        : base(c => c.Complement)
    {
    }
}

public sealed class Brightness
    : RGBColorEffect
{
    public Brightness(Scalar amount)
        : base((
            amount, 0, 0,
            0, amount, 0,
            0, 0, amount
        ))
    {
    }
}

public sealed class Saturation
    : RGBColorEffect
{
    public Saturation(Scalar amount)
        : base(new Func<Matrix3>(() =>
        {
            amount = amount.Max(0);
            (Scalar r, Scalar g, Scalar b) = (1 - amount) * new Vector3(.3086, .6094, .0820);

            return (
                r + amount, g, b,
                r, g + amount, b,
                r, g, b + amount
            );
        })())
    {
    }
}

public sealed class Contrast
    : RGBColorEffect
{
    public Contrast(Scalar amount)
        : base(new Func<Matrix3>(() =>
        {
            Scalar c = amount.Max(0);
            
            c *= 1 - ((2 - c) / 2);

            // TODO : fix this shite

            return (
                c, 0, 0,
                0, c, 0,
                0, 0, c
            );
        })())
    {
    }
}

public sealed class Hue
    : ColorEffect
{
    public Scalar Degree { get; }


    public Hue(Scalar degree) => Degree = degree.Modulus(Scalar.Tau);

    private protected override RGBAColor ProcessColor(RGBAColor input)
    {
        (double h, double s, double l) = input.HSL;

        return RGBAColor.FromHSL(h + Degree, s, l);
    }
}

public class ReplaceColor
    : ColorEffect
{
    private readonly (RGBAColor search, RGBAColor replace)[] _pairs;
    private readonly ColorTolerance _tolerance;



    public ReplaceColor(RGBAColor search, RGBAColor replace)
        : this(new[] { (search, replace) })
    {
    }

    public ReplaceColor(RGBAColor search, RGBAColor replace, ColorTolerance tolerance)
        : this(new[] { (search, replace) }, tolerance)
    {
    }

    public ReplaceColor(IEnumerable<RGBAColor> search, RGBAColor replace)
        : this(search.Select(s => (s, replace)))
    {
    }

    public ReplaceColor(IEnumerable<RGBAColor> search, RGBAColor replace, ColorTolerance tolerance)
        : this(search.Select(s => (s, replace)), tolerance)
    {
    }

    public ReplaceColor(IEnumerable<(RGBAColor search, RGBAColor replace)> pairs)
        : this(pairs, ColorTolerance.RGBDefault)
    {
    }

    public ReplaceColor(IEnumerable<(RGBAColor search, RGBAColor replace)> pairs, ColorTolerance tolerance)
    {
        _pairs = pairs as (RGBAColor, RGBAColor)[] ?? pairs.ToArray();
        _tolerance = tolerance;
    }

    private protected override RGBAColor ProcessColor(RGBAColor input)
    {
        foreach ((RGBAColor search, RGBAColor replace) in _pairs)
            if (input.Equals(search, _tolerance))
                return replace;

        return input;
    }
}

public sealed class RemoveColor
    : ReplaceColor
{
    public RemoveColor(RGBAColor color)
        : base(color, RGBAColor.Transparent)
    {
    }

    public RemoveColor(RGBAColor color, ColorTolerance tolerance)
        : base(color, RGBAColor.Transparent, tolerance)
    {
    }

    public RemoveColor(IEnumerable<RGBAColor> colors)
        : base(colors, RGBAColor.Transparent)
    {
    }

    public RemoveColor(IEnumerable<RGBAColor> colors, ColorTolerance tolerance)
        : base(colors, RGBAColor.Transparent, tolerance)
    {
    }
}

// TODO : gamma correction
// TODO : color overlay
// TODO : tint effect

public sealed class SimpleGlow
    //: PartialBitmapEffectBase
{
    //public override Bitmap ApplyTo(Bitmap bmp, Rectangle region) => ;
    /*
        return bmp.ApplyEffectRange<FastBlurBitmapEffect>(Range, Radius)
                    .ApplyBlendEffectRange<AddBitmapBlendEffect>(bmp, Range)
                    .ApplyEffectRange<SaturationBitmapEffect>(Range, 1 + (.075 * Amount))
                    .ApplyEffectRange<BrightnessBitmapEffect>(Range, 1 - (.075 * Amount))
                    .Average(bmp, Amount);
     */
}

public class GammaCorrect
    : ColorEffect
{
    public Scalar Gamma { get; }


    public GammaCorrect(Scalar gamma) => Gamma = gamma;

    private protected sealed override RGBAColor ProcessColor(RGBAColor input) => input.CorrectGamma(Gamma);
}

public sealed class RGBtoSRGB
    : GammaCorrect
{
    public RGBtoSRGB()
        : base(RGBAColor.SRGB_GAMMA_CORRECTION_FACTOR)
    {
    }
}

public sealed class SRGBtoRGB
    : GammaCorrect
{
    public SRGBtoRGB()
        : base(RGBAColor.SRGB_GAMMA_CORRECTION_FACTOR.MultiplicativeInverse)
    {
    }
}

public sealed class RGBtoHSL
    : ColorEffect
{
    private protected override RGBAColor ProcessColor(RGBAColor input)
    {
        (double h, double s, double l) = input.HSL;

        return new Vector4(h / Scalar.Tau, s, l, input.Af);
    }
}

public sealed class HSLtoRGB
    : ColorEffect
{
    private protected override RGBAColor ProcessColor(RGBAColor input)
    {
        Scalar h = input.Rf * Scalar.Tau;
        Scalar s = input.Gf;
        Scalar l = input.Bf;

        return RGBAColor.FromHSL(h, s, l);
    }
}

public class ReduceColorSpace
    : ColorEffect.Delegated
{
    public ColorEqualityMetric EqualityMetric { get; }
    public ColorPalette ColorPalette { get; }


    public ReduceColorSpace(ColorPalette target_palette, ColorEqualityMetric equality_metric = ColorEqualityMetric.RGBChannels)
        : base(c => target_palette.GetNearestColor(c, equality_metric))
    {
        ColorPalette = target_palette;
        EqualityMetric = equality_metric;
    }

    public ReduceColorSpace(IEnumerable<RGBAColor> target_palette, ColorEqualityMetric equality_metric = ColorEqualityMetric.RGBChannels)
        : this(new ColorPalette(target_palette), equality_metric)
    {
    }
}

public class ColorSpaceReductionError
    : ColorEffect.Delegated
{
    public ColorEqualityMetric EqualityMetric { get; }
    public ColorPalette ColorPalette { get; }


    public ColorSpaceReductionError(ColorPalette target_palette, ColorEqualityMetric equality_metric = ColorEqualityMetric.RGBChannels)
        : base(c =>
        {
            target_palette.GetNearestColor(c, equality_metric, out double dist);
            byte val = (byte)(dist * 255);

            return new(val, val, val);
        })
    {
        ColorPalette = target_palette;
        EqualityMetric = equality_metric;
    }

    public ColorSpaceReductionError(IEnumerable<RGBAColor> target_palette, ColorEqualityMetric equality_metric = ColorEqualityMetric.RGBChannels)
        : this(new ColorPalette(target_palette), equality_metric)
    {
    }
}

public sealed class Cartoon
    : ColorEffect
{
    public int Steps { get; }


    public Cartoon(int steps) => Steps = Math.Max(steps, 1);

    private protected override RGBAColor ProcessColor(RGBAColor input)
    {
        Vector4 v = input;

        return new Vector4(
            (v.X * Steps).Rounded / Steps,
            (v.Y * Steps).Rounded / Steps,
            (v.Z * Steps).Rounded / Steps,
            v.W
        );
    }
}

public sealed class Cartoon2
    : PartialBitmapEffect.Accelerated
{
    public int Steps { get; }


    public Cartoon2(int steps) => Steps = Math.Max(steps, 1);

    protected internal override unsafe void Process(Bitmap bmp, RGBAColor* source, RGBAColor* destination, Rectangle region)
    {
        int s = Steps;
        int s2 = Math.Max(s / 2, 1);
        int[] indices = GetIndices(bmp, region);
        RGBAColor[] blurred = new RGBAColor[bmp.Width * bmp.Height];

        fixed (RGBAColor* ptr = blurred)
            new BoxBlur(1).Process(bmp, source, ptr, region);

        Parallel.For(0, indices.Length, i =>
        {
            Scalar l = ((Scalar)source[i].CIEGray * Steps).Rounded / Steps;
            (double h, double s, _) = blurred[i].HSL;

            destination[i] = RGBAColor.FromHSL(
                ((Scalar)h * s2).Rounded / s2,
                s,
                l,
                source[i].Af
            );
        });
    }
}

public sealed class Cartoon3
    : PartialBitmapEffect
{
    public int Steps { get; }
    public Scalar EdgeSensitivity { get; }


    public Cartoon3(int steps)
        : this(steps, .3)
    {
    }

    public Cartoon3(int steps, Scalar edgeSensitivity)
    {
        Steps = Math.Max(steps, 2);
        EdgeSensitivity = edgeSensitivity.Clamp();
    }

    private protected override Bitmap Process(Bitmap bmp, Rectangle region)
    {
        double e = EdgeSensitivity / 10;
        Bitmap background = new Cartoon2(Steps).ApplyTo(bmp, region);
        
        return bmp.ApplyEffect(new AcceleratedChainedPartialBitmapEffect(
            new SingleConvolutionEffect(new Matrix3(
                1, 1, 1,
                1, 1, 1,
                1, 1, 1
            ) / 9),
            new SingleConvolutionEffect(new Matrix3(
                0, -1, 0,
                -1, 4, -1,
                0, -1, 0
            )),
            new ColorEffect.Delegated(c =>
            {
                double s = 1 - c.CIEGray;

                s = s < .3 ? 0 : s * s;
                s = s > .9 + e ? 1 : 0;

                return new Vector4(s, s, s, c.Af);
            }),
            new BitmapBlend(background, BlendMode.Multiply, 1)
        ), region);
    }
}

public sealed class Colorize
    : ColorEffect
{
    public ColorMap Map { get; }


    public Colorize(ColorMap map) => Map = map;

    [Obsolete($"Use the effect '{nameof(ReduceColorSpace)}' instead.", true)]
    public Colorize(ColorPalette palette) => throw new Exception($"Please use the class '{typeof(ReduceColorSpace)}' instead.");

    private protected override RGBAColor ProcessColor(RGBAColor input) => Map[input.Average];
}

public sealed class Sepia
    : RGBColorEffect
{
    public Sepia()
        : this(Scalar.One)
    {
    }

    public Sepia(Scalar strength)
        : base(Matrix3.Identity.LinearInterpolate((
            .393, .769, .189,
            .349, .686, .168,
            .272, .534, .131
        ), strength))
    {
    }
}

public sealed class JPEGCompressionEffect
    : PartialBitmapEffect
{
    public Scalar CompressionAmount { get; }


    public JPEGCompressionEffect(Scalar amount) =>
        CompressionAmount = amount.Clamp(); // 1 - ((4 * amount.Clamp() + 1).MultiplicativeInverse - .2) / .8;

    private protected override Bitmap Process(Bitmap bmp, Rectangle region)
    {
        using MemoryStream ms = new();
        int level = (int)Math.Round((1 - CompressionAmount) * 100);

        if (level is 100)
            return (Bitmap)bmp.Clone();

        BitmapExtensions.SaveAsJPEG(bmp, ms, level);

        Bitmap result = new(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);

        using Bitmap compressed = DataStream.FromStream(ms).ToBitmap();
        using Graphics g = Graphics.FromImage(result);

        g.DrawImageUnscaled(bmp, 0, 0);
        g.DrawImageUnscaledAndClipped(compressed, region);
        g.Flush();

        return result;
    }
}

public sealed class QOIFCorruptedEffect
    : PartialBitmapEffect
{
    private readonly Random _random = Random.XorShift;

    public int CorruptionCounts { get; }

    public QOIFVersion FormatVersion { get; set; } = QOIFVersion.Original;


    public QOIFCorruptedEffect(int corruption_counts) =>
        CorruptionCounts = Math.Max(0, corruption_counts);

    private protected override unsafe Bitmap Process(Bitmap bmp, Rectangle region)
    {
        if (CorruptionCounts is 0)
            return (Bitmap)bmp.Clone();

        using Bitmap cropped = BitmapExtensions.CropTo(bmp, region);
        DataStream stream = DataStream.FromQOIFBitmap(cropped, FormatVersion);
        Span<byte> dat = stream.Data;
        int corruptions = CorruptionCounts;

        while (corruptions --> 0)
            dat[_random.Next(sizeof(QOIFHeader), dat.Length - 8)] = _random.NextByte();

        Bitmap result = new(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
        using Bitmap corrupted = stream.SeekBeginning().ToQOIFBitmap();
        using Graphics g = Graphics.FromImage(result);

        g.DrawImageUnscaled(bmp, 0, 0);
        g.DrawImageUnscaledAndClipped(corrupted, region);
        g.Flush();

        return result;
    }
}

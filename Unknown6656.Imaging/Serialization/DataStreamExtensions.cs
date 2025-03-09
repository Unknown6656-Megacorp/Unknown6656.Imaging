using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System;

using Unknown6656.Imaging;
using Unknown6656.Runtime;

namespace Unknown6656.Serialization;


public static unsafe class DataStreamExtensions
{
    [SupportedOSPlatform(OS.WIN)]
    public static Bitmap ToBitmap(this DataStream @this) => (Bitmap)Image.FromStream(@this);

    public static Bitmap ToQOIFBitmap(this DataStream @this) => QOIF.LoadQOIFImage(@this);

    public static Bitmap ToRGBAEncodedBitmap(this DataStream @this)
    {
        RGBAColor[] pixels = @this.ToArray<RGBAColor>();
        int len = pixels.Length;
        int i = (int)Math.Sqrt(len);
        int fac = 1;

        while (i-- > 1)
            if (len % i == 0)
            {
                fac = i;

                break;
            }

        Bitmap bitmap = new(fac, len / fac, PixelFormat.Format32bppArgb);

        bitmap.LockRGBAPixels((ptr, _, _) => pixels.CopyTo(new Span<RGBAColor>(ptr, pixels.Length)));

        return bitmap;
    }


    public static DataStream DataStreamFromBitmapAsRGBAEncoded(this Bitmap bitmap) => DataStream.FromArray(bitmap.ToPixelArray());

    public static DataStream DataStreamFromQOIFBitmap(this Bitmap bitmap, QOIFVersion format_version = QOIFVersion.Original)
    {
        DataStream ds = new();

        QOIF.SaveQOIFImage(bitmap, ds, format_version);

        return ds;
    }

    [SupportedOSPlatform(OS.WIN)]
    public static DataStream DataStreamFromBitmap(this Bitmap bitmap) => DataStreamFromBitmap(bitmap, ImageFormat.Png);

    [SupportedOSPlatform(OS.WIN)]
    public static DataStream DataStreamFromBitmap(this Bitmap bitmap, ImageFormat format)
    {
        using MemoryStream ms = new();

        bitmap.Save(ms, format);

        return DataStream.FromStream(ms);
    }
}

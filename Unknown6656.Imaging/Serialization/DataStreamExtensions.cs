using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

using Unknown6656.Imaging;

using Unknown6656.Runtime;
using System.Runtime.CompilerServices;
using System.IO;

namespace Unknown6656.Serialization;


public static unsafe class DataStreamExtensions
{
    [SupportedOSPlatform(OS.WIN)]
    public Bitmap ToBitmap(this DataStream @this) => (Bitmap)Image.FromStream(@this);

    public Bitmap ToQOIFBitmap(this DataStream @this) => QOIF.LoadQOIFImage(@this);

    public Bitmap ToRGBAEncodedBitmap(this DataStream @this)
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

        bitmap.LockRGBAPixels((ptr, _, _) => pixels.CopyTo(ptr));

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

        return FromStream(ms);
    }

}

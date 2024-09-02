using System;

namespace Unknown6656.Imaging;


// Hopefully, with C#13's shapes and extensions, this will be a part of system.console

public static unsafe partial class ConsoleColorExtensions
{
    private static RGBAColor? _fg;
    private static RGBAColor? _bg;


    public static RGBAColor RGBForegroundColor
    {
        get => _fg ?? RGBAColor.FromConsoleColor(Console.ForegroundColor, ConsoleColorScheme.Legacy);
        set
        {
            _fg = value;

            Console.Write(value.ToVT100ForegroundString());
        }
    }

    public static RGBAColor RGBBackgroundColor
    {
        get => _bg ?? RGBAColor.FromConsoleColor(Console.BackgroundColor, ConsoleColorScheme.Legacy);
        set
        {
            _bg = value;

            Console.Write(value.ToVT100BackgroundString());
        }
    }

    public static void SetAsConsoleForeground(this IColor color) => RGBForegroundColor = new(color);

    public static void SetAsConsoleBackground(this IColor color) => RGBBackgroundColor = new(color);
}

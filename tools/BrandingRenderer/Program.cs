using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Svg;

if (args.Length is < 4 or > 5
    || !int.TryParse(args[2], out var width)
    || !int.TryParse(args[3], out var height)
    || width <= 0
    || height <= 0)
{
    Console.Error.WriteLine(
        "Usage: BrandingRenderer <source.svg> <destination.png> <width> <height> [fit]");
    return 2;
}

var sourcePath = Path.GetFullPath(args[0]);
var destinationPath = Path.GetFullPath(args[1]);
var fit = args.Length == 5 && args[4].Equals("fit", StringComparison.OrdinalIgnoreCase);
var document = SvgDocument.Open<SvgDocument>(sourcePath);

Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
using var output = new Bitmap(width, height, PixelFormat.Format32bppArgb);
output.SetResolution(96, 96);
using var graphics = Graphics.FromImage(output);
graphics.SmoothingMode = SmoothingMode.AntiAlias;
graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
graphics.Clear(Color.FromArgb(5, 11, 24));

if (fit)
{
    var sourceWidth = document.ViewBox.Width > 0 ? document.ViewBox.Width : document.Width.Value;
    var sourceHeight = document.ViewBox.Height > 0 ? document.ViewBox.Height : document.Height.Value;
    var scale = Math.Min(width / sourceWidth, height / sourceHeight);
    var renderWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
    var renderHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
    using var rendered = document.Draw(renderWidth, renderHeight);
    graphics.DrawImage(
        rendered,
        (width - renderWidth) / 2,
        (height - renderHeight) / 2,
        renderWidth,
        renderHeight);
}
else
{
    using var rendered = document.Draw(width, height);
    graphics.DrawImage(rendered, 0, 0, width, height);
}

output.Save(destinationPath, ImageFormat.Png);
Console.WriteLine($"Rendered {Path.GetFileName(destinationPath)} ({width}x{height}).");
return 0;

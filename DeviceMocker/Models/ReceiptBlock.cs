using System.Windows.Media.Imaging;

namespace DeviceMocker.Models
{
    public abstract class ReceiptBlock
    {
    }

    public sealed class ReceiptTextBlock : ReceiptBlock
    {
        public string Text { get; init; } = string.Empty;
    }

    public sealed class ReceiptImageBlock : ReceiptBlock
    {
        public BitmapSource? Bitmap { get; init; }
        public double DisplayWidth { get; init; }
        public double DisplayHeight { get; init; }
    }
}

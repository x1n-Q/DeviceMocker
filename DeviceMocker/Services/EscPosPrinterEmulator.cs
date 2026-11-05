using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeviceMocker.Interfaces;
using DeviceMocker.Models;

namespace DeviceMocker.Services
{
    public class EscPosPrinterEmulator : IEmulatorModule
    {
        public const string ResetDrawerTestCommand = "<<DM_RESET_DRAWER>>";

        private const double PixelsPerColumn = 8.0;

        private readonly CashDrawerEmulator _cashDrawer;
        private readonly List<byte> _pendingBytes = new();
        private readonly List<ReceiptBlock> _blocks = new();
        private readonly StringBuilder _currentLine = new();

        private int _alignmentMode;
        private bool _emphasis;
        private bool _renderPreview = true;
        private string _sessionLabel = "escpos";
        private int _paperColumns = 42;
        private int _paperDots = 576;

        public string Id => "escpos-printer-emulator";
        public string Name => "ESC/POS Printer Emulator";
        public IReadOnlyList<ReceiptBlock> Blocks => _blocks;
        public string ReceiptPreview => BuildPreviewText();
        public bool IsDrawerOpen => _cashDrawer.IsDrawerOpen;

        public event Action<EmulatorSessionLog>? LogProduced;
        public event Action? StateChanged;

        public EscPosPrinterEmulator(CashDrawerEmulator cashDrawer)
        {
            _cashDrawer = cashDrawer;
            _cashDrawer.LogProduced += log => LogProduced?.Invoke(log);
            _cashDrawer.StateChanged += () => StateChanged?.Invoke();
        }

        public void Start(EmulatorProfileSettings settings)
        {
            _pendingBytes.Clear();
            _blocks.Clear();
            _currentLine.Clear();
            _alignmentMode = 0;
            _emphasis = false;
            _renderPreview = settings.RenderReceiptPreview;
            _paperColumns = settings.PaperWidth == ReceiptPaperWidth.Mm58 ? 32 : 42;
            _paperDots = settings.PaperWidth == ReceiptPaperWidth.Mm58 ? 384 : 576;
            _sessionLabel = settings.DeviceFamily == EmulatorDeviceFamily.ReceiptPrinter ? "receipt-printer" : "printer";
            _cashDrawer.MarkClosed("Printer emulator session started. Drawer reset to closed.");
            EmitParsed("ESC/POS printer emulator ready.");
            StateChanged?.Invoke();
        }

        public void Stop()
        {
            FlushCurrentLine(force: true);
            EmitParsed("ESC/POS printer emulator stopped.");
            StateChanged?.Invoke();
        }

        public Task HandleBytesAsync(byte[] bytes, CancellationToken cancellationToken = default)
        {
            _pendingBytes.AddRange(bytes);
            ParsePendingBytes();
            return Task.CompletedTask;
        }

        private void ParsePendingBytes()
        {
            var index = 0;

            while (index < _pendingBytes.Count)
            {
                var current = _pendingBytes[index];

                if (current == 0x1B)
                {
                    if (!TryParseEscCommand(ref index))
                        break;
                    continue;
                }

                if (current == 0x1D)
                {
                    if (!TryParseGsCommand(ref index))
                        break;
                    continue;
                }

                switch (current)
                {
                    case 0x0A:
                        FlushCurrentLine();
                        index++;
                        continue;
                    case 0x0D:
                        index++;
                        continue;
                    case 0x09:
                        _currentLine.Append("    ");
                        index++;
                        continue;
                }

                if (current >= 0x20 && current <= 0x7E)
                {
                    _currentLine.Append((char)current);
                    index++;
                    continue;
                }

                EmitWarning($"Unhandled byte 0x{current:X2}");
                index++;
            }

            if (index > 0)
                _pendingBytes.RemoveRange(0, index);

            StateChanged?.Invoke();
        }

        private bool TryParseEscCommand(ref int index)
        {
            if (index + 1 >= _pendingBytes.Count)
                return false;

            var command = _pendingBytes[index + 1];

            switch (command)
            {
                case 0x40:
                    if (index + 2 > _pendingBytes.Count)
                        return false;
                    _alignmentMode = 0;
                    _emphasis = false;
                    EmitParsed("ESC @ -> Initialize printer");
                    index += 2;
                    return true;

                case 0x70:
                    if (index + 4 >= _pendingBytes.Count)
                        return false;
                    var pin = _pendingBytes[index + 2];
                    var onTime = _pendingBytes[index + 3];
                    var offTime = _pendingBytes[index + 4];
                    _cashDrawer.OpenFromPrinterKick($"pin={pin}, on={onTime}, off={offTime}");
                    EmitParsed($"ESC p -> Drawer kick (pin={pin}, on={onTime}, off={offTime})");
                    index += 5;
                    return true;

                case 0x61:
                    if (index + 2 >= _pendingBytes.Count)
                        return false;
                    _alignmentMode = _pendingBytes[index + 2] switch
                    {
                        1 => 1,
                        2 => 2,
                        _ => 0
                    };
                    EmitParsed($"ESC a -> Alignment {(_alignmentMode == 1 ? "Center" : _alignmentMode == 2 ? "Right" : "Left")}");
                    index += 3;
                    return true;

                case 0x45:
                    if (index + 2 >= _pendingBytes.Count)
                        return false;
                    _emphasis = _pendingBytes[index + 2] != 0;
                    EmitParsed($"ESC E -> Emphasis {(_emphasis ? "On" : "Off")}");
                    index += 3;
                    return true;

                case 0x64: // ESC d - feed paper n lines
                    if (index + 2 >= _pendingBytes.Count)
                        return false;
                    var feed = _pendingBytes[index + 2];
                    FlushCurrentLine(force: true);
                    EmitParsed($"ESC d -> Feed paper ({feed} line(s))");
                    index += 3;
                    return true;

                default:
                    EmitWarning($"Unknown ESC command 0x{command:X2}");
                    index += 2;
                    return true;
            }
        }

        private bool TryParseGsCommand(ref int index)
        {
            if (index + 1 >= _pendingBytes.Count)
                return false;

            var command = _pendingBytes[index + 1];

            switch (command)
            {
                case 0x56:
                    if (index + 2 >= _pendingBytes.Count)
                        return false;
                    var mode = _pendingBytes[index + 2];
                    if (mode == 0x41 || mode == 0x42) // GS V m n (feed + cut)
                    {
                        if (index + 3 >= _pendingBytes.Count)
                            return false;
                        var n = _pendingBytes[index + 3];
                        EmitParsed($"GS V -> Cut paper (mode={mode}, n={n})");
                        AppendRenderMarker("[CUT]");
                        index += 4;
                    }
                    else
                    {
                        EmitParsed($"GS V -> Cut paper (mode={mode})");
                        AppendRenderMarker("[CUT]");
                        index += 3;
                    }
                    return true;

                case 0x76: // GS v - print raster bit image
                    return TryParseGsV0(ref index);

                default:
                    EmitWarning($"Unknown GS command 0x{command:X2}");
                    index += 2;
                    return true;
            }
        }

        private bool TryParseGsV0(ref int index)
        {
            // GS v 0 m xL xH yL yH d1...dk — print raster bit image.
            if (index + 8 > _pendingBytes.Count)
                return false;

            var sub = _pendingBytes[index + 2];
            if (sub != 0x30)
            {
                EmitWarning($"Unknown GS v sub-command 0x{sub:X2}");
                index += 3;
                return true;
            }

            var m = _pendingBytes[index + 3];
            var xL = _pendingBytes[index + 4];
            var xH = _pendingBytes[index + 5];
            var yL = _pendingBytes[index + 6];
            var yH = _pendingBytes[index + 7];

            var bytesPerRow = xL + (xH << 8);
            var heightDots = yL + (yH << 8);

            int bytesPerColumn;
            switch (m)
            {
                case 0x00: // 8-dot single density
                case 0x01: // 8-dot double density
                case 0x30: // 8-dot raster (color 1)
                case 0x31: // 8-dot raster (color 2)
                case 0x32: // 8-dot raster (color 3)
                case 0x33: // 8-dot raster (color 4)
                    bytesPerColumn = 1;
                    break;
                case 0x20: // 24-dot single density
                case 0x21: // 24-dot double density
                    bytesPerColumn = 3;
                    break;
                default:
                    EmitWarning($"Unknown GS v 0 density m=0x{m:X2}");
                    index += 8;
                    return true;
            }

            var widthDots = bytesPerRow * 8;
            var dataLength = bytesPerRow * bytesPerColumn * heightDots;
            if (dataLength <= 0 || index + 8 + dataLength > _pendingBytes.Count)
                return false;

            var data = _pendingBytes.GetRange(index + 8, dataLength).ToArray();

            FlushCurrentLine(force: true);
            EmitParsed($"GS v 0 -> Print raster image {widthDots} x {heightDots} dots (m=0x{m:X2})");

            if (_renderPreview)
            {
                if (bytesPerColumn == 1)
                {
                    var bitmap = BuildBitmap(data, bytesPerRow, heightDots);
                    var scale = _paperColumns * PixelsPerColumn / _paperDots;
                    _blocks.Add(new ReceiptImageBlock
                    {
                        Bitmap = bitmap,
                        DisplayWidth = widthDots * scale,
                        DisplayHeight = heightDots * scale
                    });
                }
                else
                {
                    _blocks.Add(new ReceiptTextBlock { Text = $"[BITMAP {widthDots}x{heightDots}]" });
                }
            }

            EmitRender($"Rendered raster image {widthDots}x{heightDots} dots");

            index += 8 + dataLength;
            return true;
        }

        private static BitmapSource BuildBitmap(byte[] data, int bytesPerRow, int heightDots)
        {
            var widthDots = bytesPerRow * 8;
            var pixels = new byte[widthDots * heightDots];
            for (var y = 0; y < heightDots; y++)
            {
                for (var x = 0; x < widthDots; x++)
                {
                    var black = (data[y * bytesPerRow + x / 8] & (0x80 >> (x % 8))) != 0;
                    pixels[y * widthDots + x] = black ? (byte)0 : (byte)255; // Gray8: 0 = black, 255 = white
                }
            }

            var bitmap = new WriteableBitmap(widthDots, heightDots, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new Int32Rect(0, 0, widthDots, heightDots), pixels, widthDots, 0);
            bitmap.Freeze();
            return bitmap;
        }

        private void FlushCurrentLine(bool force = false)
        {
            if (_currentLine.Length == 0 && !force)
            {
                if (_renderPreview)
                    _blocks.Add(new ReceiptTextBlock { Text = " " });
                return;
            }

            var line = _currentLine.ToString();
            if (string.Equals(line.Trim(), ResetDrawerTestCommand, StringComparison.Ordinal))
            {
                _cashDrawer.MarkClosed("Drawer reset by DeviceMocker test command.");
                EmitParsed("DeviceMocker test command -> Reset drawer state");
                _currentLine.Clear();
                return;
            }

            if (_emphasis && line.Length > 0)
                line = $"[B] {line}";

            if (_renderPreview)
                _blocks.Add(new ReceiptTextBlock { Text = ApplyAlignment(line) });

            if (line.Length > 0)
                EmitRender($"Rendered line: {line}");

            _currentLine.Clear();
        }

        private void AppendRenderMarker(string marker)
        {
            FlushCurrentLine(force: true);
            if (_renderPreview)
                _blocks.Add(new ReceiptTextBlock { Text = marker });
            EmitRender(marker);
        }

        private string ApplyAlignment(string line)
        {
            var width = _paperColumns;
            if (line.Length >= width)
                return line;

            return _alignmentMode switch
            {
                1 => line.PadLeft((width + line.Length) / 2),
                2 => line.PadLeft(width),
                _ => line
            };
        }

        private string BuildPreviewText()
        {
            var sb = new StringBuilder();
            foreach (var block in _blocks)
            {
                switch (block)
                {
                    case ReceiptTextBlock text:
                        sb.AppendLine(text.Text);
                        break;
                    case ReceiptImageBlock image:
                        sb.AppendLine($"[IMAGE {image.DisplayWidth:0}x{image.DisplayHeight:0}px]");
                        break;
                }
            }

            if (_currentLine.Length > 0)
                sb.AppendLine(ApplyAlignment(_currentLine.ToString()));

            return sb.ToString();
        }

        private void EmitParsed(string message) => EmitLog(EmulatorSessionLogKind.Parsed, message);
        private void EmitRender(string message) => EmitLog(EmulatorSessionLogKind.Render, message);
        private void EmitWarning(string message) => EmitLog(EmulatorSessionLogKind.Warning, message);

        private void EmitLog(EmulatorSessionLogKind kind, string message)
        {
            LogProduced?.Invoke(new EmulatorSessionLog
            {
                Kind = kind,
                Message = message,
                SessionId = _sessionLabel
            });
        }
    }
}

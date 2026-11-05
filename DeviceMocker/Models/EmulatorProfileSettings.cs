namespace DeviceMocker.Models
{
    public class EmulatorProfileSettings
    {
        public bool Enabled { get; set; }
        public EmulatorDeviceFamily DeviceFamily { get; set; } = EmulatorDeviceFamily.ReceiptPrinter;
        public EmulatorProtocolType Protocol { get; set; } = EmulatorProtocolType.EscPos;
        public DrawerLinkMode DrawerLinkMode { get; set; } = DrawerLinkMode.PrinterDriven;
        public bool RenderReceiptPreview { get; set; } = true;
        public ReceiptPaperWidth PaperWidth { get; set; } = ReceiptPaperWidth.Mm80;
        public EmulatorEndpointConfig Endpoint { get; set; } = new();

        public EmulatorProfileSettings Clone()
        {
            return new EmulatorProfileSettings
            {
                Enabled = Enabled,
                DeviceFamily = DeviceFamily,
                Protocol = Protocol,
                DrawerLinkMode = DrawerLinkMode,
                RenderReceiptPreview = RenderReceiptPreview,
                PaperWidth = PaperWidth,
                Endpoint = Endpoint.Clone()
            };
        }
    }
}

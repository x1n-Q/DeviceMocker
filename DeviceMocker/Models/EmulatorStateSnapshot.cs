namespace DeviceMocker.Models
{
    public class EmulatorStateSnapshot
    {
        public bool IsRunning { get; set; }
        public EmulatorTransportType Transport { get; set; } = EmulatorTransportType.Tcp;
        public string ListenerStatus { get; set; } = "Stopped";
        public string SessionSummary { get; set; } = "No active sessions.";
        public string ActiveProfileName { get; set; } = "Not loaded";
        public string ActiveModuleName { get; set; } = "Not running";
        public int SessionCount { get; set; }
        public string ReceiptPreview { get; set; } = string.Empty;
        public System.Collections.Generic.IReadOnlyList<ReceiptBlock> ReceiptBlocks { get; set; } = Array.Empty<ReceiptBlock>();
        public bool IsDrawerOpen { get; set; }
    }
}

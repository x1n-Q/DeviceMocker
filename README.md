# DeviceMocker

Windows desktop tooling for developers who need to test hardware-dependent software without relying on physical devices.

DeviceMocker combines two workflows:

- `Simulation mode`: send realistic device input into a target app through keyboard, serial, TCP, UDP, or HTTP.
- `Emulator mode`: listen for supported device traffic and behave like a development-time POS endpoint.

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6.svg)]()

<a href="https://get.microsoft.com/installer/download/9nsf1wxjg82g?referrer=appbadge" target="_self" >
	<img src="https://get.microsoft.com/images/en-us%20dark.svg" width="200"/>
</a>

## What This Project Is For

DeviceMocker is built for:

- POS software development
- internal line-of-business applications
- inventory and warehouse systems
- kiosk and self-service flows
- access-control and card-based workflows
- teams that need repeatable hardware testing before real devices are available

The goal is practical development support: help teams simulate or emulate the hardware behavior their software expects, with a workflow that is easy to run on a Windows developer machine.

## Included Applications

This repository ships two Windows desktop apps:

### `DeviceMocker`

The main application for device simulation and POS-oriented emulator hosting.

Current highlights:

- 9 built-in device simulators
- 5 output channels
- saved profiles and reusable settings
- action logging and export
- countdown-based sends for focus switching
- ESC/POS receipt-printer emulator host
- printer-driven cash drawer state tracking

### `POS Hardware Test App`

A companion test client for exercising the DeviceMocker emulator host during development.

Current highlights:

- send ESC/POS receipt content over TCP or serial
- trigger drawer-kick commands
- send cut-paper commands
- send raw hex payloads
- reset drawer state for test workflows

## Current Capability Summary

### Device Simulation

Available device modules:

| Device | Purpose |
| --- | --- |
| Barcode / QR Scanner | Keyboard-style scan input with samples, random values, and batch sending |
| Virtual Keyboard | Text, keys, shortcuts, and navigation testing |
| POS Button Panel | Configurable point-of-sale style button actions |
| Serial Text Sender | COM-port payload testing with direct and simulated workflows |
| Weighing Scale | Weight strings in scale-friendly formats |
| RFID / NFC Reader | UID-based tap simulation |
| Magstripe Reader | Track 1/2/3 swipe testing |
| Test Sequence Builder | Repeatable multi-step input flows |
| Cash Drawer | Text-command-based open/status simulation |

Available output channels:

| Channel | Purpose |
| --- | --- |
| Keyboard Wedge | Sends focused input through Windows keyboard events |
| Serial | Sends payloads to a COM port |
| TCP Client | Sends payloads to a TCP endpoint |
| UDP | Sends payloads as datagrams |
| HTTP Webhook | Sends JSON payloads to an HTTP endpoint |

### POS Emulator Host

The current emulator host is intentionally focused.

Supported development target today:

- ESC/POS receipt-printer traffic
- printer-driven cash drawer behavior
- TCP listener workflows
- serial listener workflows
- raw-byte logging
- parsed event logging
- receipt preview rendering for supported text commands

Phase-1 emulator support is aimed at real development and integration testing, especially for POS teams using ESC/POS-compatible printing flows.

## Quick Start

### Installation

The easiest way to install DeviceMocker is directly from the Microsoft Store:

<a href="https://get.microsoft.com/installer/download/9nsf1wxjg82g?referrer=appbadge" target="_self" >
	<img src="https://get.microsoft.com/images/en-us%20dark.svg" width="200"/>
</a>

### Requirements

- Windows 10 or Windows 11
- .NET 8 SDK for source builds

### Run From Source

```powershell
git clone https://github.com/x1n-Q/DeviceMocker.git
cd DeviceMocker
dotnet run --project DeviceMocker/DeviceMocker.csproj
```

To keep settings, profiles, and logs in a custom location, set `DEVICEMOCKER_DATA_DIR`
before starting the app.

### First Simulation Test

1. Open Notepad.
2. Open `DeviceMocker`.
3. Go to `Devices -> Barcode / QR Scanner`.
4. Click a sample barcode.
5. Switch focus to Notepad during the countdown.
6. The barcode is typed into Notepad like scanner input.

## Emulator Workflow

To test the current ESC/POS emulator:

1. Open `DeviceMocker`.
2. Go to `Emulators`.
3. Set:
   - `Device Type = ReceiptPrinter`
   - `Emulation Protocol = EscPos`
   - `Drawer Kick Link Mode = PrinterDriven`
   - `Transport Binding = Tcp`
   - `TCP Binding IP = 127.0.0.1`
   - `TCP Listening Port = 9100`
4. Click `Start Host`.
5. Run the companion app:

```powershell
dotnet run --project Samples/PosHardwareTestApp/PosHardwareTestApp.csproj
```

6. In the test app, click `Print + Kick Drawer`.
7. DeviceMocker should show receipt output and set the drawer state to open.

## Repository Layout

```text
DeviceMocker/
|-- DeviceMocker/
|   |-- Core/
|   |-- Devices/
|   |-- Helpers/
|   |-- Interfaces/
|   |-- Models/
|   |-- Services/
|   |-- ViewModels/
|   `-- Views/
`-- Samples/
    `-- PosHardwareTestApp/
```

## Development Notes

- `DeviceMocker` is the main product.
- `Samples/PosHardwareTestApp` is a companion integration client, not a separate platform product.
- The current emulator host is intentionally scoped to supported receipt-printer and drawer development scenarios.

## Release Builds

Release-ready Windows builds can be generated with:

```powershell
dotnet publish DeviceMocker/DeviceMocker.csproj -c Release -r win-x64
dotnet publish Samples/PosHardwareTestApp/PosHardwareTestApp.csproj -c Release -r win-x64
```

### Prepare An MSIX Package

Generate the x64 Microsoft Store package with the checked-in Partner Center identity:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-MSIX.ps1
```

The unsigned Store package and SHA-256 checksum are written to
`artifacts/store-submission/`. Partner Center signs the package during ingestion.

### Tests

```powershell
dotnet test Tests/DeviceMocker.Tests/DeviceMocker.Tests.csproj
```

### Branding

Editable SVG sources are under `branding/`. Regenerate the Windows icon and Microsoft
Store assets with `tools/Generate-Branding.ps1`.

## License

Apache License 2.0. See [LICENSE](LICENSE).

## Author

Created by [x1n-Q](https://github.com/x1n-Q).

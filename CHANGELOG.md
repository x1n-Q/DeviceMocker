# Changelog

All notable changes to DeviceMocker will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.1] - 2026-11-05

### Added
- New DeviceMocker identity with editable SVG sources, Windows icon, and Microsoft Store tile assets.
- `DEVICEMOCKER_DATA_DIR` override for portable development and test environments.
- Rotating application diagnostics under the app data `Logs` folder.

### Fixed
- Emulator start failures now clean up listeners and modules instead of leaving a partial running state.
- Serial and TCP listener tasks are cancelled and awaited during shutdown, reducing port-lock and exit races.
- Settings and profile writes now use atomic file replacement to prevent truncated JSON after interruption.
- Invalid settings are backed up and safely reset; an invalid profile no longer prevents all profiles from loading.
- Clean Release builds restore and bundle the POS Hardware Test App reliably.
- Application and companion version metadata now consistently reports 1.1.1.

### Changed
- Startup, background-task, and UI exceptions are logged with bounded log rotation.
- Sidebar branding and release assets now use the hardware-focused DeviceMocker logo.

## [1.0.1] - 2026-05-29

### Fixed
- **Profiles screen**: Editor panel no longer overlaps the list; added proper spacing and minimum width.
- **Profiles screen**: Toolbar buttons now wrap correctly on narrow window widths.
- **Settings screen**: Theme switching now applies instantly to all controls (previously required app restart).
- **Profiles editor**: Numeric-only validation on the "Delay per Character" field prevents silent binding failures.
- **Settings**: `DefaultOutputChannel`, `LogToFile`, and `MaxLogEntries` were defined in the model but missing from the UI — now exposed.

### Added
- **Profiles**: Live search box filters by name, device type, or description.
- **Profiles**: Empty-state placeholder when no profile is selected ("No profile selected").
- **Profiles editor**: New fields — Device Type, Default Output Channel, Prefix.
- **Profiles editor**: Cancel button to discard unsaved edits.
- **Profiles**: ListView rows now have hover/selection styling for better feedback.
- **Profiles & Settings**: Status messages are color-coded (green = success, red = error) and auto-clear after 3 seconds (Settings).
- **Settings**: Reorganized into grouped sections — *Output Defaults*, *Behavior*, *Appearance*.
- **Settings**: "Reset to Defaults" button to restore factory settings.
- **Settings**: Toggle for "Persist logs to file" with helpful description.
- **App**: New reusable styles — `ModernCheckBoxStyle`, `ModernListViewItemStyle`, `SectionHeaderStyle`.

## [1.0.0] - 2026-05-28

### Added

#### Device Simulators (8 devices)
- **Barcode/QR Scanner** - Simulate barcode scanning with keyboard wedge output
  - 20 sample barcodes (EAN-13, UPC-A, Code128, QR codes)
  - Random barcode generator with check digit calculation
  - Batch scan mode with configurable count and interval
  - Scan history with click-to-rescan
- **Virtual Keyboard** - Send individual keys and shortcuts
  - Full keyboard layout (letters, numbers, function keys F1-F12)
  - Special keys (Enter, Tab, Escape, Backspace, Delete)
  - Arrow keys and shortcuts (Ctrl+C, Ctrl+V, etc.)
- **Custom Button Panel** - POS-style configurable buttons
  - Predefined buttons (Cash, Card, Discount, Submit, etc.)
  - Custom button configuration
  - Color-coded button categories
- **Serial Text Sender** - Send text to COM ports
  - Built-in simulation mode with 5 virtual devices
  - Hardware mode for real COM port communication
  - Terminal view with TX/RX hex display
  - Configurable baud rate, parity, data bits, stop bits
- **Weighing Scale** - Simulate weight readings
  - Standard scale format (ST,GS protocol)
  - Multiple units (kg, lb, g, oz)
  - Quick weight presets and random generator
  - Tare function
- **RFID/NFC Reader** - Simulate card taps
  - 8 sample cards (MIFARE Classic, Ultralight, DESFire, NTAG)
  - Multiple UID formats (hex, decimal, with colons/spaces)
  - Random UID generator
  - Tap history
- **Magstripe Card Reader** - Simulate card swipes
  - Track 1, 2, and 3 data support
  - 6 sample cards (Visa, MasterCard, Amex, Gift, Loyalty, Employee)
  - Random track data generator
  - Swipe history
- **Test Sequence Builder** - Build and replay action sequences
  - Multi-step sequences with configurable delays
  - 3 presets (POS Login, Scan 3 Items, Form Fill)
  - Step types (Text, Key, Shortcut)
  - Sequence editor with add/remove/reorder

#### Output Channels (5 channels)
- **Keyboard Wedge** - Types into active window using Windows SendInput API
- **Serial (COM Port)** - Sends data to physical or virtual COM ports
- **TCP Client** - Sends data to TCP servers
- **UDP** - Sends UDP datagrams
- **HTTP Webhook** - POSTs JSON payload to any URL

#### Core Features
- **Profile Management**
  - Create, edit, duplicate, delete profiles
  - Import/export profiles as JSON
  - Default profiles included
- **Activity Logging**
  - All actions logged with timestamp, device, channel, payload, status
  - Log export to CSV and JSON
  - Searchable log viewer
- **Settings**
  - Dark/Light theme toggle
  - Configurable countdown seconds
  - Default output channel selection
- **Architecture**
  - MVVM pattern with clean separation of concerns
  - Device ↔ Channel separation via InputRouter
  - Interface-driven design (IDeviceModule, IOutputChannel)
  - Service Locator pattern for dependency management
  - Async/await for all I/O operations

#### User Interface
- Modern dark theme with accent colors
- Responsive layout with sidebar navigation
- Dashboard with quick actions and recent logs
- Device cards with hover effects
- Inline profile editor
- Countdown timer for delayed sends

### Technical Details
- Built with .NET 8 and WPF
- Windows-only (uses SendInput API and serial ports)
- No external dependencies for core functionality
- Modular architecture for easy extension

### License
- Apache License 2.0

[1.0.1]: https://github.com/x1n-Q/DeviceMocker/releases/tag/v1.0.1
[1.0.0]: https://github.com/x1n-Q/DeviceMocker/releases/tag/v1.0.0
[1.1.1]: https://github.com/x1n-Q/DeviceMocker/releases/tag/v1.1.1

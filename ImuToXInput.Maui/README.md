# ImuToXInput.Maui

Cross-platform ImuToXInput app with the configurator built in.

- **Windows**: Uses ViGEm (virtual Xbox 360 controller). Same behavior as the desktop `ImuToXInput` exe.
- **Android**: BLE output stub; implement `BleGamepadOutput` to send reports to an nRF dongle.

## Building

1. **MAUI from workload (no NuGet):** This project does **not** reference Microsoft.Maui via NuGet. Install the MAUI workload so the SDK supplies MAUI for net10.0:
   ```bash
   dotnet workload install maui
   ```
2. **Fix "assets file doesn't have a target for net10.0":** Clean and restore the solution:
   ```bash
   cd "I:\Visual Studio\ImuToXInput"
   dotnet clean
   dotnet restore
   ```
3. Build for Windows (unpackaged; no AppxManifest):
   ```bash
   dotnet build ImuToXInput.Maui\ImuToXInput.Maui.csproj -f net10.0-windows10.0.19041.0
   ```
4. Build for Android:
   ```bash
   dotnet build ImuToXInput.Maui\ImuToXInput.Maui.csproj -f net10.0-android
   ```

**Fixes applied in the project:**

- **WindowsPackageType** is set to `None` so no AppxManifest / MSIX is required.
- **Microsoft.Extensions.Hosting** is referenced so `Program.cs` can use `IHost`/`Run()`.
- **No explicit Microsoft.Maui PackageReferences** – MAUI is supplied by the workload (`dotnet workload install maui`).
- **NETSDK1202** (workload out of support) is suppressed.

If the workload install fails or the MAUI project still doesn’t build, open the solution in **Visual Studio** and install the **.NET Multi-Platform App UI development** workload from the installer.

## Structure

- **Shared**: `MainPage` (profile list), `ProfileEditorPage` (script editor), config load/save via `ImuToXInput.Core`.
- **Platforms/Windows**: `ViGEmGamepadOutput` – forwards to ViGEm virtual controller.
- **Platforms/Android**: `BleGamepadOutput` – stub; add BLE GATT client to send HID reports to the nRF.

The mapping engine and config format live in **ImuToXInput.Core** and are shared with the desktop app and ConfigEditor.

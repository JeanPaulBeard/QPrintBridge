# AGENTS.md

Windows-only .NET 8 Worker Service that exposes a local HTTP API (`localhost:19100`) to send raw ESC/POS bytes to Windows printers via `winspool.drv`.

## Commands

```bat
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

- Publish output is a single file: the repo-root `publish\QPrintBridge.exe` (self-contained). `PublishDir` is set in the `.csproj`, so no `-o` needed. `appsettings.json`, `gestor_servicio.bat`, and the `.pdb` are intentionally NOT published (`Content Remove` + `DebugType=none` in Release).
- The `publish\` folder is excluded from default content globs (`DefaultItemExcludes`) to prevent a recursive `publish\publish\` artifact (the SDK's `**/appsettings.json` content glob otherwise re-picks up the committed copy).
- The exe is self-installing: double-clicking it opens a WinForms GUI (installed/removed via the Service Control Manager P/Invoke in `ServiceManager.cs`). Install/start/stop/remove all require elevation.
- No tests, no CI, no lint/format config. Verify by building; runtime behavior requires a real Windows service + printer.

## Architecture

- Entry: `Program.cs` branches on `Environment.UserInteractive` — interactive (double-click) shows the `ServiceForm` WinForms GUI; non-interactive (SCM) runs the `AddWindowsService` + `AddHostedService<Worker>` host. The `[STAThread] Main` is required for WinForms.
- Service install/remove/start/stop/status is P/Invoked against `advapi32.dll` in `ServiceManager.cs` (no `sc.exe` dependency). Service name constant lives there: `ServiceManager.ServiceName`.
- `Worker.cs` is a `BackgroundService` using raw `HttpListener` (not ASP.NET Core / Kestrel). It binds both `http://localhost:19100/` and `http://127.0.0.1:19100/`.
- Endpoints (all return `application/json`):
  - `GET /printers` → `{ "status": "success", "printers": [...] }`
  - `POST /imprimir` → body `{ "impresora": "...", "payload": "<base64 ESC/POS>" }`
- `RawPrinterHelper.cs` P/Invokes `winspool.drv` (`OpenPrinterA`, `StartDocPrinterA`, `WritePrinter`, ...) to push bytes to the spooler with datatype `RAW`.
- `SimpleLogger.cs` writes both INFO and ERROR to `logs\error_log.txt` relative to the executable's base dir (`AppDomain.CurrentDomain.BaseDirectory`), NOT the source tree.

## Gotchas

- Target is `net8.0-windows`; the code will not build/run on non-Windows. Requires the .NET 8 SDK to build (the published exe itself is self-contained).
- `HttpListener` prefix registration on `http://localhost:19100/` requires admin rights; the worker logs "¿Requiere permisos de administrador?" and exits if `listener.Start()` throws.
- Printer names must match `PrinterSettings.InstalledPrinters` exactly (case matters for `OpenPrinter`).
- `appsettings.json` is excluded from publish (`Content Remove`) and only held default logging; the port/URL is hardcoded in `Worker.cs` (`_url`, `_ipUrl`).
- `bin/`, `obj/`, and `logs/` are gitignored; a stray `bin/.../logs/error_log.txt` shows up as modified in `git status` — ignore it.

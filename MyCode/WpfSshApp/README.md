WpfSshApp

A minimal WPF application targeting .NET 8.0 with Serilog configured (Console + rolling file sink).

How to run

1. From PowerShell in the project folder:

   dotnet run

What it does

- Logs startup and shutdown to console and to logs\WpfSerilogApp-<date>.log
- Shows a simple message box when the main window opens

Notes

- Project target is `net8.0-windows`. Ensure you have .NET 8 SDK installed.
- Logs are written to `logs` folder under the app working directory, files named `WpfSshApp-<date>.log`.

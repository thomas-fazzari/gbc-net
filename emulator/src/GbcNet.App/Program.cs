// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using GbcNet.App.Configuration;
using Serilog;

namespace GbcNet.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Log.Logger = CreateLogger(UserDataPaths.LogFilePath);

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            Log.ForContext(typeof(Program)).Fatal(exception, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    internal static Serilog.Core.Logger CreateLogger(string logFilePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logFilePath) ?? ".");
        return new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.File(
                logFilePath,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                fileSizeLimitBytes: 2 * 1024 * 1024,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 14,
                retainedFileTimeLimit: TimeSpan.FromDays(14)
            )
            .CreateLogger();
    }

#pragma warning disable AVALONIA_X11_CSD
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<GbcNetApplication>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions { EnableDrawnDecorations = true })
            .LogToTrace();
#pragma warning restore AVALONIA_X11_CSD
}

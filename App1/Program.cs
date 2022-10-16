// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

using Gtk;
using Serilog;

namespace App1;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        string executableDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string loggingOutputTemplate = "{Timestamp:yyyy-MM-ddTHH:mm:ss} [{Level}] {Message:lj}{NewLine}{Exception}";
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                outputTemplate: loggingOutputTemplate
            )
            .WriteTo.File(
                Path.Combine(executableDirectory, "Program.log"),
                outputTemplate: loggingOutputTemplate,
                fileSizeLimitBytes: 67108864,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 4
            )
            .CreateLogger();

        if (args.Length == 0)
        {
            Program.GuiMain();
        }
        else
        {
            Program.CliMain(args);
        }
    }

    private static void GuiMain()
    {
        Application.Init();
        var app = new Application("org.gtk_test.gtk_test", GLib.ApplicationFlags.None);
        app.Register(GLib.Cancellable.Current);
        var win = new MainWindow();
        app.AddWindow(win);
        win.Show();
        Application.Run();
    }

    private static void CliMain(string[] args)
    {
        System.Action exitProgram = () =>
        {
            Console.WriteLine("Exiting...");
            Log.CloseAndFlush();
            Environment.Exit(0);
        };

        if (args.Length < 1 || args.Length > 2)
        {
            Console.WriteLine("Usage: dotnet run <transmitter_path> [<receiver_path>]");
            Environment.Exit(0);
        }

        string transmitterPath = args[0];
        var transmitter = new ArchiveHandler(transmitterPath);
        transmitter.Start();
        Console.WriteLine("Press \"Enter\" key to stop the watching process.");
        while (Console.ReadKey().Key != ConsoleKey.Enter) { }
        transmitter.Stop();

        if (args.Length == 1)
        {
            exitProgram();
        }

        string receiverPath = args[1];
        var receiver = new ArchiveHandler(receiverPath);
        ArchiveHandler.Synchronize(transmitter, receiver);
        exitProgram();
    }
}

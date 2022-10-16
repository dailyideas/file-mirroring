// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;

using Gtk;
using UI = Gtk.Builder.ObjectAttribute;

namespace App1;

class MainWindow : Window
{
#nullable enable
    [UI] private readonly Gtk.Label? _source_directory_label = null;
    [UI] private readonly Gtk.Label? _destination_directory_label = null;
    [UI] private readonly Gtk.Button? _source_directory_btn = null;
    [UI] private readonly Gtk.Button? _destination_directory_btn = null;
    [UI] private readonly Gtk.Button? _synchronization_btn = null;
    [UI] private readonly Gtk.Label? _synchronization_progress_label = null;
    [UI] private readonly Gtk.Spinner? _synchronization_progress_spinner = null;
#nullable disable
    private ArchiveHandler _transmitter;
    private ArchiveHandler _receiver;

    public MainWindow() : this(new Builder("MainWindow.glade")) { }

    private MainWindow(Builder builder) : base(
        builder.GetRawOwnedObject("MainWindow")
    )
    {
        var cssProvider = new CssProvider();
        cssProvider.LoadFromResource("MainWindow.css");
        StyleContext.AddProvider(cssProvider, UInt32.MaxValue);

        builder.Autoconnect(this);
        DeleteEvent += QuitApplicationEvent;
    }

    private void ShowNotification(
        Gtk.MessageType messageType,
        string message
    )
    {
        var md = new MessageDialog(
            this,
            DialogFlags.DestroyWithParent,
            messageType,
            ButtonsType.Close,
            message
        );
        md.Run();
        md.Destroy();
    }

    private void ShowInformationNotification(string message)
    {
        ShowNotification(Gtk.MessageType.Info, message);
    }

    private void ShowErrorNotification(string message)
    {
        ShowNotification(Gtk.MessageType.Error, message);
    }

    private void QuitApplicationEvent(object sender, DeleteEventArgs a)
    {
        if (_transmitter != null)
        {
            _transmitter.Stop();
        }

        Application.Quit();
    }

    private void TerminateApplicationOnException(System.Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            ShowErrorNotification(exception.Message);
            Environment.Exit(1);
        }
    }

    private void SelectSourceDirectory()
    {
        var fcd = new Gtk.FileChooserDialog(
            "Select the directory",
            this,
            FileChooserAction.SelectFolder,
            "Cancel", ResponseType.Cancel,
            "Open", ResponseType.Accept
        );
        if (fcd.Run() == (int)ResponseType.Accept)
        {
            _transmitter = new ArchiveHandler(fcd.Filename);
            _transmitter.Start();
            _source_directory_label.Text = fcd.Filename;
            _destination_directory_label.Text =
                "You may select the destination directory now";
            _source_directory_btn.Sensitive = false;
            _destination_directory_btn.Sensitive = true;
        }
        fcd.Destroy();
    }

    protected virtual void OnSelectSourceDirectoryBtnClicked(
        object sender,
        System.EventArgs e
    )
    {
        TerminateApplicationOnException(SelectSourceDirectory);
    }

    private void SelectDestinationDirectory()
    {
        var fcd = new Gtk.FileChooserDialog(
            "Select the directory",
            this,
            FileChooserAction.SelectFolder,
            "Cancel", ResponseType.Cancel,
            "Open", ResponseType.Accept
        );
        string selectedDirectoryPath = "";
        if (fcd.Run() == (int)ResponseType.Accept)
        {
            selectedDirectoryPath = fcd.Filename;
        }

        fcd.Destroy();
        if (selectedDirectoryPath == "")
        {
            return;
        }

        if (selectedDirectoryPath == _transmitter.ArchivePath)
        {
            ShowErrorNotification(
                "The destination directory must be different from the source directory"
            );
            return;
        }
        _receiver = new ArchiveHandler(selectedDirectoryPath);
        _destination_directory_label.Text = selectedDirectoryPath;
        _destination_directory_btn.Sensitive = false;
        _synchronization_btn.Sensitive = true;
    }

    protected virtual void OnSelectDestinationDirectoryBtnClicked(
        object sender,
        System.EventArgs e
    )
    {
        TerminateApplicationOnException(SelectDestinationDirectory);
    }

    protected virtual async void OnSynchronizationBtnClicked(
        object sender,
        System.EventArgs e
    )
    {
        _synchronization_btn.Sensitive = false;
        _synchronization_progress_spinner.Visible = true;
        _synchronization_progress_spinner.Start();
        System.Action<string> hook = msg =>
        {
            _synchronization_progress_label.Text = msg;
        };
        try
        {
            await Task.Run(
                () =>
                {
                    ArchiveHandler.Synchronize(_transmitter, _receiver, hook);
                }
            ).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            ShowErrorNotification(exception.Message);
            Environment.Exit(1);
        }
        _synchronization_progress_spinner.Stop();
        _synchronization_progress_spinner.Visible = false;
        ShowInformationNotification(
            "Synchronization completed. You can close the application now."
        );
    }
}

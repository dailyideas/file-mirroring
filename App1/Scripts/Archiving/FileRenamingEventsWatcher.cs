// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

using Serilog;

namespace App1;

public class FileRenamingEventsWatcher
{
    public event Action<FileRenamingEvent>? EventDetectedAction;
    private readonly FileSystemWatcher _fsWatcher;
    private readonly string _watchPath;
    private bool _isRunning;
    private readonly ConcurrentQueue<FileRenamingEvent> _eventsQueue
        = new ConcurrentQueue<FileRenamingEvent>();
    private Thread? _processQueueThread;
    private bool _stopThread;
    private static readonly ILogger _logger =
        Log.ForContext<FileRenamingEventsWatcher>();

    public FileRenamingEventsWatcher(string watchPath)
    {
        if (!Directory.Exists(watchPath))
        {
            throw new ArgumentException(
                $"The watchPath \"{watchPath}\" is not an existing directory."
            );
        }

        _watchPath = watchPath;

        _fsWatcher = new FileSystemWatcher(watchPath);
        _fsWatcher.NotifyFilter =
            NotifyFilters.Attributes
            // | NotifyFilters.CreationTime
            | NotifyFilters.DirectoryName
            | NotifyFilters.FileName
            // | NotifyFilters.LastAccess
            // | NotifyFilters.LastWrite
            // | NotifyFilters.Security
            // | NotifyFilters.Size
            ;
        _fsWatcher.Filter = "";
        _fsWatcher.IncludeSubdirectories = true;
        _fsWatcher.EnableRaisingEvents = false;
        // FSWatcher.Created += OnCreated;
        // FSWatcher.Changed += OnChanged;
        // FSWatcher.Deleted += OnDeleted;
        _fsWatcher.Renamed += OnFileRenamed;
    }

    public bool Start()
    {
        if (_isRunning)
        {
            _logger.Verbose("{0} has started already", GetType().Name);
            return false;
        }
        _processQueueThread = new Thread(ProcessQueue);
        _processQueueThread.Start();
        _fsWatcher.EnableRaisingEvents = true;
        _isRunning = true;
        return true;
    }

    public bool Stop()
    {
        if (!_isRunning)
        {
            _logger.Verbose(
                "{0} cannot be stopped as it is not started",
                GetType().Name
            );
            return false;
        }
        _fsWatcher.EnableRaisingEvents = false;
        _stopThread = true;
        _processQueueThread?.Join();
        _stopThread = false;
        _isRunning = false;
        return true;
    }

    private void ProcessQueue()
    {
        while (!_stopThread || !_eventsQueue.IsEmpty)
        {
            if (_eventsQueue.TryDequeue(out FileRenamingEvent? e))
            {
                EventDetectedAction?.Invoke(e);
            }
            else
            {
                Thread.Sleep(100);
            }
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        var fileRenamingEvent = new FileRenamingEvent(
            Path.GetRelativePath(_watchPath, e.OldFullPath),
            Path.GetRelativePath(_watchPath, e.FullPath)
        );
        _eventsQueue.Enqueue(fileRenamingEvent);
    }
}

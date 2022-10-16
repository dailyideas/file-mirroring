// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Serilog;

namespace App1;

class ArchiveHandler
{
    public string ArchivePath { get; private set; }
    public ArchivingCacheHandler CacheHandler { get; private set; }
    private readonly FileRenamingEventsWatcher _fileRenamingEventsWatcher;
    private static readonly ILogger _logger = Log.ForContext<ArchiveHandler>();

    public ArchiveHandler(string archivePath)
    {
        if (!Directory.Exists(archivePath))
        {
            throw new ArgumentException(
                $"Directory \"{archivePath}\" does not exist."
            );
        }
        ArchivePath = archivePath;
        CacheHandler = new ArchivingCacheHandler(ArchivePath);
        _fileRenamingEventsWatcher = new FileRenamingEventsWatcher(
            ArchivePath
        );

        _fileRenamingEventsWatcher.EventDetectedAction +=
            CacheHandler.InsertFileRenamingEvent;
    }

    public override bool Equals(object? obj)
    {
        var other = obj as ArchiveHandler;
        if (other == null)
        {
            return false;
        }
        return ArchivePath == other.ArchivePath;
    }

    public override int GetHashCode()
    {
        return ArchivePath.GetHashCode();
    }

    public bool Start()
    {
        _logger.Information(
            "Start watching file renaming events on \"{0}\".",
            ArchivePath
        );
        return _fileRenamingEventsWatcher.Start();
    }

    public bool Stop()
    {
        _logger.Information(
            "Stop watching file renaming events on \"{0}\".",
            ArchivePath
        );
        return _fileRenamingEventsWatcher.Stop();
    }

    public string[] ListFiles(string relativePath)
    {
        string combinedPath = Path.Combine(ArchivePath, relativePath);
        if (!Directory.Exists(combinedPath))
        {
            throw new DirectoryNotFoundException(
                $"combinedPath \"{combinedPath}\" is not an existing directory."
            );
        }

        string[] files = Directory.GetFileSystemEntries(combinedPath);
        files = files.Select(f => Path.GetFileName(f)).ToArray();
        Array.Sort(files);
        return files;
    }

    public static FileRenamingEvent GetLastFileRenamingEvent(
        ArchivingCacheHandler h
    )
    {
        FileRenamingEvent result;
        try
        {
            result = h.GetLastFileRenamingEvent();
        }
        catch (ArgumentException)
        {
            result = FileRenamingEvent.Empty;
        }
        return result;
    }

    public static FileRenamingEvent GetFileRenamingEvent(
        ArchivingCacheHandler h,
        int event_id
    )
    {
        FileRenamingEvent result;
        try
        {
            result = h.GetFileRenamingEvent(event_id);
        }
        catch (ArgumentException)
        {
            result = FileRenamingEvent.Empty;
        }
        return result;
    }

    public static void Synchronize(
        ArchiveHandler transmitter,
        ArchiveHandler receiver,
        Action<string>? hook = null
    )
    {
        if (transmitter == receiver)
        {
            throw new ArgumentException(
                "transmitter and receiver must be different."
            );
        }
        _logger.Information(
            "Start synchronizing directories \"{0}\" and \"{1}\". " +
            "Do not make and changes to the directories before the synchronization is finished.",
            transmitter.ArchivePath,
            receiver.ArchivePath
        );
        SynchronizeFileRenamingEvents(transmitter, receiver, hook);
        SynchronizeFileDeletions(transmitter, receiver, hook);
        SynchronizeFileAdditionsAndChanges(transmitter, receiver, hook);
        _logger.Information("Synchronization is finished.");
    }

    private static void SynchronizeFileRenamingEvents(
        ArchiveHandler transmitter,
        ArchiveHandler receiver,
        Action<string>? hook = null
    )
    {
        _logger.Debug("Renaming files in the receiver side.");

        FileRenamingEvent transmitterLastFileRenamingEvent =
            GetLastFileRenamingEvent(transmitter.CacheHandler);
        FileRenamingEvent receiverLastFileRenamingEvent =
            GetLastFileRenamingEvent(receiver.CacheHandler);
        bool isValid = receiverLastFileRenamingEvent.EventID
            <= transmitterLastFileRenamingEvent.EventID;
        FileRenamingEvent transmitterRetracedFileRenamingEvent =
            GetFileRenamingEvent(
                transmitter.CacheHandler,
                receiverLastFileRenamingEvent.EventID
            );
        isValid &= FileRenamingEvent.AreEqual(
            receiverLastFileRenamingEvent,
            transmitterRetracedFileRenamingEvent
        );
        if (!isValid)
        {
            throw new ArgumentException(
                "The receiver is not a predecessor of the transmitter."
            );
        }

        for (
            int i = receiverLastFileRenamingEvent.EventID + 1;
            i <= transmitterLastFileRenamingEvent.EventID;
            i++
        )
        {
            FileRenamingEvent e = transmitter.CacheHandler
                .GetFileRenamingEvent(i);
            receiver.CacheHandler.InsertFileRenamingEvent(e);
            string receiverPath = Path.Combine(
                receiver.ArchivePath,
                e.SourceRelativePath
            );
            string transmitterPath = Path.Combine(
                receiver.ArchivePath,
                e.DestinationRelativePath
            );
            _logger.Debug(
                "Renaming \"{0}\" to \"{1}\"",
                receiverPath,
                transmitterPath
            );
            hook?.Invoke($"Renaming \"{receiverPath}\"");
            if (Directory.Exists(receiverPath))
            {
                Directory.Move(receiverPath, transmitterPath);
            }
            else if (File.Exists(receiverPath))
            {
                File.Move(receiverPath, transmitterPath);
            }
            else
            {
                _logger.Warning(
                    $"Neither file nor directory exists at \"{receiverPath}\" for renaming."
                );
            }
        }
    }

    private static void CopyFileOrDirectory(
        string sourcePath,
        string destinationPath
    )
    {
        _logger.Debug(
            "Copying \"{0}\" to \"{1}\"",
            sourcePath,
            destinationPath
        );
        if (Directory.Exists(sourcePath))
        {
            Directory.CreateDirectory(destinationPath);
            /// -\[[Dis](https://stackoverflow.com/a/3822913)\] Copy the entire contents of a directory in C#
            foreach (
                string dirPath in Directory.GetDirectories(
                    sourcePath,
                    searchPattern: "*",
                    searchOption: SearchOption.AllDirectories
                )
            )
            {
                Directory.CreateDirectory(
                    Path.Combine(
                        destinationPath,
                        dirPath.Substring(sourcePath.Length + 1)
                    )
                );
            }
            foreach (string filePath in Directory.GetFiles(
                    sourcePath,
                    searchPattern: "*.*",
                    searchOption: SearchOption.AllDirectories
                )
            )
            {
                File.Copy(
                    filePath,
                    Path.Combine(
                        destinationPath,
                        filePath.Substring(sourcePath.Length + 1)
                    ),
                    overwrite: true
                );
            }
        }
        else if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
        else
        {
            _logger.Warning(
                $"Neither file nor directory exists at \"{sourcePath}\" for copying."
            );
        }
    }

    private static void DeleteFileOrDirectory(string path)
    {
        _logger.Debug("Deleting \"{0}\"", path);

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
        else
        {
            _logger.Warning(
                $"Neither file nor directory exists at \"{path}\" for deleting."
            );
        }
    }

    private static string GetFileHash(string path)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            using (
                var stream = new BufferedStream(File.OpenRead(path), 262144)
            )
            {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash);
            }
        }
    }

    private static bool AreFilesEqual(string path0, string path1)
    {
        return GetFileHash(path0) == GetFileHash(path1);
    }

    private static void SynchronizeFileDeletions(
        ArchiveHandler transmitter,
        ArchiveHandler receiver,
        Action<string>? hook = null
    )
    {
        _logger.Debug(
            "Deleting files in the receiver that are not in the transmitter."
        );

        var directoryStack = new Stack<string>();
        directoryStack.Push(".");
        while (directoryStack.Count > 0)
        {
            string relativePath = directoryStack.Pop();
            _logger.Debug("Processing directory \"{0}\"", relativePath);
            var transmitterFiles = transmitter.ListFiles(relativePath);
            var receiverFiles = receiver.ListFiles(relativePath);
            int transmitterFilePtr = 0;
            int receiverFilePtr = 0;
            while (
                transmitterFilePtr < transmitterFiles.Length &&
                receiverFilePtr < receiverFiles.Length
            )
            {
                int ordinal = string.CompareOrdinal(
                    transmitterFiles[transmitterFilePtr],
                    receiverFiles[receiverFilePtr]
                );
                if (ordinal < 0)
                {
                    transmitterFilePtr++;
                }
                else if (ordinal == 0)
                {
                    string transmitterFilePath = Path.Combine(
                        transmitter.ArchivePath,
                        relativePath,
                        transmitterFiles[transmitterFilePtr]
                    );
                    if (Directory.Exists(transmitterFilePath))
                    {
                        directoryStack.Push(
                            Path.Combine(
                                relativePath,
                                transmitterFiles[transmitterFilePtr]
                            )
                        );
                    }

                    transmitterFilePtr++;
                    receiverFilePtr++;
                }
                else
                {
                    string pathToDelete = Path.Combine(
                        receiver.ArchivePath,
                        relativePath,
                        receiverFiles[receiverFilePtr]
                    );
                    hook?.Invoke($"Deleting \"{pathToDelete}\"");
                    DeleteFileOrDirectory(pathToDelete);
                    receiverFilePtr++;
                }
            }
            while (receiverFilePtr < receiverFiles.Length)
            {
                string pathToDelete = Path.Combine(
                    receiver.ArchivePath,
                    relativePath,
                    receiverFiles[receiverFilePtr]
                );
                hook?.Invoke($"Deleting \"{pathToDelete}\"");
                DeleteFileOrDirectory(pathToDelete);
                receiverFilePtr++;
            }
        }
    }

    private static void SynchronizeFileAdditionsAndChanges(
        ArchiveHandler transmitter,
        ArchiveHandler receiver,
        Action<string>? hook = null
    )
    {
        _logger.Debug(
            "Copying files from the transmitter to the receiver."
        );

        var directoryStack = new Stack<string>();
        directoryStack.Push(".");
        while (directoryStack.Count > 0)
        {
            string relativePath = directoryStack.Pop();
            var transmitterFiles = transmitter.ListFiles(relativePath);
            var receiverFiles = receiver.ListFiles(relativePath);
            int transmitterFilePtr = 0;
            int receiverFilePtr = 0;
            while (
                transmitterFilePtr < transmitterFiles.Length &&
                receiverFilePtr < receiverFiles.Length
            )
            {
                string transmitterFilePath = Path.Combine(
                    transmitter.ArchivePath,
                    relativePath,
                    transmitterFiles[transmitterFilePtr]
                );
                string receiverFilePath = Path.Combine(
                    receiver.ArchivePath,
                    relativePath,
                    transmitterFiles[transmitterFilePtr]
                );
                int ordinal = string.CompareOrdinal(
                    transmitterFiles[transmitterFilePtr],
                    receiverFiles[receiverFilePtr]
                );
                if (ordinal < 0)
                {
                    hook?.Invoke($"Copying \"{transmitterFilePath}\"");
                    CopyFileOrDirectory(transmitterFilePath, receiverFilePath);
                    transmitterFilePtr++;
                }
                else if (ordinal == 0)
                {
                    if (Directory.Exists(transmitterFilePath))
                    {
                        /// transmitterFilePath is a directory
                        directoryStack.Push(
                            Path.Combine(
                                relativePath,
                                transmitterFiles[transmitterFilePtr]
                            )
                        );
                    }
                    else
                    {
                        /// transmitterFilePath is a file
                        bool areFilesEqual = AreFilesEqual(
                            transmitterFilePath,
                            receiverFilePath
                        );
                        if (!areFilesEqual)
                        {
                            hook?.Invoke(
                                $"Copying \"{transmitterFilePath}\""
                            );
                            CopyFileOrDirectory(
                                transmitterFilePath,
                                receiverFilePath
                            );
                        }
                    }
                    transmitterFilePtr++;
                    receiverFilePtr++;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"ordinal > 0 is unexpected."
                    );
                }
            }
            while (transmitterFilePtr < transmitterFiles.Length)
            {
                string transmitterFilePath = Path.Combine(
                    transmitter.ArchivePath,
                    relativePath,
                    transmitterFiles[transmitterFilePtr]
                );
                string receiverFilePath = Path.Combine(
                    receiver.ArchivePath,
                    relativePath,
                    transmitterFiles[transmitterFilePtr]
                );
                hook?.Invoke($"Copying \"{transmitterFilePath}\"");
                CopyFileOrDirectory(transmitterFilePath, receiverFilePath);
                transmitterFilePtr++;
            }
        }
    }
}

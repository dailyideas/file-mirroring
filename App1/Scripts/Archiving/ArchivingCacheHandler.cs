// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

using Microsoft.Data.Sqlite;

namespace App1;

class ArchivingCacheHandler
{
    public string CachePath { get; private set; }
    private readonly SqliteConnection _connection;

    public ArchivingCacheHandler(string archivePath)
    {
        var archiveDirectoryInfo = new DirectoryInfo(archivePath);
        string? archiveParentDirectoryPath =
            archiveDirectoryInfo.Parent?.FullName;
        if (archiveParentDirectoryPath == null)
        {
            throw new ArgumentException(
                $"archivePath \"{archivePath}\" is invalid"
            );
        }

        CachePath = Path.Combine(
            archiveParentDirectoryPath,
            $"{archiveDirectoryInfo.Name}.db"
        );

        _connection = new SqliteConnection($"Data Source={CachePath}");
        if (!File.Exists(CachePath))
        {
            _connection.Open();
            CreateTableIfNotExists(_connection);
            _connection.Close();
        }
    }

    ~ArchivingCacheHandler()
    {
        _connection.Dispose();
    }

    public override bool Equals(object? obj)
    {
        var other = obj as ArchivingCacheHandler;
        if (other == null)
        {
            return false;
        }
        return CachePath == other.CachePath;
    }

    public override int GetHashCode()
    {
        return CachePath.GetHashCode();
    }

    public void InsertFileRenamingEvent(FileRenamingEvent e)
    {
        _connection.Open();
        var command = _connection.CreateCommand();
        command.CommandText = @"INSERT INTO file_renaming_events (
                source_relative_path,
                destination_relative_path
            )
            VALUES (
                @source_relative_path,
                @destination_relative_path
            );";
        command.Parameters.AddWithValue(
            "@source_relative_path",
            e.SourceRelativePath
        );
        command.Parameters.AddWithValue(
            "@destination_relative_path",
            e.DestinationRelativePath
        );
        command.ExecuteNonQuery();
        _connection.Close();
    }

    public FileRenamingEvent GetFileRenamingEvent(int eventID)
    {
        _connection.Open();
        var command = _connection.CreateCommand();
        command.CommandText = @"SELECT *
            FROM file_renaming_events
            WHERE event_id = @event_id;";
        command.Parameters.AddWithValue("@event_id", eventID);
        var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new ArgumentException(
                $"\"{eventID}\" is not a valid eventID"
            );
        }

        var e = new FileRenamingEvent(
            sourceRelativePath: reader.GetString(1),
            destinationRelativePath: reader.GetString(2),
            eventID: reader.GetInt32(0)
        );
        _connection.Close();
        return e;
    }

    public FileRenamingEvent GetLastFileRenamingEvent()
    {
        _connection.Open();
        var command = _connection.CreateCommand();
        command.CommandText = @"SELECT *
            FROM file_renaming_events
            ORDER BY event_id DESC;";
        var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new ArgumentException(
                "No file renaming event has been recorded"
            );
        }

        var e = new FileRenamingEvent(
            sourceRelativePath: reader.GetString(1),
            destinationRelativePath: reader.GetString(2),
            eventID: reader.GetInt32(0)
        );
        _connection.Close();
        return e;
    }

    public bool IsTheSameOrPredecessor(ArchivingCacheHandler other)
    {
        int lastEventID_this;
        try
        {
            lastEventID_this = GetLastFileRenamingEvent().EventID;
        }
        catch (ArgumentException)
        {
            return true;
        }

        int lastEventID_other;
        try
        {
            lastEventID_other = other.GetLastFileRenamingEvent().EventID;
        }
        catch (ArgumentException)
        {
            return false;
        }
        return lastEventID_this <= lastEventID_other;
    }

    private static void CreateTableIfNotExists(SqliteConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS file_renaming_events (
                event_id INTEGER PRIMARY KEY,
                source_relative_path TEXT NOT NULL,
                destination_relative_path TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();
    }
}

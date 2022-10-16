// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace App1;

public class FileRenamingEvent
{
    public int EventID { get; set; }
    public string SourceRelativePath { get; set; }
    public string DestinationRelativePath { get; set; }
    public static FileRenamingEvent Empty
    {
        get
        {
            return new FileRenamingEvent(
                sourceRelativePath: "",
                destinationRelativePath: "",
                eventID: 0
            );
        }
    }

    public static bool AreEqual(FileRenamingEvent e1, FileRenamingEvent e2)
    {
        return e1.EventID == e2.EventID &&
            e1.SourceRelativePath == e2.SourceRelativePath &&
            e1.DestinationRelativePath == e2.DestinationRelativePath;
    }

    public FileRenamingEvent(
        string sourceRelativePath,
        string destinationRelativePath,
        int eventID = -1
    )
    {
        EventID = eventID;
        SourceRelativePath = sourceRelativePath;
        DestinationRelativePath = destinationRelativePath;
    }

    public override string ToString()
    {
        return $"{{\"EventID\":{EventID}, \"Destination\":\"{DestinationRelativePath}\", \"Source\":\"{SourceRelativePath}\"}}";
    }
}

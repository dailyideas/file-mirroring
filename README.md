# file-mirroring

# Table of contents
- [About](#about)
- [Get started](#get-started)
    - Prerequisites
    - Gui
    - CLI
    - Remarks

# About
This tool helps users to maintain exact copies of a directory.

There are several terms to describe the methods which keeping multiple directories in sync \[[Art](https://www.tgrmn.com/web/kb/category6.htm)\]. We used "file mirroring" as the term to describe what this tool does. Given two directories, one has to be the source, and the other one will be the destination. This tool will modify the destination directory so that it appears the same as the source directory. The source directory will remain unchanged.

This tool is helpful when we want to maintain multiple cold backups. To update the backups, we just need to modify one of the backups. Other backups could be updated by mirroring the first one.

Googling "file sync" we could find loads of file synchronization softwares. They are handy and powerful. However, making a minimal one with a subset of their functionalities is fun. Moreover, we could optimize the synchronization process base on our need. One optimization is to prevent unnecessary coping when the file/directory was renamed or moved in the source directory, without the content being modified. As far as I know, most file synchronization softwares (except those with real-time monotoring) have no knowledge of the situation when the source is being mirrored to the destination directory. These renamed or moved files will be deleted on the destination directory, then copied again from the source directory. This could be optimized by monitoring and remembering the file events on the source directory, which is what this tool does.

Remarks:
Current version monitors file renaming events only. Functionality of monitoring moved files will be introduced in the next version.

# Get started
This tool can be used via both GUI and CLI.

## Prerequisites
1. Download the latest version of the app from the release page.
1. Unzip the archive.
1. You will find the app in the directory (windows: `file_mirroring.exe`; linux: `file_mirroring`).

## GUI

### A) Monitor file renaming/moving operations in the source directory only.
1. Start the app.
1. Browse and select the source directory.
1. Make changes on the directory.
1. After all changes have been made, close the app.

### B) File mirroring
1. Start the app.
1. Browse and select the source directory.
1. Browse and select the destination directory.
1. Click the "Synchronize" button.
1. Do not modify the source and destination directories before the synchronization process has completed.
1. A message dialog will pop up to inform the user when the synchronization process is completed. The message dialog and the app can be closed afterwards.

## CLI

### A) Monitor file renaming/moving operations in the source directory only.
1. (Windows as the example of the environment) Run `file_mirroring.exe <path_to_source_directory>`.
1. Make changes on the directory.
1. After all changes have been made, terminate the program.

### B) File mirroring
1. (Windows as the example of the environment) Run `file_mirroring.exe <path_to_source_directory> <destination_directory>`.
1. Press `Enter` key to start the mirroring process.
1. Wait until the process has been completed.

## Remarks
- Using this tool will create a `SQLite` database file named `directory_name.db` whenever a directory named `directory_name` is being selected as the source or destination directory. It is used to cache the file renaming/moving operations so that unnecessary copying could be prevented during file mirroring. Do not delete this file unless all target directories are in sync.

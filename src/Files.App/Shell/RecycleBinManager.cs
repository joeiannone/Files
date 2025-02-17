﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using Vanara.PInvoke;
using Vanara.Windows.Shell;
using Windows.Storage;

namespace Files.App.Shell
{
    public sealed class RecycleBinManager
    {
        private static readonly Lazy<RecycleBinManager> lazy = new(() => new RecycleBinManager());
        private IList<FileSystemWatcher>? binWatchers;
        
        public event FileSystemEventHandler? RecycleBinItemCreated;
        public event FileSystemEventHandler? RecycleBinItemDeleted;
        public event FileSystemEventHandler? RecycleBinItemRenamed;
        public event FileSystemEventHandler? RecycleBinRefreshRequested;

        public static RecycleBinManager Default
        {
            get
            {
                return lazy.Value;
            }
        }

        private RecycleBinManager()
        {
            Initialize();
        }
        
        private void Initialize()
        {
            // Create shell COM object and get recycle bin folder
            using var recycler = new ShellFolder(Shell32.KNOWNFOLDERID.FOLDERID_RecycleBinFolder);
            ApplicationData.Current.LocalSettings.Values["RecycleBin_Title"] = recycler.Name;

            StartRecycleBinWatcher();
        }

        private void StartRecycleBinWatcher()
        {
            // Create filesystem watcher to monitor recycle bin folder(s)
            // SHChangeNotifyRegister only works if recycle bin is open in explorer :(
            binWatchers = new List<FileSystemWatcher>();
            var sid = WindowsIdentity.GetCurrent().User.ToString();
            foreach (var drive in DriveInfo.GetDrives())
            {
                var recyclePath = Path.Combine(drive.Name, "$RECYCLE.BIN", sid);
                if (drive.DriveType == DriveType.Network || !Directory.Exists(recyclePath))
                {
                    continue;
                }
                FileSystemWatcher watcher = new FileSystemWatcher
                {
                    Path = recyclePath,
                    Filter = "*.*",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
                };
                watcher.Created += RecycleBinWatcher_Changed;
                watcher.Deleted += RecycleBinWatcher_Changed;
                watcher.EnableRaisingEvents = true;
                binWatchers.Add(watcher);
            }
        }

        private void RecycleBinWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Recycle bin event: {e.ChangeType}, {e.FullPath}");
            if (e.Name.StartsWith("$I", StringComparison.Ordinal))
            {
                // Recycle bin also stores a file starting with $I for each item
                return;
            }

            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    RecycleBinItemCreated?.Invoke(this, e);
                    break;
                case WatcherChangeTypes.Deleted:
                    RecycleBinItemDeleted?.Invoke(this, e);
                    break;
                case WatcherChangeTypes.Renamed:
                    RecycleBinItemRenamed?.Invoke(this, e);
                    break;
                default:
                    RecycleBinRefreshRequested?.Invoke(this, e);
                    break;
            }
        }

        private void Unregister()
        {
            if (binWatchers != null)
            {
                foreach (var watcher in binWatchers)
                {
                    watcher.Dispose();
                }
            }
        }

        ~RecycleBinManager()
        {
            Unregister();
        }
    }
}

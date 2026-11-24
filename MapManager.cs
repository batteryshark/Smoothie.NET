using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Smoothie
{
    class MapManager
    {
        internal const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x400;
        internal const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern uint GetFileAttributes(string lpFileName);

        [DllImport("kernel32.dll")]
        static extern bool CreateSymbolicLink(
        string lpSymlinkFileName, string lpTargetFileName, SymbolicLink dwFlags);

        enum SymbolicLink
        {
            File = 0,
            Directory = 1
        }


        public static void CreateSymlink(String src_path, String dest_path)
        {
            // Symlinks on windows can be up to 32768 characters in length provided they are prepended with '\\\\?\\'
            String full_taget_path = "\\\\?\\" + dest_path;
            CreateSymbolicLink(full_taget_path, src_path, SymbolicLink.File);

        }

        // Platform-Dependent Functionality
        // Windows doesn't play well with creating a symlink or detecting one via stdlib for whatever reason...
        public static Boolean IsSymlink(string in_path)
        {
            return ((GetFileAttributes(in_path) & FILE_ATTRIBUTE_REPARSE_POINT) != 0);
        }

        public static Boolean IsDirectory(string in_path)
        {
            return ((GetFileAttributes(in_path) & FILE_ATTRIBUTE_DIRECTORY) != 0);
        }

        public static Boolean IsFile(string in_path)
        {
            return IsDirectory(in_path) == false;
        }

        // Creates a 'mapped' representation of the absolute path by conversion to a stem prepended to a given root.
        // e.g. C:\\app\\hello.exe, C:\\map -> C:\\map\\C\\app\\hello.exe
        public static String ResolvePath(String map_root, String virtual_path)
        {
            // If there's no virtual path given, we assume the map root.
            if (virtual_path == "")
            {
                return map_root;
            }

            // Remove any : from the path (Windows Paths)
            String node = virtual_path.Replace(":", "");

            // Appending a path with a node that starts with a directory separator can screw it up.
            // It results in the whole path being reset with the node representing root (which we don't want)
            // Move the node ahead one character if this is the case.
            if (node.StartsWith("\\") || node.StartsWith("/"))
            {
                node = node.Substring(1);
            }


            return Path.Combine(map_root, node);
        }

        // Given a map root, remove all empty directories and symlinks.
        // If a persistence path was specified, move any real files to 
        // a cloned path in the persistence root.
        public static void Cleanup(string map_root, string persistence_root)
        {
            // Do nothing if the map root doesn't exist.
            if (!Directory.Exists(map_root)) { return; }
            if (!map_root.EndsWith("\\")) { map_root += "\\"; }
            Boolean persist = Directory.Exists(persistence_root);
            // Remove the symlinks - if there are any files that were written, move those to persistence
            // or remove them.
            string[] entries = Directory.GetFileSystemEntries(map_root, "*", SearchOption.AllDirectories);
            foreach (var entry in entries)
            {
                if (!IsDirectory(entry))
                {
                    if (!persist || IsSymlink(entry))
                    {
                        File.Delete(entry);
                    }
                    else
                    {
                        String persistence_path = RebasePath(entry, map_root, persistence_root);
                        Directory.CreateDirectory(Directory.GetParent(persistence_path).FullName);
                        if (File.Exists(persistence_path))
                        {
                            File.Delete(persistence_path);
                        }
                        File.Copy(entry, persistence_path);
                    }
                }
            }

            // Remove all Leftover Directories
            foreach (var entry in entries)
            {
                if (IsDirectory(entry))
                {
                    try
                    {
                        Directory.Delete(entry, true);
                    }
                    catch { }

                }
            }

            // Remove all that remains from map
            try
            {
                Directory.Delete(map_root, true);
            }
            catch { }
        }

        public static string GetRelativePath(string relativeTo, string path)
        {
            var uri = new Uri(relativeTo);
            var rel = Uri.UnescapeDataString(uri.MakeRelativeUri(new Uri(path)).ToString()).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            return rel;
        }

        internal static void MapPath(string map_root, string src_root, string virtual_root)
        {
            if (!src_root.EndsWith("\\")) { src_root += "\\"; }
            String mapped_base = ResolvePath(map_root, virtual_root);
            if (!Directory.Exists(mapped_base))
            {
                Directory.CreateDirectory(mapped_base);
            }
            string[] entries = Directory.GetFileSystemEntries(src_root, "*", SearchOption.AllDirectories);

            foreach (var entry in entries)
            {
                if (IsDirectory(entry))
                {
                    Directory.CreateDirectory(RebasePath(entry, src_root, mapped_base));
                }
            }

            foreach (var entry in entries)
            {
                String rebased_path = RebasePath(entry, src_root, mapped_base);
                if (!IsDirectory(entry))
                {
                    if (File.Exists(rebased_path) || IsSymlink(rebased_path))
                    {
                        try
                        {
                            File.Delete(rebased_path);
                        }
                        catch { 
                        }
                    }
                    Directory.CreateDirectory(Directory.GetParent(entry).FullName);
                    CreateSymlink(entry, rebased_path);
                }
            }
        }

        // Given a virtual path, resolve to its mapped path and remove if it exists.
        internal static void RemoveNode(string map_root, string target_path)
        {
            String mapped_path = ResolvePath(map_root, target_path);
            if (File.Exists(mapped_path)) { File.Delete(mapped_path); }
        }

        // Given a real source file path and virtual path, resolve to its mapped path and create a symbolic link.
        internal static void LinkNode(string map_root, string src_path, string target_path)
        {
            String mapped_path = ResolvePath(map_root, target_path);
            CreateSymlink(src_path, mapped_path);

        }

        // Separates a path from a given root and attaches a new root to it.
        // e.g. C:\\test\\app\\banana\\example.txt, C:\\test\\app, C:\\wat -> C:\\wat\\banana\\example.txt
        private static string RebasePath(string in_path, string old_root, string new_root)
        {
            String relative_stem = GetRelativePath(old_root, in_path);

            return Path.Combine(new_root, relative_stem);
        }
    }
}

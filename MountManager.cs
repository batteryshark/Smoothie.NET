using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Smoothie
{
    class MountEntry
    {
        public String full_image_path { get; set; }
        public String full_mount_point { get; set; }

    }

    static class MountManager
    {
        static List<MountEntry> mount_entries = new List<MountEntry>();

        public static String ResolveContentPath(String content_root, String image_filename)
        {
            // If it's a literal path, we'll return that instead.
            if(File.Exists(image_filename) || Directory.Exists(image_filename))
            {
                return image_filename;
            }
            // Resolve a content path based on local or global root.
            String local_path = Path.Combine(content_root, image_filename);
            if (File.Exists(local_path) || Directory.Exists(local_path)) { return local_path; }

            if (Environment.GetEnvironmentVariable("SGCROOT") != null)
            {
                String global_path = Path.Combine(Environment.GetEnvironmentVariable("SGCROOT"), image_filename);
                if (File.Exists(global_path) || Directory.Exists(global_path)) { return global_path; }
            }
            return "";
        }

        // Use the extension to figure out what to do with the given image.
        private static string DetectMountType(string path_to_image)
        {
            String extension = Path.GetExtension(path_to_image);
            if (Directory.Exists(path_to_image)) { return "RAW"; }
            extension = extension.ToLower();
            switch (extension)
            {
                case ".iso":
                case ".vhd":
                case ".vhdx":
                    return "IMAGE";
                case ".zip":
                    return "ZIP";
            }
            return "UNKNOWN";
        }

        internal static bool AddMount(string image_filename, string image_path, string mount_path)
        {
            // Detect type of image, fail out if not.
            String mount_type = DetectMountType(image_filename);
            if (mount_type == "") { return false; }

            // Construct Mountpoint Path if not a RAW mount
            if(mount_type != "RAW")
            {
                Directory.CreateDirectory(mount_path);
            }
            
            switch (mount_type)
            {
                case "IMAGE":
                    if (!Virtlib.MountImage(image_path, mount_path)) { return false; }
                    break;
                case "RAW":
                    // A RAW mount doesn't need a dedicated mount path.
                    mount_path = image_path;
                    break;
                case "ZIP":
                    try { ZipFile.ExtractToDirectory(image_path, mount_path); } catch { return false; }
                    break;
            }

            mount_entries.Add(new MountEntry
            {
                full_image_path = image_path,
                full_mount_point = mount_path
            });

            return true;
        }

        public static Boolean RemoveMount(String path_to_image, String path_to_mountpoint)
        {
            // Detect type of image, fail out if not.
            switch (DetectMountType(path_to_image))
            {
                case "IMAGE":
                    if (!Virtlib.UnmountImage(path_to_image, path_to_mountpoint)) { return false; }
                    Directory.Delete(path_to_mountpoint, true);
                    break;
                case "RAW":
                    // We do nothing because we created nothing.
                    break;
                case "ZIP":
                    Directory.Delete(path_to_mountpoint, true);
                    break;
            }
            return true;
        }

        // If there was an issue mounting, we will unmount what was mounted so far.
        public static void RollbackMounts()
        {
            foreach (var entry in mount_entries)
            {
                RemoveMount(entry.full_image_path, entry.full_mount_point);
            }
        }

        // Read all entries in mount config and unmount them.
        public static void RemoveMountsFromConf(String path_to_conffile)
        {
            // If there isn't a conf file, do nothing.
            if (!File.Exists(path_to_conffile)) { return; }
            string[] lines = File.ReadAllLines(path_to_conffile);
            foreach (var line in lines)
            {
                string[] pm = line.Split(';');
                if (!RemoveMount(pm[0], pm[1]))
                {
                    Console.WriteLine($"Error: Could Not Remove Mount: {pm[0]} {pm[1]}");
                }
            }

        }

        // Write every image mounted along with their mountpoints to a config file for later.
        internal static void SaveMountsConf(string mounts_conf_path)
        {
            List<String> conf_lines = new List<String>();
            foreach (var entry in mount_entries)
            {
                conf_lines.Add(entry.full_image_path + ";" + entry.full_mount_point);
            }

            File.WriteAllLines(mounts_conf_path, conf_lines);
        }
    }
}

using System;
using System.IO;

namespace Smoothie
{
    public class Smoothie
    {
        private static String mount_root;
        private static String map_root;
        private static String mounts_conf_path;
        private static String persistence_conf_path;


        // Rolling back in case something went wrong.
        private static void FailureCleanup()
        {
            MountManager.RollbackMounts();
            Directory.Delete(map_root, true);
            Directory.Delete(mount_root, true);
        }


        private static void InitPaths(String path_to_root)
        {
            mount_root = Path.Combine(path_to_root, "mnt");
            map_root = Path.Combine(path_to_root, "map");
            mounts_conf_path = Path.Combine(path_to_root, "mounts.conf");
            persistence_conf_path = Path.Combine(path_to_root, "persistence.conf");
            PrivligesNative.AdjustPrivileges();
        }
        public static Boolean Create(String path_to_mapfile, String path_to_root, String path_to_persistence)
        {
            // Initialize Our Global Paths
            InitPaths(path_to_root);

            // Create our mount and map roots if they don't exist already.
            Directory.CreateDirectory(mount_root);
            Directory.CreateDirectory(map_root);

            // If this smoothie is live, we need to kill it before proceeding.
            if (File.Exists(mounts_conf_path))
            {
                Console.WriteLine("Root Already Mounted - Unmounting First...");
                Destroy(path_to_root);
            }

            // Read our Mapfile - Figure out what we have to do.
            if (!File.Exists(path_to_mapfile))
            {
                Console.WriteLine("Mapfile does not Exist!\n");
                return false;
            }
            String content_path = Directory.GetParent(path_to_mapfile).FullName;
            string[] lines = File.ReadAllLines(path_to_mapfile);
            foreach (var line in lines)
            {
                // Skip Comments
                if (line.StartsWith("#")) { continue; }
                string[] prms = line.Split(';');
                if (prms.Length != 2 && prms.Length != 3) { continue; }

                // MAP;image_name.ext;virtual_path
                if (prms[0] == "MAP")
                {
                    // Resolve Image Path (from global or local content root)
                    String full_image_path = MountManager.ResolveContentPath(content_path, prms[1]);
                    if (full_image_path == "")
                    {
                        Console.WriteLine($"Could not Resolve Image Path: {prms[1]}");
                        return false;
                    }
                    String mount_path = Path.Combine(mount_root, Path.GetFileNameWithoutExtension(prms[1]));
                    if (!MountManager.AddMount(prms[1], full_image_path, mount_path))
                    {
                        Console.WriteLine($"Failed to Mount: {prms[1]}");
                        FailureCleanup();
                        return false;
                    }
                    // We now have to merge our changes onto the composite.
                    MapManager.MapPath(map_root, mount_path, prms[2]);
                }
                else if (prms[0] == "REMOVE")
                {
                    MapManager.RemoveNode(map_root, prms[1]);
                }
                else if (prms[0] == "LINK")
                {
                    String full_item_path = MountManager.ResolveContentPath(content_path, prms[1]);
                    MapManager.LinkNode(map_root, full_item_path, prms[2]);
                }
                else
                {
                    Console.WriteLine($"Unrecognized Map Command: {line}");
                }
            }

            // We're ready to write our mounts config file now.
            MountManager.SaveMountsConf(mounts_conf_path);

            // If we specified a persistence root:
            // Create if it didn't exist, map what existed already, and write the path to our config.
            if (path_to_persistence != "")
            {
                Directory.CreateDirectory(path_to_persistence);
                MapManager.MapPath(map_root, path_to_persistence, "");
                File.WriteAllText(persistence_conf_path, path_to_persistence);
            }

            return true;
        }

        public static Boolean Destroy(String path_to_root)
        {
            // Initialize Our Global Paths
            InitPaths(path_to_root);

            // Read given Configuration Files
            String persistence_root = "";
            if (File.Exists(persistence_conf_path))
            {
                persistence_root = File.ReadAllText(persistence_conf_path);
            }

            // Read the mounts that were mounted and unmount them
            MountManager.RemoveMountsFromConf(mounts_conf_path);

            // Remove Mounts Config
            if (File.Exists(mounts_conf_path)) { File.Delete(mounts_conf_path); }

            // Remove contents of map root - persist migrate if necessary.
            MapManager.Cleanup(map_root, persistence_root);

            // Remove Persistence Config
            if (File.Exists(persistence_conf_path)) { File.Delete(persistence_conf_path); }

            // Remove Map Directory
            if (Directory.Exists(map_root))
            {
                try
                {
                    Directory.Delete(map_root, true);
                }
                catch { return false; }

            }

            // Remove Mounts Directory
            if (Directory.Exists(mount_root))
            {
                try
                {
                    Directory.Delete(mount_root, true);
                }
                catch { return false; }
            }

            return true;
        }

        public static Boolean Resolve(String path_to_root, String virtual_path, out String out_path)
        {
            InitPaths(path_to_root);
            out_path = MapManager.ResolvePath(map_root, virtual_path);
            if (out_path == "") { return false; }

            return true;
        }
    }
}

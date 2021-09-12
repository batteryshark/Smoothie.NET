using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Smoothie
{
    internal class Virtlib
    {
        public const int FILE_DEVICE_CD_ROM = 0x2;
        public const int ERROR_NO_MORE_FILES = 18;
        public const int FILE_DEVICE_DISK = 0x7;
        public const int OPEN_EXISTING = 3;
        public const int FILE_SHARE_READ = 1;
        internal const uint GENERIC_READ = 0x80000000;
        public const int FILE_SHARE_WRITE = 2;
        static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public const int SDDL_REVISION_1 = 1;


        public const int IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x2D1080;
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STORAGE_DEVICE_NUMBER
        {
            public uint DeviceType;
            public uint DeviceNumber;
            public uint PartitionNumber;
        }


        [DllImport("kernel32.dll")]
        static extern bool SetVolumeMountPoint(string lpszVolumeMountPoint, string lpszVolumeName);

        [DllImport("kernel32.dll")]
        public static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, ref uint lpBytesReturned, IntPtr lpOverlapped);
        [DllImport("kernel32.dll")]
        public static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, ref STORAGE_DEVICE_NUMBER lpOutBuffer, uint nOutBufferSize, ref uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", EntryPoint = "FindNextVolume",CharSet = CharSet.Unicode, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool FindNextVolume(
                  IntPtr hFindVolume,
                  StringBuilder lpszVolumeName,
                  int cchBufferLength);

        [DllImport("kernel32.dll", EntryPoint = "FindFirstVolume", CharSet = CharSet.Unicode, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr FindFirstVolume(
  StringBuilder lpszVolumeName,
  int cchBufferLength);

        public static List<string> GetVolumes()
        {

            const int N = 1024;
            StringBuilder cVolumeName = new StringBuilder((int)N);
            List<string> ret = new List<string>();
            IntPtr volume_handle = FindFirstVolume(cVolumeName, N);
            do
            {
                ret.Add(cVolumeName.ToString());

            } while (FindNextVolume(volume_handle, cVolumeName, N) && Marshal.GetLastWin32Error() != ERROR_NO_MORE_FILES);
            return ret;
        }


        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool FindVolumeClose(IntPtr hFindVolume);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern internal IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);


        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        public static extern Int32 GetVirtualDiskPhysicalPath(IntPtr Handle, ref Int32 DiskPathSizeInBytes, StringBuilder Path);

        [DllImport("Virtdisk.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern int AttachVirtualDisk(IntPtr VirtualDiskHandle, IntPtr SecurityDescriptor, ATTACH_VIRTUAL_DISK_FLAG Flags, uint ProviderSpecificFlags, ref ATTACH_VIRTUAL_DISK_PARAMETERS Parameters, IntPtr Overlapped);


        [Flags()]
        public enum ATTACH_VIRTUAL_DISK_FLAG
        {
            ATTACH_VIRTUAL_DISK_FLAG_NONE = 0x0,

            // Attach the disk as read only
            ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY = 0x1,

            // Will cause all volumes on the disk to be mounted
            // without drive letters.
            ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER = 0x2,

            // Will decouple the disk lifetime from that of the VirtualDiskHandle.
            // The disk will be attached until an explicit call is made to
            // DetachVirtualDisk even if all handles are closed.
            ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME = 0x4,

            // Indicates that the drive will not be attached to
            // the local system (but rather to a VM).
            ATTACH_VIRTUAL_DISK_FLAG_NO_LOCAL_HOST = 0x8
        }

        [Flags()]
        public enum ATTACH_VIRTUAL_DISK_VERSION
        {
            ATTACH_VIRTUAL_DISK_VERSION_UNSPECIFIED = 0,
            ATTACH_VIRTUAL_DISK_VERSION_1 = 1

        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct ATTACH_VIRTUAL_DISK_PARAMETERS
        {
            public ATTACH_VIRTUAL_DISK_VERSION Version;
            public int Reserved;
        }

        [DllImport("Advapi32.dll")]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
            string StringSecurityDescriptor,
            uint StringSDRevision,
            out IntPtr SecurityDescriptor,
            IntPtr SecurityDescriptorSize
            );

        [DllImport("kernel32.dll")]
        static extern bool DeleteVolumeMountPoint(string lpszVolumeMountPoint);

        [DllImport("kernel32.dll", SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        public enum DETACH_FLAG
        {
            NONE = 0x00000000
        }

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        public static extern Int32 DetachVirtualDisk(IntPtr Handle, DETACH_FLAG Flag, Int32 ProviderSpecificFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct VIRTUAL_STORAGE_TYPE
        {
            public uint DeviceId;

            // <MarshalAs(UnmanagedType.ByValArray, ArraySubType:=UnmanagedType.U1, SizeConst:=16)> _

            [MarshalAs(UnmanagedType.Struct)]
            public Guid VendorId;
        }

        /// <summary>
        /// Contains the bit mask for specifying access rights to a virtual hard disk (VHD).
        /// </summary>
        [Flags]
        public enum VirtualDiskAccessMask : int
        {
            /// <summary>
            /// Open the virtual disk for read-only attach access. The caller must have READ access to the virtual disk image file. If used in a request to open a virtual disk that is already open, the other handles must be limited to either VIRTUAL_DISK_ACCESS_DETACH or VIRTUAL_DISK_ACCESS_GET_INFO access, otherwise the open request with this flag will fail.
            /// </summary>
            AttachReadOnly = 0x00010000,
            /// <summary>
            /// Open the virtual disk for read-write attaching access. The caller must have (READ | WRITE) access to the virtual disk image file. If used in a request to open a virtual disk that is already open, the other handles must be limited to either VIRTUAL_DISK_ACCESS_DETACH or VIRTUAL_DISK_ACCESS_GET_INFO access, otherwise the open request with this flag will fail. If the virtual disk is part of a differencing chain, the disk for this request cannot be less than the RWDepth specified during the prior open request for that differencing chain.
            /// </summary>
            AttachReadWrite = 0x00020000,
            /// <summary>
            /// Open the virtual disk to allow detaching of an attached virtual disk. The caller must have (FILE_READ_ATTRIBUTES | FILE_READ_DATA) access to the virtual disk image file.
            /// </summary>
            Detach = 0x00040000,
            /// <summary>
            /// Information retrieval access to the VHD. The caller must have READ access to the virtual disk image file.
            /// </summary>
            GetInfo = 0x00080000,
            /// <summary>
            /// VHD creation access.
            /// </summary>
            Create = 0x00100000,
            /// <summary>
            /// Open the VHD to perform offline meta-operations. The caller must have (READ | WRITE) access to the virtual disk image file, up to RWDepth if working with a differencing chain. If the VHD is part of a differencing chain, the backing store (host volume) is opened in RW exclusive mode up to RWDepth.
            /// </summary>
            MetaOperations = 0x00200000,
            /// <summary>
            /// Allows unrestricted access to the VHD. The caller must have unrestricted access rights to the virtual disk image file.
            /// </summary>
            All = 0x003f0000,
        }

        /// <summary>
        /// Contains virtual disk attach request flags.
        /// </summary>
        [Flags]
        public enum VirtualDiskAttachOptions
        {
            /// <summary>
            /// No flags. Use system defaults.
            /// </summary>
            None = 0x00000000,
            /// <summary>
            /// Attach the virtual disk as read-only.
            /// </summary>
            ReadOnly = 0x00000001,
            /// <summary>
            /// Will cause all volumes on the attached virtual disk to be mounted without assigning drive letters to them.
            /// </summary>
            NoDriveLetter = 0x00000002,
            /// <summary>
            /// Will decouple the virtual disk lifetime from that of the VirtualDiskHandle. The virtual disk will be attached until the DetachVirtualDisk function is called, even if all open handles to the virtual disk are closed.
            /// </summary>
            PermanentLifetime = 0x00000004,
            /// <summary>
            /// Reserved.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "LocalHost", Justification = "Naming is same as in Windows API definition.")]
            NoLocalHost = 0x00000008,
        }

        /// <summary>
        /// Options for create operations.
        /// </summary>
        [Flags]
        public enum VirtualDiskCreateOptions : int
        {
            /// <summary>
            /// No additional options are set.
            /// </summary>
            None = 0x00000000,
            /// <summary>
            /// Pre-allocate all physical space necessary for the size of the virtual disk.
            /// </summary>
            FullPhysicalAllocation = 0x00000001,
        }

        public const int VIRTUAL_STORAGE_TYPE_DEVICE_UNKNOWN = 0;
        public const int VIRTUAL_STORAGE_TYPE_DEVICE_ISO = 1;
        public const int VIRTUAL_STORAGE_TYPE_DEVICE_VHD = 2;
        public const int VIRTUAL_STORAGE_TYPE_DEVICE_VHDX = 3;


        public enum OPEN_VIRTUAL_DISK_VERSION
        {
            OPEN_VIRTUAL_DISK_VERSION_UNSPECIFIED = 0,
            OPEN_VIRTUAL_DISK_VERSION_1 = 1,
            OPEN_VIRTUAL_DISK_VERSION_2 = 2
        }

        [Flags()]
        public enum VIRTUAL_DISK_ACCESS_MASK
        {
            VIRTUAL_DISK_ACCESS_NONE = 0x0,
            VIRTUAL_DISK_ACCESS_ATTACH_RO = 0x10000,
            VIRTUAL_DISK_ACCESS_ATTACH_RW = 0x20000,
            VIRTUAL_DISK_ACCESS_DETACH = 0x40000,
            VIRTUAL_DISK_ACCESS_GET_INFO = 0x80000,
            VIRTUAL_DISK_ACCESS_CREATE = 0x100000,
            VIRTUAL_DISK_ACCESS_METAOPS = 0x200000,
            VIRTUAL_DISK_ACCESS_READ = 0xD0000,
            VIRTUAL_DISK_ACCESS_ALL = 0x3F0000,
            //
            //
            // A special flag to be used to test if the virtual disk needs to be
            // opened for write.
            //
            VIRTUAL_DISK_ACCESS_WRITABLE = 0x320000
        }

        [Flags()]
        public enum OPEN_VIRTUAL_DISK_FLAG
        {
            OPEN_VIRTUAL_DISK_FLAG_NONE = 0x0,
            // Open the backing store without opening any differencing chain parents.
            // This allows one to fixup broken parent links.
            OPEN_VIRTUAL_DISK_FLAG_NO_PARENTS = 0x1,

            // The backing store being opened is an empty file. Do not perform virtual
            // disk verification.
            OPEN_VIRTUAL_DISK_FLAG_BLANK_FILE = 0x2,

            // This flag is only specified at boot time to load the system disk
            // during virtual disk boot.  Must be kernel mode to specify this flag.
            OPEN_VIRTUAL_DISK_FLAG_BOOT_DRIVE = 0x4,

            // This flag causes the backing file to be opened in cached mode.
            OPEN_VIRTUAL_DISK_FLAG_CACHED_IO = 0x8,

            // Open the backing store without opening any differencing chain parents.
            // This allows one to fixup broken parent links temporarily without updating
            // the parent locator.
            OPEN_VIRTUAL_DISK_FLAG_CUSTOM_DIFF_CHAIN = 0x10,

            // This flag causes all backing stores except the leaf backing store to
            // be opened in cached mode.
            OPEN_VIRTUAL_DISK_FLAG_PARENT_CACHED_IO = 0x20
        }




        public struct OPEN_VIRTUAL_DISK_PARAMETERS
        {
            // Token: 0x04000110 RID: 272
            public OPEN_VIRTUAL_DISK_VERSION Version;

            // Token: 0x04000111 RID: 273
            public uint RWDepth;
        }



        public static readonly Guid VIRTUAL_STORAGE_TYPE_VENDOR_UNKNOWN = Guid.Empty;
        public static readonly Guid VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT = new Guid("EC984AEC-A0F9-47e9-901F-71415A66345B");

        [DllImport("Virtdisk.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern int OpenVirtualDisk(ref VIRTUAL_STORAGE_TYPE VirtualStorageType, [MarshalAs(UnmanagedType.LPWStr)] string Path, VIRTUAL_DISK_ACCESS_MASK VirtualDiskAccessMask, OPEN_VIRTUAL_DISK_FLAG Flags, ref OPEN_VIRTUAL_DISK_PARAMETERS Parameters, ref IntPtr Handle);

        public static bool UnmountImage(string path_to_image, string path_to_mountpoint)
        {
            IntPtr hDisk;
            if (!open_virtual_disk(path_to_image, out hDisk)) { return false; }

            bool result = unmount_virtual_disk(hDisk, path_to_mountpoint);
            CloseHandle(hDisk);
            return result;
        }

        public static bool MountImage(string path_to_image, string path_to_mountpoint)
        {
            IntPtr hDisk;
            if (!open_virtual_disk(path_to_image, out hDisk)) { return false; }

            bool result = mount_virtual_disk(hDisk, path_to_mountpoint);
            CloseHandle(hDisk);
            return result;
        }

        private static bool open_virtual_disk(string path_to_image, out IntPtr hDisk)
        {

            var vdp = new OPEN_VIRTUAL_DISK_PARAMETERS();
            vdp.Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1;

            VIRTUAL_STORAGE_TYPE vst;
            vst.DeviceId = VIRTUAL_STORAGE_TYPE_DEVICE_UNKNOWN;
            vst.VendorId = VIRTUAL_STORAGE_TYPE_VENDOR_UNKNOWN;

            var am = VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_ATTACH_RO | VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_READ | VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_GET_INFO;
            hDisk = IntPtr.Zero;

            int res = OpenVirtualDisk(ref vst, path_to_image, am, OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE, ref vdp, ref hDisk);
            if (res != 0)
            {
                Console.WriteLine("OpenVirtualDisk Failed: " + res);
                return false;
            }
            return true;
        }

        private static bool mount_virtual_disk(IntPtr hDisk, string path_to_mountpoint)
        {
            if (!path_to_mountpoint.EndsWith("\\"))
            {
                path_to_mountpoint += "\\";
            }

            ATTACH_VIRTUAL_DISK_PARAMETERS attachParameters = new ATTACH_VIRTUAL_DISK_PARAMETERS();

            UIntPtr sz = UIntPtr.Zero;
            attachParameters.Version = ATTACH_VIRTUAL_DISK_VERSION.ATTACH_VIRTUAL_DISK_VERSION_1;
            IntPtr sd = IntPtr.Zero;
            if (!ConvertStringSecurityDescriptorToSecurityDescriptor("O:BAG:BAD:(A;;GA;;;WD)", 1, out sd, IntPtr.Zero))
            {
                Console.WriteLine("ConvertStringSecurityDescriptorToSecurityDescriptorA Failed");
                return false;
            }
            ATTACH_VIRTUAL_DISK_FLAG attach_flags = ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY | ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER | ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME;
            if (AttachVirtualDisk(hDisk, sd, (ATTACH_VIRTUAL_DISK_FLAG)attach_flags, 0, ref attachParameters, IntPtr.Zero) != 0)
            {
                Console.WriteLine("AttachVirtualDisk Failed!");
                return false;
            }


            int capacity = 1024;
            StringBuilder stringBuilder = new StringBuilder(capacity);
            if (GetVirtualDiskPhysicalPath(hDisk, ref capacity, stringBuilder) != 0)
            {
                DetachVirtualDisk(hDisk, DETACH_FLAG.NONE, 0);
                Console.WriteLine("GetVirtualDiskPhysicalPath Failed");
                return false;
            }

            String vdpp = stringBuilder.ToString();
            uint device_index = 0;
            uint device_index_type = 0;

            if (vdpp.Contains("\\\\.\\PhysicalDrive"))
            {
                device_index = uint.Parse(vdpp.Replace("\\\\.\\PhysicalDrive", ""));
                device_index_type = FILE_DEVICE_DISK;
            }
            else if (vdpp.Contains("\\\\.\\CDROM"))
            {
                device_index = uint.Parse(vdpp.Replace("\\\\.\\CDROM", ""));
                device_index_type = FILE_DEVICE_CD_ROM;
            }
            else
            {
                Console.WriteLine("[mount_virtual_disk] Failed: Could not Identify Disk Type");
                DetachVirtualDisk(hDisk, DETACH_FLAG.NONE, 0);
                return false;
            }

            List<String> volumes = GetVolumes();

            String selected_volume = "";
            foreach (var volume_path in volumes)
            {
                String candidate_volume = volume_path;
                if (candidate_volume.EndsWith("\\"))
                {
                    candidate_volume = candidate_volume.Remove(volume_path.Length - 1, 1);
                }


                IntPtr cVol = CreateFile(candidate_volume, 0, FILE_SHARE_READ, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (cVol == INVALID_HANDLE_VALUE)
                {
                    continue;
                }

                uint requiredSize = 0;
                STORAGE_DEVICE_NUMBER sdn = new STORAGE_DEVICE_NUMBER();
                sdn.DeviceNumber = 0;
                sdn.DeviceType = 0;
                int nBytes = Marshal.SizeOf(sdn);
                IntPtr ptrSdn = Marshal.AllocHGlobal(nBytes);
                bool res = DeviceIoControl(cVol, IOCTL_STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0, ptrSdn, (uint)nBytes, ref requiredSize, IntPtr.Zero);
                CloseHandle(cVol);
                if (res)
                {
                    sdn = (STORAGE_DEVICE_NUMBER)Marshal.PtrToStructure(ptrSdn, typeof(STORAGE_DEVICE_NUMBER));

                    if (sdn.DeviceNumber == device_index && sdn.DeviceType == device_index_type)
                    {
                        selected_volume = volume_path;
                        if (!volume_path.EndsWith("\\"))
                        {
                            selected_volume += "\\";
                        }
                        break;
                    }
                }
                else
                {
                    Console.WriteLine("DeviceIoControl Failed: " + Marshal.GetLastWin32Error());
                }
                Marshal.FreeHGlobal(ptrSdn);

            }

            if (selected_volume == "")
            {
                Console.WriteLine("Target Volume Could not be Found");
                DetachVirtualDisk(hDisk, DETACH_FLAG.NONE, 0);
                return false;
            }

            if (!SetVolumeMountPoint(path_to_mountpoint, selected_volume))
            {
                Console.WriteLine("Failed to Set Volume Mountpoint: " + Marshal.GetLastWin32Error());
                DetachVirtualDisk(hDisk, DETACH_FLAG.NONE, 0);
                return false;
            }
            return true;
        }

        private static bool unmount_virtual_disk(IntPtr hDisk, string path_to_mountpoint)
        {
            if (!path_to_mountpoint.EndsWith("\\"))
            {
                path_to_mountpoint += "\\";
            }

            if (!DeleteVolumeMountPoint(path_to_mountpoint))
            {
                Console.WriteLine("Failed to Delete Volume Mountpoint.");
                return false;
            }

            if (DetachVirtualDisk(hDisk, DETACH_FLAG.NONE, 0) != 0)
            {
                Console.WriteLine("DetachVirtualDisk Fail!");
                return false;
            }

            return true;
        }
    }
}
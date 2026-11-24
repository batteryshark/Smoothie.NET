# Smoothie.NET

A C# class library for Windows that builds a layered filesystem out of disk
images and ZIPs, using mounts and symbolic links.

This is the managed implementation of Smoothie from part two of my VX write-up,
[Masterpiece - Implementing a Layered Filesystem](https://batteryshark.github.io/2021/10/25/masterpiece-implementing-a-layered-filesystem-part-two.html).
[libsmoothie](https://github.com/batteryshark/libsmoothie) is the native C++
original; this is a port of it with the same map format and the same working
directory layout.

**Status: a finished 2021 library, unchanged since apart from removing dead
code and copied SDK header comments. Windows only, needs an elevated process, and targets .NET 5.0, which is
out of support. Details under [Status and limits](#status-and-limits).**

## Why this exists

VX packages an application as a set of read-only images: a base layer, maybe an
update on top, maybe a disc image the app expects in drive D. Something has to
turn that pile into one directory tree the app can run from, apply small edits
(hide this file, swap that one), and keep whatever the app writes apart from the
images.

Smoothie is that compositor. It doesn't hook anything and has no driver. It
mounts what needs mounting and builds the tree out of ordinary directories and
symlinks, so the result is something you can look at in Explorer.

The C# version exists for the managed side of VX.
[VXTools.NET](https://github.com/batteryshark/VXTools.NET) references this DLL
and calls it directly, where the native
[vxlauncher](https://github.com/batteryshark/vxlauncher) loads libsmoothie.

## How it works

`Create` reads a map file one line at a time. For each `MAP` line it finds the
image, makes it readable as a directory, then walks it and mirrors it into the
map root: a real directory for every directory, a file symlink for every file.
Later lines land on top of earlier ones, so a second `MAP` over the same virtual
path replaces the files it shares with the first. That is the layering.

How an image becomes a directory depends on what it is:

- `.iso`, `.vhd`, `.vhdx` are attached read-only through `virtdisk.dll` with no
  drive letter. The new volume is found by its device number and given a mount
  point under `mnt\`.
- `.zip` is extracted into `mnt\`. It is not mounted.
- A directory is meant to be used where it sits, but that path is broken in
  this code and a directory layer never maps anything. See
  [Status and limits](#status-and-limits).

If a persistence directory was given, it is mapped last, over everything else.

`Destroy` undoes it. It unmounts whatever `mounts.conf` lists, deletes the
symlinks, and copies any real file it finds in the map root into the persistence
directory before removing the working root's contents. A real file in the map
root is one the app created or replaced while it ran, so that is what a save
looks like. The next `Create` with the same persistence directory puts those
files back on top.

After `Create`, the working root looks like this:

```text
<root>\
  map\              the composite tree, one folder per drive letter (map\C\..., map\D\...)
  mnt\              one folder per image or ZIP, named after its file without the extension
  mounts.conf       image;mountpoint per line, so the images can be unmounted later
  persistence.conf  the persistence directory, if one was given
```

If a process dies after `Create` returned but without calling `Destroy`, its
images stay attached and `mounts.conf` is still there. The next `Create` on that
root sees it and runs `Destroy` first. A process that dies, or a `Create` that
fails, partway through the map leaves no `mounts.conf`; see
[Status and limits](#status-and-limits).

## Map format

One command per line, fields separated by semicolons. A line that starts with
`#` is a comment.

```text
# MAP;image;virtual_path      layer an image or ZIP at a virtual path
MAP;example.zip;C:\
MAP;exiso.iso;D:\
MAP;windows.vhdx;C:\Windows

# LINK;source;virtual_path    put a single file at a virtual path
LINK;somefile_example.txt;C:\app\somefile.txt

# REMOVE;virtual_path         hide a file that an earlier layer brought in
REMOVE;C:\app\unwanted.dll
```

Image and source names are tried as given, then relative to the map file's own
directory, then relative to the directory in the `SGCROOT` environment variable
if it is set. That order decides which file is used. The type check that
follows looks at the name as written in the map, not at the path it resolved
to, which matters only for directories (see the directory-layer limit below).

A virtual path turns into a real one by dropping the colon and putting the rest
under `map\`: `C:\app\hello.exe` becomes `<root>\map\C\app\hello.exe`.

## API

Everything is static, in namespace `Smoothie`, class `Smoothie`.

```csharp
bool Create(string path_to_mapfile, string path_to_root, string path_to_persistence);
bool Destroy(string path_to_root);
bool Resolve(string path_to_root, string virtual_path, out string out_path);
```

Pass `""` as `path_to_persistence` to run without a read-write layer. Don't pass
`null`. Use absolute paths throughout.

```csharp
string root  = @"C:\work\tmp\example";    // working root
string saves = @"C:\work\save\example";   // or "" to throw writes away

if (!Smoothie.Smoothie.Create(@"C:\work\apps\example\example.map", root, saves))
    return;

Smoothie.Smoothie.Resolve(root, @"C:\app\hello.exe", out string exe);
// exe is C:\work\tmp\example\map\C\app\hello.exe

// ... start the app from there and wait for it ...

Smoothie.Smoothie.Destroy(root);
```

## Build

```powershell
dotnet build Smoothie.NET.csproj -c Release
```

The Release configuration writes `Smoothie.NET.dll` to `.\bin`. There are no
package dependencies.

The project targets `net5.0`, which went out of support in May 2022, so expect
to retarget it before building with a current SDK: change `<TargetFramework>` in
`Smoothie.NET.csproj`. Retargeted to net10.0 it compiles with no warnings;
that was a compile check only, and it has not been run on Windows since 2021.

To use it with VXTools.NET, build this first and copy `Smoothie.NET.dll` into a
`bin\` folder at the root of your VXTools.NET checkout (`VXShared` references
`..\bin\Smoothie.NET.dll`). VXShared, the VXTools.NET project that references
it, targets .NET 5.0 as well, so retarget the two together.

## Windows versions and architectures

Windows only. The work is done by `virtdisk.dll`, the volume mount point APIs
and `CreateSymbolicLink`, all through P/Invoke. Attaching an ISO or VHDX needs
Windows 8 or later. Whether VHD-only use works on Windows 7 was never tested;
the code opens every image with device type and vendor `UNKNOWN` (auto-detect),
and .NET 5 itself needs Windows 7 SP1. The source doesn't record which Windows
builds it was run on.

The project is AnyCPU and has no architecture-specific code, so it runs as x86
or x64, whichever the process that loads it is.

## Status and limits

This is the library as it stood when the write-up went out in 2021. The limits
below come from reading the code.

- **Elevation.** The process has to run as administrator. Symlinks are created
  without the unprivileged-create flag, so Developer Mode doesn't help, and
  setting a volume mount point needs it anyway. Every call also tries to enable
  `SeBackupPrivilege` on the process token.
- **Images outlive the process.** Images are attached with permanent lifetime
  and an Everyone-full-access security descriptor; they stay attached after the
  process exits until `Destroy` (or the next `Create` on the same root) detaches
  them. That needs `mounts.conf`, which is written only after the whole map has
  been processed. If `Create` fails partway (a missing image returns `false`
  without rolling back; a malformed `MAP` line or a directory layer throws after
  earlier images were attached), or the process dies during `Create`, the
  images already attached stay attached and nothing on disk records them.
  Detach them by hand, in Disk Management or with `Dismount-DiskImage`.
- **Directory layers don't work.** A `MAP` line naming a directory is meant to
  layer it in place, but `Create` always mirrors from `mnt\<name>`, which is
  only created for images and ZIPs. A directory whose name, as written, exists
  from the process's current directory (an absolute path, say) is detected as a
  directory, nothing is made under `mnt\`, and `Create` throws
  `DirectoryNotFoundException` when it goes to mirror it. A directory found only
  next to the map file or under `SGCROOT` fails the type check, which uses the
  name as written rather than the resolved path, so it is treated as an unknown
  type: it gets an empty `mnt\<name>` and maps nothing, with no message. The
  persistence directory is mapped directly and isn't affected.
- **Layer file names must differ.** Each image or ZIP gets
  `mnt\<file name without extension>`, so `base\data.zip` and
  `update\data.zip`, or `game.iso` and `game.zip`, share a folder. Usually the
  second mount fails (an image can't be mounted on a non-empty folder, a ZIP
  won't extract over existing files) and `Create` rolls back. Two ZIPs with no
  file paths in common extract into the same folder instead: the second `MAP`
  brings the first ZIP's files along, and `Destroy` throws when it tries to
  delete that folder a second time.
- **One instance per process.** The list of mounts is static and never cleared.
- **No copy-on-write.** A file in the map root is a symlink to the layer below.
  Writing through it hits that layer, or fails if the layer is a read-only
  image. Replacing the symlink with a real copy before a write is done from
  inside the app's process by [pdx](https://github.com/batteryshark/pdx), which
  is part three of the write-up. Smoothie only collects the real files
  afterwards.
- **ZIPs are extracted every time.** A large ZIP layer costs a full extract on
  each `Create` and a delete on each `Destroy`.
- **Unknown image types are not rejected.** A `MAP` line naming a file that
  isn't an ISO, VHD, VHDX or ZIP gets an empty mount directory and maps nothing.
- **`REMOVE` and `LINK` are file operations.** `REMOVE` doesn't remove a
  directory. `LINK` doesn't create missing parent directories or replace
  something already at that path. A `LINK` that fails, because its source
  can't be found, its parent directory is missing or something is already
  there, fails silently: the symlink call's result is never checked and
  `Create` still returns `true`.
- **Errors are thin.** The three calls return `bool` and print reasons to
  standard output, but they can also throw: a `MAP` or `LINK` line with only
  two fields throws `IndexOutOfRangeException`, and IO failures during mount
  rollback or cleanup are not caught. A line with fewer than two or more than
  three fields is skipped without a message.
- **`Resolve` is path arithmetic.** It doesn't check that the file exists.
- **No tests.** There is no test project or sample map in this repository.
  libsmoothie has an example map, and the format is the same.

## Credits

The P/Invoke declarations in `Virtlib.cs` follow `virtdisk.h` from the Windows
SDK.

Related pieces of VX:
[libsmoothie](https://github.com/batteryshark/libsmoothie) (the native original),
[VXTools.NET](https://github.com/batteryshark/VXTools.NET) (the managed launcher
that uses this),
[pdx](https://github.com/batteryshark/pdx) (the in-process redirection) and
[VXApp4Playnite](https://github.com/batteryshark/VXApp4Playnite) (the Playnite
extension, whose default settings list this repository's releases as a download
source). This repository has no release binaries; build the DLL yourself (see
[Build](#build)).

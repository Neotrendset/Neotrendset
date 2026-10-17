using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace PickPack.Disk.ETC
{
    public static class DiskUtil
    {    
        public static int GetSectorSize(SafeFileHandle diskHandle)
        {
            if(OperatingSystem.IsMacOS())return MacDisk.Sector(diskHandle);
            int size = Marshal.SizeOf<Win32API.DISK_GEOMETRY>();
            IntPtr buffer = Marshal.AllocHGlobal(size);

            try
            {
                if (Win32API.DeviceIoControl(diskHandle, Win32API.IOCTL_DISK_GET_DRIVE_GEOMETRY, IntPtr.Zero, 0, buffer, (uint)size, out _, IntPtr.Zero))
                {
                    Win32API.DISK_GEOMETRY geometry = Marshal.PtrToStructure<Win32API.DISK_GEOMETRY>(buffer);

                    return (int)geometry.BytesPerSector;
                }
                else
                {
                    return 512;
                }
            }
            finally
            {   
                Marshal.FreeHGlobal(buffer);
            }
        }

        public static long GetDiskLength(int physicalDriveNumber) => GetDiskLength(OperatingSystem.IsMacOS()?MacDisk.PathFor(physicalDriveNumber):$@"\\.\PhysicalDrive{physicalDriveNumber}");

        public static long GetDiskLength(string physicalDrivePath)
        {
            if(OperatingSystem.IsMacOS())return MacDisk.Length(physicalDrivePath);
            using var stream = new FileStream(physicalDrivePath, FileMode.Open, FileAccess.ReadWrite);            

            return stream.Length;
        }

        public static ulong GetAvailableFreeSpace(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            if(OperatingSystem.IsMacOS()){var full=Path.GetFullPath(path);return (ulong)DriveInfo.GetDrives().Where(d=>full.StartsWith(d.Name,StringComparison.Ordinal)&&d.IsReady).OrderByDescending(d=>d.Name.Length).First().AvailableFreeSpace;}
            bool success = Win32API.GetDiskFreeSpaceEx(path, out ulong freeBytesAvailable, out _, out _);

            if (!success)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "디스크 용량 확인에 실패했습니다.");

            return freeBytesAvailable;
        }

        private static System.Diagnostics.Process? sleepProcess;
        public static void AllowSleep()
        {
            if(sleepProcess is {} process){try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}process.Dispose();sleepProcess=null;}
            if(OperatingSystem.IsWindows())_ = Win32API.SetThreadExecutionState(Win32API.ES_CONTINUOUS);
        }
        public static void PreventSleep()
        {
            if(OperatingSystem.IsMacOS())
            {
                if(sleepProcess==null||sleepProcess.HasExited){var info=new System.Diagnostics.ProcessStartInfo("/usr/bin/caffeinate"){UseShellExecute=false};info.ArgumentList.Add("-i");info.ArgumentList.Add("-w");info.ArgumentList.Add(Environment.ProcessId.ToString());sleepProcess=System.Diagnostics.Process.Start(info);}return;
            }
            if(!OperatingSystem.IsWindows())return;
            _ = Win32API.SetThreadExecutionState(Win32API.ES_CONTINUOUS | Win32API.ES_SYSTEM_REQUIRED);
        }
    }
}
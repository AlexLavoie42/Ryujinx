using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ChocolArm64.Memory
{
    public static class MemoryAllocWindows
    {
        [Flags]
        private enum AllocationType : uint
        {
            Commit     = 0x1000,
            Reserve    = 0x2000,
            Decommit   = 0x4000,
            Release    = 0x8000,
            Reset      = 0x80000,
            Physical   = 0x400000,
            TopDown    = 0x100000,
            WriteWatch = 0x200000,
            LargePages = 0x20000000
        }

        [Flags]
        private enum MemoryProtection
        {
            Execute                  = 0x10,
            ExecuteRead              = 0x20,
            ExecuteReadWrite         = 0x40,
            ExecuteWriteCopy         = 0x80,
            NoAccess                 = 0x01,
            ReadOnly                 = 0x02,
            ReadWrite                = 0x04,
            WriteCopy                = 0x08,
            GuardModifierflag        = 0x100,
            NoCacheModifierflag      = 0x200,
            WriteCombineModifierflag = 0x400
        }

        private enum WriteWatchFlags : uint
        {
            None  = 0,
            Reset = 1
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr VirtualAlloc(
            IntPtr           lpAddress,
            IntPtr           dwSize,
            AllocationType   flAllocationType,
            MemoryProtection flProtect);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualFree(
            IntPtr         lpAddress,
            uint           dwSize,
            AllocationType dwFreeType);

        [DllImport("kernel32.dll")]
        private static extern int GetWriteWatch(
            WriteWatchFlags dwFlags,
            IntPtr          lpBaseAddress,
            IntPtr          dwRegionSize,
            IntPtr[]        lpAddresses,
            ref ulong       lpdwCount,
            out uint        lpdwGranularity);

        public static IntPtr Allocate(IntPtr size)
        {
            const AllocationType flags =
                AllocationType.Reserve |
                AllocationType.Commit;

            return VirtualAlloc(IntPtr.Zero, size, flags, MemoryProtection.ReadWrite);
        }

        public static IntPtr AllocateWriteTracked(IntPtr size)
        {
            const AllocationType flags =
                AllocationType.Reserve |
                AllocationType.Commit  |
                AllocationType.WriteWatch;

            return VirtualAlloc(IntPtr.Zero, size, flags, MemoryProtection.ReadWrite);
        }

        public static bool Free(IntPtr address)
        {
            return VirtualFree(address, 0, AllocationType.Release);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetModifiedPages(
            IntPtr    address,
            IntPtr    size,
            IntPtr[]  addresses,
            out ulong count)
        {
            ulong pagesCount = (ulong)addresses.Length;

            int result = GetWriteWatch(
                WriteWatchFlags.Reset,
                address,
                size,
                addresses,
                ref pagesCount,
                out uint granularity);

            count = pagesCount;

            return result == 0;
        }
    }
}
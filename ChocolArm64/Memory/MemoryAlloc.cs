using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ChocolArm64.Memory
{
    public static class MemoryAlloc
    {
        public static bool HasWriteWatchSupport => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static IntPtr Allocate(IntPtr size)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return MemoryAllocWindows.Allocate(size);
            }
            else
            {
                return Marshal.AllocHGlobal(size);
            }
        }

        public static IntPtr AllocateWriteTracked(IntPtr size)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return MemoryAllocWindows.AllocateWriteTracked(size);
            }
            else
            {
                return Marshal.AllocHGlobal(size);
            }
        }

        public static bool Free(IntPtr address)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return MemoryAllocWindows.Free(address);
            }
            else
            {
                Marshal.FreeHGlobal(address);

                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetModifiedPages(
            IntPtr    address,
            IntPtr    size,
            IntPtr[]  addresses,
            out ulong count)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return MemoryAllocWindows.GetModifiedPages(address, size, addresses, out count);
            }
            else
            {
                count = 0;

                return false;
            }
        }
    }
}
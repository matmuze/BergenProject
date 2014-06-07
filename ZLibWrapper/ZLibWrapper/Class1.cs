using System;
using System.Runtime.InteropServices;

namespace ZLibWrapper
{
    public class ZLibWrapper
    {
        [DllImport("zlib1")]
        public static extern int compress(IntPtr dest, IntPtr destSize, IntPtr source, uint sourceSize);

        public static int CompressBuffer(byte[] destBuffer, byte[] sourceBuffer, uint bufferSize)
        {
            GCHandle pinnedDestBuffer = GCHandle.Alloc(destBuffer, GCHandleType.Pinned);
            IntPtr destBufferPtr = pinnedDestBuffer.AddrOfPinnedObject();

            GCHandle pinnedSourceBuffer = GCHandle.Alloc(sourceBuffer, GCHandleType.Pinned);
            IntPtr sourceBufferPtr = pinnedSourceBuffer.AddrOfPinnedObject();

            IntPtr destBufferSizePtr = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(destBufferSizePtr, (int)bufferSize);

            int compressedSize = 0;
            int res = compress(destBufferPtr, destBufferSizePtr, sourceBufferPtr, bufferSize);

            if (res == 0)
            {
                compressedSize = Marshal.ReadInt32(destBufferSizePtr);
            }

            // Free buffers
            pinnedDestBuffer.Free();
            pinnedSourceBuffer.Free();
            Marshal.FreeHGlobal(destBufferSizePtr);

            return res + compressedSize;
        }
    }
}



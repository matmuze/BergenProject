using System;
using System.Runtime.InteropServices;

public class ZLibWrapper
{
	[DllImport("zlib1")]
	private static extern int compress(IntPtr dest, IntPtr destSize, IntPtr source, uint sourceSize);
	
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

	[DllImport("zlib1")]
	private static extern int uncompress(IntPtr dest, IntPtr destSize, IntPtr source, uint sourceSize);

	public static int UncompressBuffer(byte[] destBuffer, uint destBufferSize, byte[] sourceBuffer, uint sourceBufferSize)
	{
		GCHandle pinnedDestBuffer = GCHandle.Alloc(destBuffer, GCHandleType.Pinned);
		IntPtr destBufferPtr = pinnedDestBuffer.AddrOfPinnedObject();
		
		GCHandle pinnedSourceBuffer = GCHandle.Alloc(sourceBuffer, GCHandleType.Pinned);
		IntPtr sourceBufferPtr = pinnedSourceBuffer.AddrOfPinnedObject();
		
		IntPtr destBufferSizePtr = Marshal.AllocHGlobal(sizeof(int));
		Marshal.WriteInt32(destBufferSizePtr, (int)destBufferSize);
		
		int uncompressedSize = 0;
		int res = uncompress(destBufferPtr, destBufferSizePtr, sourceBufferPtr, sourceBufferSize);
		
		if (res == 0)
		{
			uncompressedSize = Marshal.ReadInt32(destBufferSizePtr);
		}
		
		// Free buffers
		pinnedDestBuffer.Free();
		pinnedSourceBuffer.Free();
		Marshal.FreeHGlobal(destBufferSizePtr);
		
		return res + uncompressedSize;
	}
}
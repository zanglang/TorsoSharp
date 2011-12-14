//-----------------------------------------------------------------------
// <copyright file="NativeMethods.cs" company="muvee Technologies Pte Ltd">
//   Copyright (c) muvee Technologies Pte Ltd. All rights reserved.
// </copyright>
// <author>Jerry Chong</author>
//-----------------------------------------------------------------------

namespace Torso
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// SafeHandle wrapper implementation for P/Invoke DLL handles
    /// </summary>
    internal sealed class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeLibraryHandle() : base(true) {}

        /// <summary>
        /// Override function to release the P/Invoke DLL handle
        /// </summary>
        /// <returns></returns>
        protected override bool ReleaseHandle()
        {
            return NativeMethods.FreeLibrary(handle);
        }
    }

    /// <summary>
    /// Collection of unmanaged code methods used by Torso
    /// </summary>
    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeLibraryHandle LoadLibrary(string fileName);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(SafeLibraryHandle hModule, string procedureName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FreeLibrary(IntPtr hModule);

        /*************************/

        [StructLayout(LayoutKind.Sequential)]
        internal struct MvExec
        {
            public IntPtr baseObject;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string config;
            public int bufSize;
            public int UTID;
            public int result;
            public int threadid;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAsAttribute(UnmanagedType.I1)]
        internal delegate bool Init();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void Shutdown();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int GetUTCount();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAsAttribute(UnmanagedType.I1)]
        internal delegate bool GetUTName(
            int pos,
            [MarshalAs(UnmanagedType.LPWStr)] string pNamebuf,
            ref int pBufSize);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int GetUTID(
            [MarshalAs(UnmanagedType.LPWStr)] string namebuf,
            int bufsize);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAsAttribute(UnmanagedType.I1)]
        internal delegate bool GenericCanExecute(IntPtr p, int pos);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int GenericExecute(
            IntPtr p,
            int pos,
            [MarshalAsAttribute(UnmanagedType.LPWStr)] string cfgNameBuf,
            int bufsize);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr ThreadedExecute(
            IntPtr p,
            int pos,
            [MarshalAsAttribute(UnmanagedType.LPWStr)] string cfgNameBuf,
            int bufsize);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAsAttribute(UnmanagedType.I1)]
        internal delegate bool GetThreadedExecuteResult(out int result);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr GetBaseObject();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void SubmitClass(
            [MarshalAs(UnmanagedType.LPWStr)] string clsnameBuf,
            int bufsize,
            IntPtr submittee,
            IntPtr pCore);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr GetClass(
            [MarshalAs(UnmanagedType.LPWStr)] string clsnameBuf,
            int bufsize,
            IntPtr pCore);
    }
}

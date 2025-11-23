using System;
using System.Runtime.InteropServices;

namespace ChillPatcher.Rime
{
    /// <summary>
    /// librime C API的P/Invoke绑定
    /// </summary>
    public static class RimeApi
    {
        private const string RimeDll = "rime.dll";

        public delegate void RimeNotificationHandler(
            IntPtr contextObject,
            ulong sessionId,
            [MarshalAs(UnmanagedType.LPStr)] string messageType,
            [MarshalAs(UnmanagedType.LPStr)] string messageValue);

        [StructLayout(LayoutKind.Sequential)]
        public struct RimeTraits
        {
            public int DataSize;
            [MarshalAs(UnmanagedType.LPStr)] public string SharedDataDir;
            [MarshalAs(UnmanagedType.LPStr)] public string UserDataDir;
            [MarshalAs(UnmanagedType.LPStr)] public string DistributionName;
            [MarshalAs(UnmanagedType.LPStr)] public string DistributionCodeName;
            [MarshalAs(UnmanagedType.LPStr)] public string DistributionVersion;
            [MarshalAs(UnmanagedType.LPStr)] public string AppName;
            public IntPtr Modules;
            public int MinLogLevel;
            [MarshalAs(UnmanagedType.LPStr)] public string LogDir;
            [MarshalAs(UnmanagedType.LPStr)] public string PrebuiltDataDir;
            [MarshalAs(UnmanagedType.LPStr)] public string StagingDir;

            public static RimeTraits Create()
            {
                var traits = new RimeTraits();
                traits.DataSize = Marshal.SizeOf<RimeTraits>() - sizeof(int);
                return traits;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RimeComposition
        {
            public int Length;
            public int CursorPos;
            public int SelStart;
            public int SelEnd;
            public IntPtr Preedit;  // char* - 手动 marshal
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RimeCandidate
        {
            [MarshalAs(UnmanagedType.LPStr)] public string Text;
            [MarshalAs(UnmanagedType.LPStr)] public string Comment;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RimeMenu
        {
            public int PageSize;
            public int PageNo;
            public int IsLastPage;  // Bool → int (librime 中 Bool 是 int)
            public int HighlightedCandidateIndex;
            public int NumCandidates;
            public IntPtr Candidates;
            [MarshalAs(UnmanagedType.LPStr)] public string SelectKeys;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RimeCommit
        {
            public int DataSize;
            public IntPtr Text;  // char* - 手动 marshal 避免自动释放

            public static RimeCommit Create()
            {
                var commit = new RimeCommit();
                commit.DataSize = Marshal.SizeOf<RimeCommit>() - sizeof(int);
                return commit;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RimeContext
        {
            public int DataSize;
            public RimeComposition Composition;
            public RimeMenu Menu;
            // v0.9.2+
            public IntPtr CommitTextPreview;  // char* - 改为 IntPtr
            public IntPtr SelectLabels;

            public static RimeContext Create()
            {
                var context = new RimeContext();
                context.DataSize = Marshal.SizeOf<RimeContext>() - sizeof(int);
                return context;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RimeStatus
        {
            public int DataSize;
            public IntPtr SchemaId;     // char*
            public IntPtr SchemaName;   // char*
            // librime 中 Bool 定义为 int (4字节), 不是 C# 的 bool (1字节)
            public int IsDisabled;      // Bool → int
            public int IsComposing;     // Bool → int
            public int IsAsciiMode;     // Bool → int
            public int IsFullShape;     // Bool → int
            public int IsSimplified;    // Bool → int
            public int IsTraditional;   // Bool → int
            public int IsAsciiPunct;    // Bool → int

            public static RimeStatus Create()
            {
                var status = new RimeStatus();
                status.DataSize = Marshal.SizeOf<RimeStatus>() - sizeof(int);
                return status;
            }
        }

        // API函数
        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void RimeSetup(ref RimeTraits traits);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void RimeInitialize(ref RimeTraits traits);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void RimeFinalize();

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RimeStartMaintenance([MarshalAs(UnmanagedType.I1)] bool fullCheck);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void RimeJoinMaintenanceThread();

        // Deployment functions
        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void RimeDeployerInitialize(ref RimeTraits traits);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RimeDeployWorkspace();

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong RimeCreateSession();

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RimeDestroySession(ulong sessionId);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RimeProcessKey(ulong sessionId, int keycode, int mask);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void RimeClearComposition(ulong sessionId);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RimeGetCommit(ulong sessionId, ref RimeCommit commit);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RimeFreeCommit(ref RimeCommit commit);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RimeGetContext(ulong sessionId, ref RimeContext context);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RimeFreeContext(ref RimeContext context);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void RimeSetOption(ulong sessionId, string option, [MarshalAs(UnmanagedType.I1)] bool value);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RimeGetOption(ulong sessionId, string option);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RimeGetStatus(ulong sessionId, ref RimeStatus status);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RimeFreeStatus(ref RimeStatus status);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RimeSelectCandidateOnCurrentPage(ulong sessionId, nuint index);

        [DllImport(RimeDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RimeSelectSchema(ulong sessionId, string schemaId);

        public static RimeCandidate[] GetCandidates(IntPtr candidatesPtr, int count)
        {
            if (candidatesPtr == IntPtr.Zero || count == 0)
                return Array.Empty<RimeCandidate>();

            var candidates = new RimeCandidate[count];
            var candidateSize = Marshal.SizeOf<RimeCandidate>();

            for (int i = 0; i < count; i++)
            {
                var ptr = IntPtr.Add(candidatesPtr, i * candidateSize);
                candidates[i] = Marshal.PtrToStructure<RimeCandidate>(ptr);
            }

            return candidates;
        }
    }
}

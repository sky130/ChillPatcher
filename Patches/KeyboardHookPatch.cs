using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using ChillPatcher.Rime;
using ChillPatcher.Patches.Rime;

namespace ChillPatcher.Patches
{
    /// <summary>
    /// 全局键盘钩子补丁 - 用于在壁纸引擎中捕获桌面键盘输入
    /// </summary>
    public class KeyboardHookPatch
    {
        private static IntPtr hookId = IntPtr.Zero;
        private static LowLevelKeyboardProc hookCallback;
        private static readonly Queue<char> inputQueue = new Queue<char>();
        private static readonly Queue<string> commitQueue = new Queue<string>();
        private static readonly Queue<uint> navigationKeyQueue = new Queue<uint>(); // 导航键队列(方向键/Delete等)
        private static readonly object queueLock = new object();
        private static Thread hookThread;
        private static bool isRunning = false;
        
        // Rime输入法引擎
        private static RimeEngine rimeEngine = null;
        private static bool useRime = false;
        private static bool _debugFirstKey = true;  // 调试标志
        
        // Windows API
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage([In] ref MSG lpmsg);

        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int nExitCode);
        
        private const uint PM_REMOVE = 0x0001;

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        // 常量
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <summary>
        /// 初始化键盘钩子
        /// </summary>
        public static void Initialize()
        {
            if (hookThread != null && hookThread.IsAlive)
            {
                Plugin.Logger.LogWarning("[KeyboardHook] 钩子线程已经在运行");
                return;
            }

            // 初始化Rime引擎
            useRime = PluginConfig.EnableRimeInputMethod.Value;
            if (useRime)
            {
                try
                {
                    InitializeRime();
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"[Rime] 初始化失败: {ex.GetType().Name}: {ex.Message}");
                    Plugin.Logger.LogError($"[Rime] 堆栈: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Plugin.Logger.LogError($"[Rime] 内部异常: {ex.InnerException.Message}");
                    }
                    Plugin.Logger.LogWarning("[Rime] 降级为简单输入模式");
                    useRime = false;
                }
            }
            else
            {
                Plugin.Logger.LogInfo("[KeyboardHook] 使用简单输入模式(未启用Rime)");
            }

            isRunning = true;
            hookThread = new Thread(HookThreadProc);
            hookThread.IsBackground = true;
            hookThread.Start();
            
            Plugin.Logger.LogInfo("[KeyboardHook] 钩子线程已启动");
        }

        /// <summary>
        /// 钩子线程过程
        /// </summary>
        private static void HookThreadProc()
        {
            try
            {
                hookCallback = HookCallback;
                using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    hookId = SetWindowsHookEx(WH_KEYBOARD_LL, hookCallback, GetModuleHandle(curModule.ModuleName), 0);
                }

                if (hookId == IntPtr.Zero)
                {
                    Plugin.Logger.LogError("[KeyboardHook] 钩子设置失败");
                    return;
                }

                Plugin.Logger.LogInfo("[KeyboardHook] 钩子设置成功，开始消息循环");

                // 非阻塞消息循环 - 使用 PeekMessage 替代 GetMessage
                MSG msg;
                while (isRunning)
                {
                    // 非阻塞地检查消息
                    if (PeekMessage(out msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                    {
                        if (msg.message == 0x0012) // WM_QUIT
                        {
                            Plugin.Logger.LogInfo("[KeyboardHook] 收到 WM_QUIT 消息");
                            break;
                        }
                        
                        TranslateMessage(ref msg);
                        DispatchMessage(ref msg);
                    }
                    else
                    {
                        // 没有消息时短暂休眠，避免 CPU 占用过高
                        Thread.Sleep(PluginConfig.KeyboardHookInterval.Value);
                    }
                }

                Plugin.Logger.LogInfo("[KeyboardHook] 消息循环退出");
            }
            catch (ThreadAbortException)
            {
                // Unity 退出时会中止后台线程，这是正常的
                Plugin.Logger.LogInfo("[KeyboardHook] 线程被中止（正常退出）");
                Thread.ResetAbort(); // 重置中止状态，防止异常传播
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[KeyboardHook] 线程异常: {ex}");
            }
            finally
            {
                if (hookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(hookId);
                    hookId = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// 清理键盘钩子
        /// </summary>
        public static void Cleanup()
        {
            isRunning = false;
            
            // 先卸载钩子
            if (hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }

            // 发送退出消息
            if (hookThread != null && hookThread.IsAlive)
            {
                PostQuitMessage(0);
                // 不等待线程，让它自然结束
            }

            // 释放Rime引擎
            if (rimeEngine != null)
            {
                rimeEngine.Dispose();
                rimeEngine = null;
            }

            Plugin.Logger.LogInfo("[KeyboardHook] 钩子已清理");
        }

        /// <summary>
        /// 检查当前前台窗口是否是桌面
        /// </summary>
        private static bool IsDesktopActive()
        {
            IntPtr hwnd = GetForegroundWindow();
            StringBuilder className = new StringBuilder(256);
            GetClassName(hwnd, className, 256);
            string classNameStr = className.ToString();

            // 桌面窗口类名：Progman, WorkerW, SysListView32
            return classNameStr == "Progman" || classNameStr == "WorkerW" || classNameStr == "SysListView32";
        }

        /// <summary>
        /// 键盘钩子回调
        /// </summary>
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                uint vkCode = hookStruct.vkCode;

                // F6: 重新部署Rime(热重载配置)
                if (vkCode == 0x75 && useRime && rimeEngine != null) // VK_F6 = 0x75
                {
                    Plugin.Logger.LogInfo("[Rime] 用户按下F6,开始重新部署...");
                    rimeEngine.Redeploy();
                    return (IntPtr)1; // 拦截 F6
                }

                // 移除F4特殊处理,让Rime根据配置处理(default.yaml中定义了F4为方案选单)
                // if (vkCode == 0x73 && useRime && rimeEngine != null) // VK_F4 = 0x73
                // {
                //     rimeEngine.ToggleAsciiMode();
                //     return (IntPtr)1; // 拦截 F4
                // }

                // Shift 键临时切换(按下Shift时切到英文,松开恢复中文)
                // 这个功能稍后实现,需要处理 WM_KEYUP

                // 检测是否在桌面
                bool isDesktop = IsDesktopActive();
                
                if (isDesktop)
                {
                    if (useRime && rimeEngine != null)
                    {
                        // 使用Rime处理输入
                        ProcessKeyWithRime(vkCode);
                        return (IntPtr)1; // Rime模式总是拦截
                    }
                    else
                    {
                        // 简单队列模式
                        bool shouldIntercept = ProcessKeySimple(vkCode);
                        if (shouldIntercept)
                        {
                            return (IntPtr)1; // 拦截已处理的按键
                        }
                        // 未处理的按键(如方向键)传递给Unity
                    }
                }
            }

            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// 使用Rime处理按键
        /// </summary>
        private static void ProcessKeyWithRime(uint vkCode)
        {
            try
            {
                // 使用 Weasel 风格的按键转换
                if (!KeyEventConverter.ConvertKeyEvent(vkCode, 0, out int keycode, out int mask))
                {
                    Plugin.Logger.LogWarning($"[Rime] 无法转换按键 vk={vkCode:X2}");
                    return;
                }

                // 首次按键时检查状态(只保留标志重置)
                if (_debugFirstKey)
                {
                    _debugFirstKey = false;
                }

                // 处理Rime按键(使用转换后的 keycode 和 mask)
                bool processed = rimeEngine.ProcessKey(keycode, mask);
                
                // 如果Rime处理了按键,检查是否有提交
                if (processed)
                {
                    string commit = rimeEngine.GetCommit();
                    if (!string.IsNullOrEmpty(commit))
                    {
                        lock (queueLock)
                        {
                            commitQueue.Enqueue(commit);
                            Plugin.Logger.LogInfo($"[Rime] 提交文本: {commit}");
                        }
                    }
                    // Rime处理了,不再传递给简单模式
                    return;
                }
                
                // Rime没有处理(processed=false),传递给简单队列
                ProcessKeySimple(vkCode);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[Rime] 处理按键失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 简单字符队列处理
        /// </summary>
        /// <returns>是否拦截该按键</returns>
        private static bool ProcessKeySimple(uint vkCode)
        {
            char? inputChar = null;
            
            // 处理特殊按键
            if (vkCode == 0x08) // Backspace
            {
                inputChar = '\b';
            }
            else if (vkCode == 0x0D) // Enter
            {
                inputChar = '\n';
            }
            else if (vkCode >= 0x20 && vkCode <= 0x7E) // 可打印字符范围
            {
                // 方向键/Delete: 加入导航键队列
                if (vkCode >= 0x25 && vkCode <= 0x28) // 方向键: Left, Up, Right, Down
                {
                    lock (queueLock)
                    {
                        navigationKeyQueue.Enqueue(vkCode);
                    }
                    return true; // 拦截,但不加入inputQueue
                }
                else if (vkCode == 0x2E) // Delete
                {
                    lock (queueLock)
                    {
                        navigationKeyQueue.Enqueue(vkCode);
                    }
                    return true; // 拦截,但不加入inputQueue
                }
                
                char ch = (char)vkCode;
                bool isShiftPressed = (GetAsyncKeyState(0x10) & 0x8000) != 0;
                
                if (ch >= 'A' && ch <= 'Z')
                {
                    if (!isShiftPressed)
                    {
                        ch = char.ToLower(ch);
                    }
                }
                
                inputChar = ch;
            }

            if (inputChar.HasValue)
            {
                lock (queueLock)
                {
                    inputQueue.Enqueue(inputChar.Value);
                }
                return true; // 已处理,拦截
            }
            
            return false; // 未处理,不拦截
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        /// <summary>
        /// 获取并清空输入队列
        /// </summary>
        public static string GetAndClearInputBuffer()
        {
            lock (queueLock)
            {
                if (inputQueue.Count == 0)
                    return string.Empty;

                StringBuilder result = new StringBuilder();
                while (inputQueue.Count > 0)
                {
                    result.Append(inputQueue.Dequeue());
                }
                return result.ToString();
            }
        }

        /// <summary>
        /// 初始化Rime引擎
        /// </summary>
        private static void InitializeRime()
        {
            Plugin.Logger.LogInfo("[Rime] 正在初始化引擎...");
            
            string sharedData = RimeConfigManager.GetSharedDataDirectory();
            string userData = RimeConfigManager.GetUserDataDirectory();
            
            Plugin.Logger.LogInfo(RimeConfigManager.GetConfigInfo());
            
            rimeEngine = new RimeEngine();
            rimeEngine.Initialize(sharedData, userData, "rime.chill");
            
            RimeConfigManager.CopyExampleConfig();
        }

        /// <summary>
        /// 获取Rime上下文（preedit和候选词）
        /// </summary>
        public static RimeContextInfo GetRimeContext()
        {
            if (!useRime || rimeEngine == null)
                return null;
            
            return rimeEngine.GetContext();
        }

        /// <summary>
        /// 获取Rime提交的文本
        /// </summary>
        public static string GetCommittedText()
        {
            lock (queueLock)
            {
                if (commitQueue.Count == 0)
                    return null;
                
                return commitQueue.Dequeue();
            }
        }

        /// <summary>
        /// 获取导航键(方向键/Delete)
        /// </summary>
        public static uint? GetNavigationKey()
        {
            lock (queueLock)
            {
                if (navigationKeyQueue.Count == 0)
                    return null;
                
                return navigationKeyQueue.Dequeue();
            }
        }

        /// <summary>
        /// 清空所有输入（Rime和队列）
        /// </summary>
        public static void ClearAll()
        {
            // 清空Rime待提交
            if (useRime && rimeEngine != null)
            {
                rimeEngine.ClearComposition();
            }
            
            // 清空队列
            lock (queueLock)
            {
                inputQueue.Clear();
                commitQueue.Clear();
            }
        }
    }

    /// <summary>
    /// Patch Update - 每帧检测鼠标点击，清空输入队列
    /// </summary>
    [HarmonyPatch(typeof(EventSystem), "Update")]
    public class EventSystem_Update_Patch
    {
        static void Prefix()
        {
            // 检测鼠标点击（左键按下）
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                // 清空所有输入
                KeyboardHookPatch.ClearAll();
            }
        }
    }

    /// <summary>
    /// Patch TMP_InputField 来注入键盘输入和显示Rime候选词
    /// </summary>
    [HarmonyPatch(typeof(TMP_InputField), "LateUpdate")]
    public class TMP_InputField_LateUpdate_Patch
    {
        private static System.Reflection.MethodInfo keyPressedMethod = null;
        
        // 使用字典为每个InputField实例维护独立状态
        private static Dictionary<int, PreeditState> preeditStates = new Dictionary<int, PreeditState>();

        class PreeditState
        {
            public string lastPreeditDisplay = "";
            public string savedBaseText = "";
            public string savedTextAfterCaret = ""; // 光标后的文本
            public int savedCaretPosition = 0; // 进入preedit时的光标位置
            public bool inPreeditMode = false;
        }

        static void Postfix(TMP_InputField __instance)
        {
            int instanceId = __instance.GetInstanceID();
            
            // 只在输入框激活且获得焦点时注入
            if (!__instance.isFocused)
            {
                // 失焦时清理:preedit状态、Rime composition、所有队列
                if (preeditStates.ContainsKey(instanceId))
                {
                    preeditStates.Remove(instanceId);
                    
                    // 清理Rime composition和队列
                    KeyboardHookPatch.ClearAll();
                    
                    Plugin.Logger.LogInfo($"[InputField #{instanceId}] 失焦,已清理所有状态和队列");
                }
                return;
            }

            // 获取或创建该InputField的preedit状态
            if (!preeditStates.TryGetValue(instanceId, out var state))
            {
                state = new PreeditState();
                preeditStates[instanceId] = state;
            }

            // 1. 检查Rime的preedit状态
            var rimeContext = KeyboardHookPatch.GetRimeContext();
            
            if (rimeContext != null && !string.IsNullOrEmpty(rimeContext.Preedit))
            {
                // 进入preedit模式时保存基础文本和光标位置
                if (!state.inPreeditMode)
                {
                    int caret = __instance.caretPosition;
                    state.savedCaretPosition = caret;
                    state.savedBaseText = __instance.text.Substring(0, caret); // 光标前的文本
                    state.savedTextAfterCaret = caret < __instance.text.Length ? __instance.text.Substring(caret) : ""; // 光标后的文本
                    state.inPreeditMode = true;
                    Plugin.Logger.LogInfo($"[Preedit #{instanceId}] 进入模式, caret={caret}, before='{state.savedBaseText}', after='{state.savedTextAfterCaret}'");
                }

                // 生成新的preedit显示
                string currentPreeditDisplay = rimeContext.GetPreeditWithCandidates();
                
                // 只在preedit显示内容变化时更新文本
                if (currentPreeditDisplay != state.lastPreeditDisplay)
                {
                    Plugin.Logger.LogInfo($"[Preedit #{instanceId}] 更新显示: '{state.lastPreeditDisplay}' → '{currentPreeditDisplay}'");
                    state.lastPreeditDisplay = currentPreeditDisplay;
                    // 文本 = 光标前 + preedit + 光标后
                    __instance.text = state.savedBaseText + currentPreeditDisplay + state.savedTextAfterCaret;
                    __instance.ForceLabelUpdate(); // 立即更新文本显示
                }
                
                // 始终更新光标位置(即使preedit文本未变,光标可能循环移动)
                int targetCaret = state.savedBaseText.Length + rimeContext.CursorPos;
                __instance.caretPosition = targetCaret;
                __instance.stringPosition = targetCaret;
                __instance.selectionAnchorPosition = targetCaret;
                __instance.selectionFocusPosition = targetCaret;
                __instance.ForceLabelUpdate(); // 立即更新光标显示,消除闪烁
                
                return; // preedit显示中，不处理提交
            }
            else
            {
                // 退出preedit模式,恢复基础文本
                if (state.inPreeditMode)
                {
                    __instance.text = state.savedBaseText + state.savedTextAfterCaret;
                    __instance.caretPosition = state.savedCaretPosition;
                    state.lastPreeditDisplay = "";
                    state.savedBaseText = "";
                    state.savedTextAfterCaret = "";
                    state.inPreeditMode = false;
                }
            }

            // 2. 获取已提交的文本
            string commit = KeyboardHookPatch.GetCommittedText();
            if (!string.IsNullOrEmpty(commit))
            {
                int currentCaret = __instance.caretPosition;
                Plugin.Logger.LogInfo($"[InputField #{instanceId}] 提交前: text='{__instance.text}', caret={currentCaret}");
                
                // 插入到光标位置
                __instance.text = __instance.text.Insert(currentCaret, commit);
                __instance.ForceLabelUpdate(); // 先强制更新文本
                
                // 光标移动到插入文本之后
                int targetCaret = currentCaret + commit.Length;
                __instance.caretPosition = targetCaret;
                __instance.stringPosition = targetCaret; // TMP特有属性
                __instance.selectionAnchorPosition = targetCaret;
                __instance.selectionFocusPosition = targetCaret;
                
                Plugin.Logger.LogInfo($"[InputField #{instanceId}] 提交后: text='{__instance.text}', caret={__instance.caretPosition}, target={targetCaret}, commit='{commit}'");
                return;
            }

            // 3. 简单队列模式的兼容处理
            string simpleInput = KeyboardHookPatch.GetAndClearInputBuffer();
            if (!string.IsNullOrEmpty(simpleInput))
            {
                // 获取KeyPressed方法（只获取一次）
                if (keyPressedMethod == null)
                {
                    keyPressedMethod = typeof(TMP_InputField).GetMethod("KeyPressed",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }

                if (keyPressedMethod == null)
                {
                    Plugin.Logger.LogError("[桌面输入] 无法获取KeyPressed方法");
                    return;
                }

                // 调用KeyPressed处理每个字符
                foreach (char c in simpleInput)
                {
                    UnityEngine.Event evt = new UnityEngine.Event();
                    evt.type = UnityEngine.EventType.KeyDown;

                    if (c == '\b')
                    {
                        evt.keyCode = KeyCode.Backspace;
                        evt.character = '\0';
                    }
                    else if (c == '\n')
                    {
                        evt.keyCode = KeyCode.Return;
                        evt.character = '\n';
                    }
                    else
                    {
                        evt.keyCode = KeyCode.None;
                        evt.character = c;
                    }

                    keyPressedMethod.Invoke(__instance, new object[] { evt });
                }

                __instance.ForceLabelUpdate();
            }

            // 4. 处理导航键(方向键/Delete)
            uint? navKey = KeyboardHookPatch.GetNavigationKey();
            if (navKey.HasValue)
            {
                int currentCaret = __instance.caretPosition;
                int textLength = __instance.text.Length;

                switch (navKey.Value)
                {
                    case 0x25: // Left
                        if (currentCaret > 0)
                        {
                            __instance.caretPosition = currentCaret - 1;
                            __instance.stringPosition = currentCaret - 1;
                            __instance.selectionAnchorPosition = currentCaret - 1;
                            __instance.selectionFocusPosition = currentCaret - 1;
                            __instance.ForceLabelUpdate(); // 强制更新,避免闪烁延迟
                        }
                        break;

                    case 0x27: // Right
                        if (currentCaret < textLength)
                        {
                            __instance.caretPosition = currentCaret + 1;
                            __instance.stringPosition = currentCaret + 1;
                            __instance.selectionAnchorPosition = currentCaret + 1;
                            __instance.selectionFocusPosition = currentCaret + 1;
                            __instance.ForceLabelUpdate(); // 强制更新,避免闪烁延迟
                        }
                        break;

                    case 0x26: // Up
                        MoveCaretVertically(__instance, -1);
                        break;

                    case 0x28: // Down
                        MoveCaretVertically(__instance, 1);
                        break;

                    case 0x2E: // Delete
                        if (currentCaret < textLength)
                        {
                            __instance.text = __instance.text.Remove(currentCaret, 1);
                            __instance.ForceLabelUpdate();
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 垂直移动光标(上下键)
        /// </summary>
        private static void MoveCaretVertically(TMP_InputField inputField, int direction)
        {
            // 确保textInfo已更新
            inputField.ForceLabelUpdate();
            var textInfo = inputField.textComponent.textInfo;
            
            if (textInfo == null || textInfo.lineCount == 0)
                return;

            int currentCaret = inputField.caretPosition;
            
            // 查找当前光标所在行
            int currentLine = -1;
            int charIndexInLine = 0;
            for (int i = 0; i < textInfo.lineCount; i++)
            {
                int lineStart = textInfo.lineInfo[i].firstCharacterIndex;
                int lineEnd = textInfo.lineInfo[i].lastCharacterIndex;
                
                if (currentCaret >= lineStart && currentCaret <= lineEnd + 1)
                {
                    currentLine = i;
                    charIndexInLine = currentCaret - lineStart;
                    break;
                }
            }

            if (currentLine == -1)
                return;

            // 计算目标行
            int targetLine = currentLine + direction;
            if (targetLine < 0 || targetLine >= textInfo.lineCount)
                return; // 超出范围

            // 获取目标行信息
            int targetLineStart = textInfo.lineInfo[targetLine].firstCharacterIndex;
            int targetLineEnd = textInfo.lineInfo[targetLine].lastCharacterIndex;
            int targetLineLength = targetLineEnd - targetLineStart + 1;

            // 尝试保持相同的列位置,如果目标行更短,则移到行尾
            int targetCaret = targetLineStart + Math.Min(charIndexInLine, targetLineLength);

            // 更新光标
            inputField.caretPosition = targetCaret;
            inputField.stringPosition = targetCaret;
            inputField.selectionAnchorPosition = targetCaret;
            inputField.selectionFocusPosition = targetCaret;
        }
    }
}

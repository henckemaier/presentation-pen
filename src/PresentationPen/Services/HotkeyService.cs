using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Interop;
using PresentationPen.Interop;

namespace PresentationPen.Services
{
    /// <summary>Atalhos globais via RegisterHotKey em uma janela message-only.</summary>
    public sealed class HotkeyService : IDisposable
    {
        private readonly HwndSource _source;
        private readonly Dictionary<int, Action> _actions = new();
        private int _nextId = 1;

        public HotkeyService()
        {
            _source = new HwndSource(new HwndSourceParameters("PresentationPenHotkeys")
            {
                WindowStyle = 0,
                ParentWindow = NativeMethods.HWND_MESSAGE,
            });
            _source.AddHook(WndProc);
        }

        public bool Register(string gestureText, Action action)
        {
            if (!TryParse(gestureText, out uint mods, out uint vk)) return false;

            int id = _nextId++;
            if (!NativeMethods.RegisterHotKey(_source.Handle, id, mods | NativeMethods.MOD_NOREPEAT, vk))
                return false;

            _actions[id] = action;
            return true;
        }

        public void UnregisterAll()
        {
            foreach (int id in _actions.Keys)
                NativeMethods.UnregisterHotKey(_source.Handle, id);
            _actions.Clear();
        }

        public static bool TryParse(string text, out uint modifiers, out uint virtualKey)
        {
            modifiers = 0;
            virtualKey = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            try
            {
                if (new KeyGestureConverter().ConvertFromInvariantString(text.Trim()) is not KeyGesture gesture)
                    return false;
                // ModifierKeys usa os mesmos valores de MOD_ALT/MOD_CONTROL/MOD_SHIFT/MOD_WIN.
                modifiers = (uint)gesture.Modifiers;
                virtualKey = (uint)KeyInterop.VirtualKeyFromKey(gesture.Key);
                return virtualKey != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
            {
                handled = true;
                action();
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            UnregisterAll();
            _source.RemoveHook(WndProc);
            _source.Dispose();
        }
    }
}

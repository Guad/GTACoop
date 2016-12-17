using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using NativeUI;
using Control = GTA.Control;
using Font = GTA.Font;

namespace GTAServer
{
    public class Chat
    {
        public event EventHandler OnComplete;

        public Chat()
        {
            CurrentInput = "";
            _mainScaleform = new Scaleform(0);
            _mainScaleform.Load("multiplayer_chat");
        }

        public bool HasInitialized;

        public void Init()
        {
            _mainScaleform.CallFunction("SET_FOCUS", 2, 2, "ALL");
            _mainScaleform.CallFunction("SET_FOCUS", 1, 2, "ALL");
            HasInitialized = true;
        }

        public bool IsFocused
        {
            get { return _isFocused; }
            set
            {
                if (value && !_isFocused)
                {
                    _mainScaleform.CallFunction("SET_FOCUS", 2, 2, "ALL");
                }
                else if (!value && _isFocused)
                {
                    _mainScaleform.CallFunction("SET_FOCUS", 1, 2, "ALL");
                }

                _isFocused = value;
                
            }
        }

        private Scaleform _mainScaleform;

        public string CurrentInput;

        private int _switch = 1;
        private Keys _lastKey;
        private bool _isFocused;

        public void Tick()
        {
            if (!Main.IsOnServer()) return;

            _mainScaleform.Render2D();

            
            if (!IsFocused) return;
            Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);
        }

        public void AddMessage(string sender, string msg)
        {
            if (string.IsNullOrEmpty(sender))
                _mainScaleform.CallFunction("ADD_MESSAGE", "", SanitizeString(msg));
            else
                _mainScaleform.CallFunction("ADD_MESSAGE", SanitizeString(sender) + ":", SanitizeString(msg));
        }

        public string SanitizeString(string input)
        {
            input = Regex.Replace(input, "~.~", "", RegexOptions.IgnoreCase);
            return input;
        }
        
        public void OnKeyDown(Keys key)
        {
            if (key == Keys.PageUp && Main.IsOnServer())
                _mainScaleform.CallFunction("PAGE_UP");

            else if (key == Keys.PageDown && Main.IsOnServer())
                _mainScaleform.CallFunction("PAGE_DOWN");

            if (!IsFocused) return;

            if ((key == Keys.ShiftKey && _lastKey == Keys.Menu) || (key == Keys.Menu && _lastKey == Keys.ShiftKey))
                ActivateKeyboardLayout(1, 0);

            _lastKey = key;

            if (key == Keys.Escape)
            {
                IsFocused = false;
                CurrentInput = "";
            }

            var keyChar = GetCharFromKey(key, Game.IsKeyPressed(Keys.ShiftKey), false);

            if (keyChar.Length == 0) return;

            if (keyChar[0] == (char)8)
            {
                _mainScaleform.CallFunction("SET_FOCUS", 1, 2, "ALL");
                _mainScaleform.CallFunction("SET_FOCUS", 2, 2, "ALL");

                if (CurrentInput.Length > 0)
                {
                    CurrentInput = CurrentInput.Substring(0, CurrentInput.Length - 1);
                    _mainScaleform.CallFunction("ADD_TEXT", CurrentInput);
                }
                return;
            }
            if (keyChar[0] == (char)13)
            {
                _mainScaleform.CallFunction("ADD_TEXT", "ENTER");
                if (OnComplete != null) OnComplete.Invoke(this, EventArgs.Empty);
                CurrentInput = "";
                return;
            }
            var str = keyChar;

            CurrentInput += str;
            _mainScaleform.CallFunction("ADD_TEXT", str);
        }


        [DllImport("user32.dll")]
        public static extern int ToUnicodeEx(uint virtualKeyCode, uint scanCode,
        byte[] keyboardState,
        [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)]
        StringBuilder receivingBuffer,
        int bufferSize, uint flags, IntPtr kblayout);

        [DllImport("user32.dll")]
        public static extern int ActivateKeyboardLayout(int hkl, uint flags);

        public static string GetCharFromKey(Keys key, bool shift, bool altGr)
        {
            var buf = new StringBuilder(256);
            var keyboardState = new byte[256];
            if (shift)
                keyboardState[(int)Keys.ShiftKey] = 0xff;
            if (altGr)
            {
                keyboardState[(int)Keys.ControlKey] = 0xff;
                keyboardState[(int)Keys.Menu] = 0xff;
            }

            ToUnicodeEx((uint)key, 0, keyboardState, buf, 256, 0, InputLanguage.CurrentInputLanguage.Handle);
            return buf.ToString();
        }
    }
}
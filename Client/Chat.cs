using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using NativeUI;
using Control = GTA.Control;
using Font = GTA.Font;

namespace GTACoOp
{
    public class Chat
    {
        public event EventHandler OnComplete;

        public Chat()
        {
            CurrentInput = "";
        }

        public bool IsFocused { get; set; }


        public string CurrentInput;

        private int _switch = 1;
        public void Tick()
        {
            if (!IsFocused) return;

            Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);

            var res = UIMenu.GetScreenResolutionMantainRatio();

            new UIResRectangle(new Point(0, 0), new Size((int)res.Width, 40), Color.FromArgb(160, 0, 0, 0)).Draw();
            new UIResText(CurrentInput + (_switch > 15 ? "|" : ""), new Point(5, 5), 0.35f, Color.WhiteSmoke,
                Font.ChaletLondon, UIResText.Alignment.Left)
            {
                Outline = true,
            }.Draw();
            _switch++;
            if (_switch >= 30) _switch = 0;
        }


        public void OnKeyDown(Keys key)
        {
            if (!IsFocused) return;
            if (key == Keys.Escape)
                IsFocused = false;

            var keyChar = GetCharFromKey(key, Game.IsKeyPressed(Keys.ShiftKey), false);

            if (keyChar.Length == 0) return;

            if (keyChar[0] == (char)8)
            {
                if (CurrentInput.Length > 0)
                    CurrentInput = CurrentInput.Substring(0, CurrentInput.Length - 1);
                return;
            }
            if (keyChar[0] == (char)13)
            {
                if (OnComplete != null) OnComplete.Invoke(this, EventArgs.Empty);
                CurrentInput = "";
                return;
            }
            var str = keyChar;

            CurrentInput += str;
        }


        [DllImport("user32.dll")]
        public static extern int ToUnicode(uint virtualKeyCode, uint scanCode,
        byte[] keyboardState,
        [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)]
        StringBuilder receivingBuffer,
        int bufferSize, uint flags);

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
            ToUnicode((uint)key, 0, keyboardState, buf, 256, 0);
            return buf.ToString();
        }
    }
}
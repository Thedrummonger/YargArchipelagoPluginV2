using Archipelago.MultiClient.Net.MessageLog.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Yaml_Creator
{
    public static class RichTextBoxExtensions
    {
        const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public static void BeginUpdate(this RichTextBox rtb) =>
            SendMessage(rtb.Handle, WM_SETREDRAW, (IntPtr)0, IntPtr.Zero);

        public static void EndUpdate(this RichTextBox rtb)
        {
            SendMessage(rtb.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            rtb.Invalidate();
        }

        public static void AppendMessages(this RichTextBox rtb, params object[] messages)
        {
            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionLength = 0;

            foreach (var message in messages)
            {
                ColoredString coloredString = message is ColoredString cs ? cs : ColoredString.FromObject(message);
                foreach (var part in coloredString.Parts)
                {
                    rtb.SelectionColor = Color.FromArgb(part.Color.R, part.Color.G, part.Color.B);
                    rtb.AppendText(part.Text);
                }
                rtb.AppendText(Environment.NewLine);
            }
            rtb.SelectionColor = rtb.ForeColor;
        }

        public static bool IsScrolledToBottom(this RichTextBox rtb)
        {
            int lastCharIndex = rtb.TextLength;
            Point lastCharPos = rtb.GetPositionFromCharIndex(lastCharIndex);

            return lastCharPos.Y <= rtb.ClientSize.Height;
        }
    }

    public class ColoredString
    {
        public ColoredString() { }
        public ColoredString(string text, Color? color)
        {
            Parts.Add(new ColoredStringPart(text, color));
        }
        public List<ColoredStringPart> Parts = new List<ColoredStringPart>();
        public static ColoredString FromObject(object obj)
        {
            if (obj is LogMessage logMessage)
                return FromAPMessage(logMessage);
            return FromText(obj.ToString());
        }
        private static ColoredString FromAPMessage(LogMessage logMessage)
        {
            var result = new ColoredString();
            result.Parts = logMessage.Parts.Select(x => new ColoredStringPart(x.Text, Color.FromArgb(x.Color.R, x.Color.G, x.Color.B))).ToList();
            return result;
        }
        private static ColoredString FromText(string Message)
        {
            var result = new ColoredString();
            result.Parts.Add(new ColoredStringPart(Message));
            return result;
        }
        public ColoredString AddPart(string text, Color? color = null, bool WithSpace = false)
        {
            string Text = Parts.Count > 0 && WithSpace ? $" {text}" : text;
            Parts.Add(new ColoredStringPart(Text, color));
            return this;
        }
    }

    public class ColoredStringPart
    {
        public Color Color { get; }
        public string Text { get; }
        public ColoredStringPart(string text, Color? color = null)
        {
            Text = text;
            Color = color ?? Color.White;
        }
        public ColoredStringPart(string text, Archipelago.MultiClient.Net.Models.Color color)
        {
            Text = text;
            Color = Color.FromArgb(color.R, color.G, color.B);
        }
    }
}

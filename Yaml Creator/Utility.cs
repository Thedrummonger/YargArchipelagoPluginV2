using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using YargArchipelagoCommon;
using YargArchipelagoPlugin;
using static YargArchipelagoCommon.CommonData;

namespace Yaml_Creator
{
    public static class Utility
    {
        public class DisplayItem<T>
        {
            public T Value { get; set; }
            public string Display { get; set; }

            public override string ToString()
            {
                return Display;
            }
        }

        // Helper method for enums using GetDescription
        public static List<DisplayItem<T>> GetEnumDataSource<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .Select(e => new DisplayItem<T>
                {
                    Value = e,
                    Display = e.GetDescription()
                })
                .ToList();
        }

        // Generic helper method for any collection with custom display selector
        public static List<DisplayItem<T>> GetDataSource<T>(IEnumerable<T> items, Func<T, string> displaySelector)
        {
            return items.Select(item => new DisplayItem<T>
            {
                Value = item,
                Display = displaySelector(item)
            })
            .ToList();
        }

        public static YAMLSongPool NewSongPool(SupportedInstrument i, int a = 0, int min = 3, int max = 5)
        {
            return new YAMLSongPool
            {
                instrument = i,
                amount_in_pool = a,
                random_variance = 0,
                max_difficulty = max,
                min_difficulty = min,
                max_time = 3600,
                min_time = 0,
                completion_requirements = new CompletionRequirements()
                {
                    reward1_diff = SupportedDifficulty.Expert,
                    reward2_diff = SupportedDifficulty.Expert,
                    reward1_req = CompletionReq.Clear,
                    reward2_req = CompletionReq.ThreeStar
                }
            };
        }

        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }
        public static void ShowTextDialog(this Control parent, string title, IEnumerable<string> inputLines)
        {
            using (var f = new Form())
            using (var tb = new TextBox())
            {
                f.Text = title;
                f.StartPosition = FormStartPosition.CenterParent;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.ShowInTaskbar = false;
                f.Padding = Padding.Empty;

                tb.Multiline = true;
                tb.ReadOnly = true;
                tb.WordWrap = false;
                tb.ScrollBars = ScrollBars.Both;
                tb.BorderStyle = BorderStyle.None;
                tb.Font = new Font("Consolas", 10f);
                tb.Dock = DockStyle.Fill;

                var lines = inputLines?.ToList() ?? new List<string>();

                string longest = "";
                foreach (var line in lines)
                    if (line != null && line.Length > longest.Length)
                        longest = line;

                lines = lines.Select(line => line ?? new string('=', longest.Length)).ToList();

                string finalText = string.Join(Environment.NewLine, lines);

                var textSize = TextRenderer.MeasureText(longest, tb.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);

                int lineHeight = tb.Font.Height;
                int textWidth = textSize.Width - 20;
                int textHeight = (lineHeight * lines.Count());

                if (textWidth > parent.Width)
                    textWidth = parent.Width;
                if (textHeight > parent.Height)
                    textHeight = parent.Height;

                tb.Text = finalText;
                var parentSize = parent.ClientSize;
                f.ClientSize = new Size(textWidth, textHeight);

                f.Shown += (s, e) => { f.BeginInvoke(new Action(() => tb.DeselectAll())); };

                f.Controls.Add(tb);
                f.ShowDialog(parent);
            }
        }



    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Yaml_Creator.Utility;

namespace Yaml_Creator
{
    public partial class ValueSelectForm : Form
    {
        public object SelectedValue { get; private set; }
        private ListBox AltDisplay;

        public ValueSelectForm(string Title, bool selectable = true)
        {
            InitializeComponent(); 
            btnConfirm.DialogResult = DialogResult.OK;
            btnCancel.DialogResult = DialogResult.Cancel;
            lblTitle.Text = Title;

            if (!selectable)
            {
                AltDisplay = new ListBox
                {
                    Dock = DockStyle.Fill,
                    Name = "lbDisplay"
                };
                tableLayoutPanel1.Controls.Remove(btnCancel);
                tableLayoutPanel1.Controls.Remove(clbDisplay);
                tableLayoutPanel1.Controls.Add(AltDisplay, 0, 1);
                tableLayoutPanel1.SetColumnSpan(AltDisplay, 2);
                tableLayoutPanel1.SetColumnSpan(btnConfirm, 2);
                btnConfirm.Text = "Close";
            }

        }

        private bool SetDisplayAlt<T>(IEnumerable<T> items)
        {
            if (AltDisplay == null)
                return false;
            AltDisplay.Items.Clear();
            foreach (var item in items)
                AltDisplay.Items.Add(item);
            return true;
        }

        public void SetItems<T>(IEnumerable<DisplayItem<T>> items, IEnumerable<T> preSelected = null)
        {
            if (SetDisplayAlt(items)) 
                return;

            clbDisplay.Items.Clear();

            var preSelectedSet = preSelected != null ? new HashSet<T>(preSelected) : new HashSet<T>();
            foreach (var item in items)
            {
                bool isChecked = preSelectedSet.Contains(item.Value);
                clbDisplay.Items.Add(item, isChecked);
            }
        }
        public void SetItems<T>(IEnumerable<T> items, Func<T, string> displaySelector, IEnumerable<T> preSelected = null)
        {
            if (SetDisplayAlt(items))
                return;

            clbDisplay.Items.Clear();

            var preSelectedSet = preSelected != null ? new HashSet<T>(preSelected) : new HashSet<T>();
            foreach (var item in items)
            {
                var displayItem = new DisplayItem<T> { Value = item, Display = displaySelector(item) };
                bool isChecked = preSelectedSet.Contains(item);
                clbDisplay.Items.Add(displayItem, isChecked);
            }
        }

        public List<T> GetSelectedValues<T>()
        {
            if (AltDisplay != null)
                throw new Exception("Form was set to display only mode");

            var selected = new List<T>();
            foreach (DisplayItem<T> item in clbDisplay.CheckedItems)
            {
                selected.Add(item.Value);
            }
            return selected;
        }
    }
}

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

        public ValueSelectForm(string Title)
        {
            InitializeComponent(); 
            btnConfirm.DialogResult = DialogResult.OK;
            btnCancel.DialogResult = DialogResult.Cancel;
            lblTitle.Text = Title;
        }

        public void SetItems<T>(IEnumerable<DisplayItem<T>> items, IEnumerable<T> preSelected = null)
        {
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
            var selected = new List<T>();
            foreach (DisplayItem<T> item in clbDisplay.CheckedItems)
            {
                selected.Add(item.Value);
            }
            return selected;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Utilz
{
    /// <summary>
    /// A ListView with added tolerance:
    /// if the SelectedIndex is set when Items is still null or empty or not fully populated,
    /// this control will remember it and enforce it as soon as the Items are loaded.
    /// Bind to SelectedIndexTolerant instead of SelectedIndex.
    /// </summary>
    public class TolerantListView : ListView
    {
        public TolerantListView() : base() { }

        public int SelectedIndexTolerant
        {
            // get { return (int)GetValue(SelectedIndexTolerantProperty); }
            get { return (int)GetValue(SelectedIndexProperty); }
            set { SetValue(SelectedIndexTolerantProperty, value); }
        }
        public static readonly DependencyProperty SelectedIndexTolerantProperty =
            DependencyProperty.Register("SelectedIndexTolerant", typeof(int), typeof(TolerantListView), new PropertyMetadata(-1, OnSelectedIndexTolerantChanged));
        private static void OnSelectedIndexTolerantChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            TolerantListView instance = obj as TolerantListView;
            int newValue = (int)args.NewValue;
            if (instance != null)
            {
                instance._selectedIndexDesired = newValue;
                if (instance.Items != null && instance.Items.Count > newValue)
                {
                    instance.SelectedIndex = newValue;
                }
                else { }
            }
        }

        private int _selectedIndexDesired = -1;
        /// <summary>
        /// Fires when the collection is replaced or receives a new item or loses an item
        /// </summary>
        /// <param name="e"></param>
        protected override void OnItemsChanged(object e)
        {
            UpdateSelectedIndex();
            base.OnItemsChanged(e);
        }

        private void UpdateSelectedIndex()
        {
            if (_selectedIndexDesired != SelectedIndex && Items != null && Items.Count > _selectedIndexDesired)
            {
                IAsyncAction upd = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
                {
                    SelectedIndexTolerant = _selectedIndexDesired;
                });
            }
        }
    }
}

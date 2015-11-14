using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UniFiler10.Converters;
using UniFiler10.Data.Model;
using UniFiler10.Data.Metadata;
using UniFiler10.ViewModels;
using Utilz;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Animation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
    public sealed partial class FolderView : UserControl
    {
        public BinderVM VM
        {
            get { return (BinderVM)GetValue(VMProperty); }
            set { SetValue(VMProperty, value); }
        }
        public static readonly DependencyProperty VMProperty =
            DependencyProperty.Register("VM", typeof(BinderVM), typeof(FolderView), new PropertyMetadata(null));

        public FolderView()
        {
            InitializeComponent();
        }

        //private void OnDynamicFieldValueSelectComboBox_Opened(object sender, object e)
        //{
        //    var cb = sender as ComboBox;
        //    if (cb != null && cb.DataContext is DynamicField)
        //    {
        //        cb.ItemsSource = (cb.DataContext as DynamicField).FieldDescription.PossibleValues;
        //    }
        //}

        private void OnDynamicFieldValueSelectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = sender as ComboBox;
            if (cb != null && cb.DataContext is DynamicField && cb.SelectedItem is FieldValue)
            {
                (cb.DataContext as DynamicField).FieldValueId = (cb.SelectedItem as FieldValue).Id;
            }
        }

        private void OnVaalue_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (!textBox.IsReadOnly)
            {
                VM?.ChangeFieldValue(textBox?.DataContext as DynamicField, textBox?.Text);
            }
        }

        //private void DFGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
        //{
        //    ((Storyboard)((sender as Grid).Resources["EvidenceGrid"])).Begin();
        //}

        //private void DFGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        //{
        //    ((Storyboard)((sender as Grid).Resources["EvidenceGrid"])).Stop();
        //}
    }
}

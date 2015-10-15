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

        //private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        //{
        //    if (args?.NewValue != null)
        //    {
        //        VM?.UpdateCurrentFolderCategories();
        //    }
        //}

        //private void OnAddRemoveCategories_Click(object sender, RoutedEventArgs e)
        //{
        //    VM?.ToggleIsEditingCategories();

        //    //if (VM?.Binder?.CurrentFolder != null)
        //    //{
        //    //    VM?.ToggleIsEditingCategories();
        //    //    if (VM.Binder.CurrentFolder.IsEditingCategories)
        //    //    {
        //    //        FrameworkElement fe = sender as FrameworkElement;
        //    //        if (fe?.Tag is ItemsControl)
        //    //        {
        //    //            VM.UpdateCurrentFolderCategories();
        //    //            (fe.Tag as ItemsControl).ItemsSource = VM.FolderCategorySelector;
        //    //        }
        //    //    }
        //    //}
        //}

        private void OnDynamicFieldValueSelectComboBox_Opened(object sender, object e)
        {
            var cb = sender as ComboBox;
            if (cb != null && cb.DataContext is DynamicField)
            {
                cb.ItemsSource = (cb.DataContext as DynamicField).FieldDescription.PossibleValues;
            }
        }

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
            var fe = sender as TextBox;
            VM?.ChangeFieldValue(fe?.DataContext as DynamicField, fe?.Text);
        }
    }
}

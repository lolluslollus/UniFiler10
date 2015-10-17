using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
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
    public sealed partial class BinderView : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        //private void ClearListeners() { PropertyChanged = null; }
        private void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private BinderVM _vm = null;
        public BinderVM VM { get { return _vm; } private set { _vm = value; RaisePropertyChanged(); } }

        public BinderView()
        {
            InitializeComponent();
        }

        private async void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (args?.NewValue is Binder)
                await ActivateAsync(args.NewValue as Binder).ConfigureAwait(false);
        }

        private async Task ActivateAsync(Binder binder)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
            {
                if (_vm == null || _vm.Binder != binder)
                {
                    _vm?.Dispose(); // LOLLO the final dispose is missing, but that should not matter because the raiser clears the listeners when disposing.
                    _vm = null; // In fact, this makes BinderVM.Dispose() redundant, you can see it in the debugger.
                    VM = new BinderVM(binder);
                }
            }).AsTask().ConfigureAwait(false);
        }

        private void OnListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView lv = sender as ListView;

            // if (lv != null && VM != null && VM.Binder != null && lv.SelectedIndex >= 0 && VM.Binder.Folders.Count > lv.SelectedIndex)
            if (VM != null && VM.Binder != null && lv != null && lv.SelectedItem is Folder)
            {
                // VM.Binder.CurrentFolderId = VM.Binder.Folders[lv.SelectedIndex].Id;
                VM.Binder.CurrentFolderId = (lv.SelectedItem as Folder).Id;
            }
        }

        private async void OnDeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_vm != null) await _vm.DeleteFolderAsync((sender as FrameworkElement)?.DataContext as Folder).ConfigureAwait(false);
        }

        private async void OnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_vm != null) await _vm.AddFolderAsync().ConfigureAwait(false);
        }

        private void OnTogglePaneOpen(object sender, RoutedEventArgs e)
        {
            if (_vm?.Binder != null) _vm.Binder.IsPaneOpen = !_vm.Binder.IsPaneOpen;
        }
    }
}

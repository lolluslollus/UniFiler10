using System;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Utilz;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
    public sealed partial class BinderView : ObservableControl
    {
        private BinderVM _vm = null;
        public BinderVM VM { get { return _vm; } private set { _vm = value; RaisePropertyChanged_UI(); } }

        #region construct dispose
        public BinderView()
        {
            InitializeComponent();
        }
        private static SemaphoreSlimSafeRelease _vmSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        private async void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            try
            {
                await _vmSemaphore.WaitAsync().ConfigureAwait(false);
                if (args != null)
                {
                    var newBinder = args.NewValue as Binder;
                    if (newBinder == null)
                    {
                        if (_vm != null) await _vm.CloseAsync().ConfigureAwait(false);
                    }
                    else if (_vm == null)
                    {
                        VM = new BinderVM(newBinder);
                        // await _vm.OpenAsync().ConfigureAwait(false); // no: this particular class only opens when its Binder has opened
                    }
                    else if (_vm.Binder != newBinder)
                    {
                        await _vm.CloseAsync().ConfigureAwait(false);
                        VM = new BinderVM(newBinder);
                        // await _vm.OpenAsync().ConfigureAwait(false); // no: this particular class only opens when its Binder has opened
                    }
                }
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_vmSemaphore);
            }
        }
        #endregion construct dispose

        private async void OnListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                await _vmSemaphore.WaitAsync().ConfigureAwait(false);
                ListView lv = sender as ListView;
                if (_vm?.Binder != null && lv?.SelectedItem is Folder)
                {
                    await _vm.SelectFolderAsync((lv.SelectedItem as Folder).Id).ConfigureAwait(false);
                }
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_vmSemaphore);
            }
        }

        private async void OnDeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _vmSemaphore.WaitAsync().ConfigureAwait(false);
                if (_vm != null) await _vm.DeleteFolderAsync((sender as FrameworkElement)?.DataContext as Folder).ConfigureAwait(false);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_vmSemaphore);
            }
        }

        private async void OnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _vmSemaphore.WaitAsync().ConfigureAwait(false);
                if (_vm != null) await _vm.AddFolderAsync().ConfigureAwait(false);
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_vmSemaphore);
            }
        }

        private async void OnTogglePaneOpen(object sender, RoutedEventArgs e)
        {
            try
            {
                await _vmSemaphore.WaitAsync().ConfigureAwait(false);
                if (_vm?.Binder != null) _vm.Binder.IsPaneOpen = !_vm.Binder.IsPaneOpen;
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_vmSemaphore);
            }
        }

        private async void OnOpenCover_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _vmSemaphore.WaitAsync().ConfigureAwait(false);
                _vm?.OpenCover();
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_vmSemaphore);
            }
        }
    }
}

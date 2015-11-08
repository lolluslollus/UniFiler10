using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
    public sealed partial class BinderView : OpenableObservableControl
    {
        private BinderVM _vm = null;
        public BinderVM VM { get { return _vm; } private set { _vm = value; RaisePropertyChanged_UI(); } }

        #region construct dispose open close
        public BinderView()
        {
            OpenCloseWhenLoadedUnloaded = false;
            InitializeComponent();
        }

        private Binder _newBinderTemp = null;
        private async void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (args != null)
            {
                var newBinder = args.NewValue as Binder;
                if (newBinder != null && (_vm == null || _vm.Binder != newBinder))
                {
                    _newBinderTemp = newBinder;
                    await CloseAsync().ConfigureAwait(false);
                    await TryOpenAsync().ConfigureAwait(false);
                    _newBinderTemp = null;
                }
            }
        }
        protected override async Task<bool> OpenMayOverrideAsync()
        {
            if (_newBinderTemp != null)
            {
                VM = new BinderVM(_newBinderTemp);
                await _vm.OpenAsync().ConfigureAwait(false);
                return true;
            }
            else
            {
                return false;
            }
        }
        protected override async Task CloseMayOverrideAsync()
        {
            await _vm?.CloseAsync();
            _vm?.Dispose();
            _vm = null;
        }
        #endregion construct dispose open close

        private void OnListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView lv = sender as ListView;
            if (VM?.Binder != null && lv?.SelectedItem is Folder)
            {
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

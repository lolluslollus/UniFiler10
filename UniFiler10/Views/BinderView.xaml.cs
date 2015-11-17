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
    public sealed partial class BinderView : BackableOpenableObservableControl
    {
        private BinderVM _vm = null;
        public BinderVM VM { get { return _vm; } private set { _vm = value; RaisePropertyChanged_UI(); } }

        #region construct, dispose, open, close
        public BinderView()
        {
            OpenCloseWhenLoadedUnloaded = false;
            InitializeComponent();
        }
        protected override async Task<bool> OpenMayOverrideAsync()
        {
            if (DataContext is Binder)
            {
                _vm = new BinderVM(DataContext as Binder);
                await _vm.OpenAsync().ConfigureAwait(false);
                RaisePropertyChanged_UI(nameof(VM));

				RunInUiThread(delegate { RegisterBackEventHandlers(); });

                return true;
            }
            else
            {
                return false;
            }
        }
        protected override async Task CloseMayOverrideAsync()
        {
			RunInUiThread(delegate { UnregisterBackEventHandlers(); });

			await _vm?.CloseAsync();
            _vm?.Dispose();
            VM = null;
        }
        private static SemaphoreSlimSafeRelease _vmSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        private async void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            try
            {
                await _vmSemaphore.WaitAsync().ConfigureAwait(false);
                if (args != null)
                {
                    var newBinder = args.NewValue as Data.Model.Binder;
                    if (newBinder == null)
                    {
                        await CloseAsync().ConfigureAwait(false);
                    }
                    else if (_vm == null)
                    {
                        await CloseAsync().ConfigureAwait(false);
                        await TryOpenAsync().ConfigureAwait(false);
                    }
                    else if (_vm.Binder != newBinder)
                    {
                        await CloseAsync().ConfigureAwait(false);
                        await TryOpenAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_vmSemaphore);
            }
        }
        #endregion construct, dispose, open, close

        private void OnListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Task sel = _vm?.SelectFolderAsync(((sender as ListView)?.SelectedItem as Folder)?.Id);
        }

        private void OnDeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            Task del = _vm?.DeleteFolderAsync((sender as FrameworkElement)?.DataContext as Folder);
        }

        private void OnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            Task add = _vm?.AddFolderAsync();
        }

        //private void OnTogglePaneOpen(object sender, RoutedEventArgs e)
        //{
        //    _vm?.TogglePaneOpen();
        //}

        private void OnOpenCover_Click(object sender, RoutedEventArgs e)
        {
            _vm?.OpenCover();
        }

		protected override void CloseMe()
		{
			_vm?.GoBack();
		}
	}
}

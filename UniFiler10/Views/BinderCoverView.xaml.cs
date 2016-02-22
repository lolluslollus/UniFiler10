using System;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Utilz.Controlz;


// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class BinderCoverView : OpenableObservableControl
	{
		#region events
		public event EventHandler GoToSettingsRequested;
		public event EventHandler GoToBinderContentRequested;
		#endregion events


		#region properties
		private volatile BinderCoverVM _vm = null;
		public BinderCoverVM VM { get { return _vm; } set { _vm = value; RaisePropertyChanged_UI(); } }

		private readonly AnimationStarter _animationStarter = null;
		#endregion properties


		#region construct, open, close
		public BinderCoverView()
		{
			DataContextChanged += OnDataContextChanged;
			InitializeComponent();
			_animationStarter = AnimationsControl.AnimationStarter;
		}


		protected override async Task OpenMayOverrideAsync()
		{
			await UpdateVMAsync().ConfigureAwait(false);
			await AnimationsControl.OpenAsync().ConfigureAwait(false);
		}

		protected override async Task CloseMayOverrideAsync()
		{
			await DisposeVMAsync().ConfigureAwait(false);
			//_animationStarter.EndAllAnimations();
			await AnimationsControl.CloseAsync().ConfigureAwait(false);
		}

		private async Task UpdateVMAsync()
		{
			var binder = DataContext as Binder;
			if (binder != null && !binder.IsDisposed)
			{
				if (_vm == null)
				{
					_vm = new BinderCoverVM(binder, _animationStarter);
					await _vm.OpenAsync().ConfigureAwait(false);
					RaisePropertyChanged_UI(nameof(VM));
				}
				else if (_vm.Binder != binder)
				{
					await DisposeVMAsync().ConfigureAwait(false);

					_vm = new BinderCoverVM(binder, _animationStarter);
					await _vm.OpenAsync().ConfigureAwait(false);
					RaisePropertyChanged_UI(nameof(VM));
				}
			}
			else
			{
				await DisposeVMAsync().ConfigureAwait(false);
			}
		}

		private async Task DisposeVMAsync()
		{
			var vm = _vm;
			if (vm != null)
			{
				await vm.CloseAsync();
				vm.Dispose();
				VM = null;
			}
		}
		#endregion construct, open, close


		#region event handlers
		private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
		{
			Task upd = RunFunctionIfOpenAsyncT(UpdateVMAsync);
		}

		private void OnDocumentView_DeleteClicked(object sender, DocumentView.DocumentClickedArgs e)
		{
			Task del = _vm?.DeleteFolderAsync((sender as FrameworkElement)?.DataContext as Binder.FolderPreview);
		}

		private async void OnFolderPreviews_ItemClick(object sender, ItemClickEventArgs e)
		{
			var vm = _vm;
			if (vm == null) return;
			// LOLLO NOTE await instance?.method crashes if instance is null; await is not that clever yet.
			await vm.SetCurrentFolderAsync((e?.ClickedItem as Binder.FolderPreview)?.FolderId);
			GoToBinderContentRequested?.Invoke(this, EventArgs.Empty);
		}

		private void OnAddFolder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task add = _vm?.AddFolderAsync();
		}

		private async void OnAddAndOpenFolder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			var vm = _vm;
			if (vm == null) return;

			if (await vm.AddAndOpenFolderAsync()) //.ConfigureAwait(false);
			{
				GoToBinderContentRequested?.Invoke(this, EventArgs.Empty);
			}
		}

		private void OnImportFoldersFromBinder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			_vm?.StartImportFoldersFromBinder();
		}

		private void OnSettingsButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			GoToSettingsRequested?.Invoke(this, EventArgs.Empty);
		}
		#endregion event handlers
	}
}
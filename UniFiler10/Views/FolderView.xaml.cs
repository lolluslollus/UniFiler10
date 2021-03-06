﻿using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Utilz.Controlz;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;


// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class FolderView : OpenableObservableControl
	{
		#region properties
		private volatile FolderVM _vm = null;
		public FolderVM VM { get { return _vm; } private set { _vm = value; RaisePropertyChanged_UI(); } }

		private readonly AnimationStarter _animationStarter = null;
		#endregion properties

		public FolderView()
		{
			DataContextChanged += OnDataContextChanged;
			InitializeComponent();
			_animationStarter = AnimationsControl.AnimationStarter;
		}

		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			await UpdateFolderVMAsync().ConfigureAwait(false);
			await AnimationsControl.OpenAsync().ConfigureAwait(false);
		}

		protected override async Task CloseMayOverrideAsync()
		{
			await AnimationsControl.CloseAsync().ConfigureAwait(false);
			await DisposeFolderVMAsync().ConfigureAwait(false);
		}

		private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
		{
			Task upd = RunFunctionIfOpenAsyncT(UpdateFolderVMAsync);
		}
		private async Task UpdateFolderVMAsync()
		{
			var folder = DataContext as Folder;
			if (folder != null && !folder.IsDisposed)
			{
				if (_vm == null)
				{
					_vm = new FolderVM(DataContext as Folder, AudioRecorderView/*, CameraView*/, _animationStarter);
					await _vm.OpenAsync();
					RaisePropertyChanged_UI(nameof(VM));
				}
				else if (_vm.Folder != folder)
				{
					await DisposeFolderVMAsync();

					_vm = new FolderVM(DataContext as Folder, AudioRecorderView/*, CameraView*/, _animationStarter);
					await _vm.OpenAsync();
					RaisePropertyChanged_UI(nameof(VM));
				}
			}
			else
			{
				await DisposeFolderVMAsync().ConfigureAwait(false);
			}
		}

		private async Task DisposeFolderVMAsync()
		{
			var fvm = _vm;
			if (fvm != null)
			{
				await fvm.CloseAsync();
				fvm.Dispose();
			}
			_vm = null;
		}

		private async void OnVaalue_LostFocus(object sender, RoutedEventArgs e)
		{
			var textBox = sender as TextBox;
			var vm = VM;
			if (textBox == null || vm == null) return;

			await RunFunctionIfOpenAsyncT(async () =>
			{
				string oldText = textBox.Text;
				// LOLLO TODO check if I need to reset the value, it's new
				bool isValueSet = await vm.TrySetFieldValueAsync(textBox.DataContext as DynamicField, textBox.Text);
				if (!isValueSet) textBox.Text = oldText;
			}).ConfigureAwait(false);
		}
	}
}
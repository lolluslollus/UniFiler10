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
using UniFiler10.Controlz;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class FolderView : OpenableObservableControl
	{
		//public BinderContentVM VM
		//{
		//	get { return (BinderContentVM)GetValue(VMProperty); }
		//	set { SetValue(VMProperty, value); }
		//}
		//public static readonly DependencyProperty VMProperty =
		//	DependencyProperty.Register("VM", typeof(BinderContentVM), typeof(FolderView), new PropertyMetadata(null));

		private FolderVM _vm = null;
		public FolderVM VM { get { return _vm; } private set { _vm = value; RaisePropertyChanged_UI(); } }


		public FolderView()
		{
			DataContextChanged += OnDataContextChanged;
			InitializeComponent();
		}

		protected override Task OpenMayOverrideAsync()
		{
			return UpdateFolderVMAsync();
		}

		protected override async Task CloseMayOverrideAsync()
		{
			await DisposeFolderVMAsync().ConfigureAwait(false);
		}

		private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
		{
			Task upd = RunFunctionWhileOpenAsyncT(delegate
			{
				return UpdateFolderVMAsync();
			});
		}
		private async Task UpdateFolderVMAsync()
		{
			var folder = DataContext as Folder;
			if (folder != null && !folder.IsDisposed)
			{
				if (_vm == null)
				{
					_vm = new FolderVM(DataContext as Folder, AudioRecorderView/*, CameraView*/);
					await _vm.OpenAsync();
					RaisePropertyChanged_UI(nameof(VM));
				}
				else if (_vm.Folder != folder)
				{
					await DisposeFolderVMAsync();

					_vm = new FolderVM(DataContext as Folder, AudioRecorderView/*, CameraView*/);
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

		private void OnVaalue_LostFocus(object sender, RoutedEventArgs e)
		{
			var textBox = sender as TextBox;
			if (textBox != null)
			{
				Task setVal = VM?.TrySetFieldValueAsync(textBox.DataContext as DynamicField, textBox.Text);
			}
		}
	}
}

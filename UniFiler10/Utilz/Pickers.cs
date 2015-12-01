using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace UniFiler10.Utilz
{
	public class Pickers
	{
		public static async Task<StorageFolder> PickFolderAsync(string[] extensions)
		{
			//bool unsnapped = ((ApplicationView.Value != ApplicationViewState.Snapped) || ApplicationView.TryUnsnap());
			//if (unsnapped)
			//{

			var openPicker = new FolderPicker();
			openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
			//openPicker.CommitButtonText=
			//openPicker.ViewMode = PickerViewMode.List;
			foreach (var ext in extensions)
			{
				openPicker.FileTypeFilter.Add(ext);
			}
			var folder = await openPicker.PickSingleFolderAsync();
			return folder;

			//}
			//return false;
		}

		public static async Task<StorageFile> PickOpenFileAsync(string[] extensions)
		{
			var openPicker = new FileOpenPicker();
			openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
			//openPicker.CommitButtonText=
			//openPicker.ViewMode = PickerViewMode.List;
			foreach (var ext in extensions)
			{
				openPicker.FileTypeFilter.Add(ext);
			}

			var file = await openPicker.PickSingleFileAsync();
			return file;
		}

		public static async Task<StorageFile> PickSaveFileAsync(string[] extensions)
		{
			var picker = new FileSavePicker();
			picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
			//openPicker.CommitButtonText=
			//openPicker.ViewMode = PickerViewMode.List;
			foreach (var ext in extensions)
			{
				var exts = new List<string>(); exts.Add(ext);
				picker.FileTypeChoices.Add(ext + " file", exts);
			}

			var file = await picker.PickSaveFileAsync();
			return file;
		}
	}
}

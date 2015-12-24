using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.ViewManagement;

namespace Utilz
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
			//if (folder != null)
			//{
			//	// Application now has read/write access to all contents in the picked folder
			//	// (including other sub-folder contents)
			//	// LOLLO NOTE check https://msdn.microsoft.com/en-us/library/windows/apps/mt186452.aspx
			//	Windows.Storage.AccessCache.StorageApplicationPermissions.
			//	FutureAccessList.AddOrReplace("PickedFolderToken", folder);
			//}
			return folder;

			//}
			//return false;
		}

		public static async Task<StorageFile> PickOpenFileAsync(string[] extensions)
		{
			// test for phone: bring it to the UI thread
			StorageFile file = null;
			await Logger.AddAsync("About to pick file", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			try
			{
				Task<StorageFile> fileTask = null;
				await Logger.AddAsync("open picker opened", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
				await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
				{
					var openPicker = new FileOpenPicker();
					
					openPicker.ViewMode = PickerViewMode.Thumbnail;
					//openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
					openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
					//openPicker.CommitButtonText = "Pick a file"; // LOLLO localise this if you use it
					foreach (var ext in extensions)
					{
						openPicker.FileTypeFilter.Add(ext);
					}
					fileTask = openPicker.PickSingleFileAsync().AsTask();
				});
				file = await fileTask;

				if (file == null) await Logger.AddAsync("file picked, null", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
				else await Logger.AddAsync("file picked, not null", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}
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

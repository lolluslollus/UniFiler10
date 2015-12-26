using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Utilz
{
	public class FileDirectoryExts
	{
		private int _currentDepth = 0;
		// LOLLO TODO what if you copy a directory to an existing one? Shouldn't you delete the contents first? No! But then, shouldn't you issue a warning?
		public async Task CopyDirContentsReplacingAsync(StorageFolder from, StorageFolder to, int maxDepth = 0)
		{
			// read files
			var filesDepth0 = await from.GetFilesAsync().AsTask().ConfigureAwait(false);
			// copy files
			var copyTasks = new List<Task>();
			foreach (var file in filesDepth0)
			{
				copyTasks.Add(file.CopyAsync(to, file.Name, NameCollisionOption.ReplaceExisting).AsTask());
			}
			await Task.WhenAll(copyTasks).ConfigureAwait(false);

			//var plr = Parallel.ForEach(filesDepth0, (file) =>
			//{
			//	// LOLLO NOTE avoid async calls within a Parallel.ForEach coz they are not awaited
			//	file.CopyAsync(to, file.Name, NameCollisionOption.ReplaceExisting).AsTask().Wait();
			//});

			//Debug.WriteLine("CopyDirContentsReplacingAsync: plr is completed = " + plr.IsCompleted);

			//foreach (var file in filesDepth0)
			//{
			//	await file.CopyAsync(to, file.Name, NameCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
			//}
			// check depth
			_currentDepth += 1;
			if (_currentDepth > maxDepth) return;
			// read dirs
			var dirsDepth0 = await from.GetFoldersAsync().AsTask().ConfigureAwait(false);
			// copy dirs
			foreach (var dirFrom in dirsDepth0)
			{
				var dirTo = await to.CreateFolderAsync(dirFrom.Name, CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
				await CopyDirContentsReplacingAsync(dirFrom, dirTo).ConfigureAwait(false);
			}
		}
		public static async Task<ulong> GetFileSizeAsync(StorageFile file)
		{
			if (file == null) return 0;

			BasicProperties fileProperties = await file.GetBasicPropertiesAsync().AsTask().ConfigureAwait(false);
			if (fileProperties != null) return fileProperties.Size;
			else return 0;
		}
	}
}

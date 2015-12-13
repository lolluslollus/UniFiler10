﻿using System;
using System.Collections.Generic;
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
		public async Task CopyDirContentsAsync(StorageFolder from, StorageFolder to, int maxDepth = 0)
		{
			// read files
			var filesDepth0 = await from.GetFilesAsync().AsTask().ConfigureAwait(false);
			// copy files
			foreach (var file in filesDepth0)
			{
				await file.CopyAsync(to).AsTask().ConfigureAwait(false);
			}
			// check depth
			_currentDepth += 1;
			if (_currentDepth > maxDepth) return;
			// read dirs
			var dirsDepth0 = await from.GetFoldersAsync().AsTask().ConfigureAwait(false);
			// copy dirs
			foreach (var dirFrom in dirsDepth0)
			{
				var dirTo = await to.CreateFolderAsync(dirFrom.Name, CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
				await CopyDirContentsAsync(dirFrom, dirTo).ConfigureAwait(false);
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

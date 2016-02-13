using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Utilz;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Utilz
{
	public static class DocumentExtensions
	{
		public static readonly string[] AUDIO_EXTENSIONS = { ".mp3", ".wav", ".wma" };
		public static readonly string[] HTML_EXTENSIONS = { ".htm", ".html", ".mht" };
		public static readonly string PDF_EXTENSION = ".pdf";
		public static readonly string[] IMAGE_EXTENSIONS = { ".bmp", ".gif", ".giff", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };
		//public static readonly string TXT_EXTENSION = ".txt";

		internal static Task<StorageFile> PickMediaFileAsync()
		{
			var exts = HTML_EXTENSIONS.ToList();
			exts.Add(PDF_EXTENSION);
			exts.AddRange(IMAGE_EXTENSIONS);
			exts.AddRange(AUDIO_EXTENSIONS);

			return Pickers.PickOpenFileAsync(exts.ToArray());
		}

		public static async Task<string> GetTextFromFileAsync(string uri)
		{
			try
			{
				var file = await StorageFile.GetFileFromPathAsync(uri).AsTask().ConfigureAwait(false);
				if (file != null)
				{
					using (IInputStream stream = await file.OpenSequentialReadAsync().AsTask().ConfigureAwait(false))
					{
						using (StreamReader streamReader = new StreamReader(stream.AsStreamForRead()))
						{
							string ssss = await streamReader.ReadToEndAsync().ConfigureAwait(false);
							// await stream.AsStreamForRead().FlushAsync().ConfigureAwait(false);
							return ssss;
						}
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}
			return null;
		}

		public static async Task<StorageFile> WriteTextIntoFileAsync(string text, string fileName)
		{
			try
			{
				var file = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
				if (file != null)
				{
					using (var stream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
					{
						using (var writer = new StreamWriter(stream))
						{
							stream.Seek(0, SeekOrigin.Begin);
							await writer.WriteAsync(text).ConfigureAwait(false);
							await stream.FlushAsync();
							await writer.FlushAsync();
							return file;
						}
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}
			return null;
		}

	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilz;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;

namespace Utilz
{
	public static class DocumentExtensions
	{
		public static readonly string[] AUDIO_EXTENSIONS = new string[] { ".mp3", ".wav", ".wma" };
		public static readonly string[] HTML_EXTENSIONS = new string[] { ".htm", ".html", ".mht" };
		public static readonly string PDF_EXTENSION = ".pdf";
		public static readonly string[] IMAGE_EXTENSIONS = new string[] { ".bmp", ".gif", ".giff", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };
		//public static readonly string TXT_EXTENSION = ".txt";

		internal static Task<StorageFile> PickMediaFileAsync()
		{
			var exts = new List<string>();
			foreach (var ext in HTML_EXTENSIONS)
			{
				exts.Add(ext);
			}
			exts.Add(PDF_EXTENSION);
			foreach (var ext in IMAGE_EXTENSIONS)
			{
				exts.Add(ext);
			}
			foreach (var ext in AUDIO_EXTENSIONS)
			{
				exts.Add(ext);
			}

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
				var file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
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

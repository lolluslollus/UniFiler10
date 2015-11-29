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

namespace UniFiler10.Services
{
    public static class DocumentExtensions
    {
        public static readonly string[] AUDIO_EXTENSIONS = new string[] { ".mp3", ".wav", ".wma" };
        public static readonly string[] HTML_EXTENSIONS = new string[] { ".htm", ".html", ".mht" };
        public static readonly string PDF_EXTENSION = ".pdf";
        public static readonly string[] IMAGE_EXTENSIONS = new string[] { ".bmp", ".gif", ".giff", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };
		//public static readonly string TXT_EXTENSION = ".txt";

		internal static async Task<StorageFile> PickMediaFileAsync()
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            //openPicker.CommitButtonText=
            //openPicker.ViewMode = PickerViewMode.List;
            foreach (var ext in HTML_EXTENSIONS)
            {
                openPicker.FileTypeFilter.Add(ext);
            }
            openPicker.FileTypeFilter.Add(PDF_EXTENSION);
            foreach (var ext in IMAGE_EXTENSIONS)
            {
                openPicker.FileTypeFilter.Add(ext);
            }
            foreach (var ext in AUDIO_EXTENSIONS)
            {
                openPicker.FileTypeFilter.Add(ext);
            }
			//openPicker.FileTypeFilter.Add(TXT_EXTENSION);
			//openPicker.FileTypeFilter.Add(".jpg");
			//openPicker.FileTypeFilter.Add(".jpeg");
			//openPicker.FileTypeFilter.Add(".png");
			//openPicker.FileTypeFilter.Add(".bmp");
			//openPicker.FileTypeFilter.Add(".tif");
			//openPicker.FileTypeFilter.Add(".tiff");

			var file = await openPicker.PickSingleFileAsync();
            return file;
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
                await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
            }
            return null;
        }

    }
}

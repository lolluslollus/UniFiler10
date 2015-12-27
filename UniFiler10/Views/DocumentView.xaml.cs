using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using Utilz;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236
// LOLLO WebView: http://blogs.msdn.com/b/wsdevsol/archive/2012/10/18/nine-things-you-need-to-know-about-webview.aspx

namespace UniFiler10.Views
{
	public sealed partial class DocumentView : ObservableControl
	{
		#region properties
		public bool IsDeleteButtonEnabled
		{
			get { return (bool)GetValue(IsDeleteButtonEnabledProperty); }
			set { SetValue(IsDeleteButtonEnabledProperty, value); }
		}
		public static readonly DependencyProperty IsDeleteButtonEnabledProperty =
			DependencyProperty.Register("IsDeleteButtonEnabled", typeof(bool), typeof(DocumentView), new PropertyMetadata(true));

		/// <summary>
		/// This button is useful if I have web content coz the WebView does not relay the clicks
		/// </summary>
		public bool IsViewLargeButtonEnabled
		{
			get { return (bool)GetValue(IsViewLargeButtonEnabledProperty); }
			set { SetValue(IsViewLargeButtonEnabledProperty, value); }
		}
		public static readonly DependencyProperty IsViewLargeButtonEnabledProperty =
			DependencyProperty.Register("IsViewLargeButtonEnabled", typeof(bool), typeof(DocumentView), new PropertyMetadata(false));

		public bool IsClickSensitive
		{
			get { return (bool)GetValue(IsClickSensitiveProperty); }
			set { SetValue(IsClickSensitiveProperty, value); }
		}
		public static readonly DependencyProperty IsClickSensitiveProperty =
			DependencyProperty.Register("IsClickSensitive", typeof(bool), typeof(DocumentView), new PropertyMetadata(false));

		public Wallet Wallet
		{
			get { return (Wallet)GetValue(WalletProperty); }
			set { SetValue(WalletProperty, value); }
		}
		public static readonly DependencyProperty WalletProperty =
			DependencyProperty.Register("Wallet", typeof(Wallet), typeof(DocumentView), new PropertyMetadata(null));

		public Document Document
		{
			get { return (Document)GetValue(DocumentProperty); }
			set { SetValue(DocumentProperty, value); }
		}
		public static readonly DependencyProperty DocumentProperty =
			DependencyProperty.Register("Document", typeof(Document), typeof(DocumentView), new PropertyMetadata(null, OnDocumentChanged));
		private static async void OnDocumentChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			try
			{
				await _previousUriSemaphore.WaitAsync(); //.ConfigureAwait(false); // LOLLO NOTE we need accesses to DataContext and other UIControl properties to run in the UI thread, across the app!
				var instance = obj as DocumentView;
				if (instance != null)
				{
					var newDoc = args.NewValue as Document;
					if (newDoc == null)
					{
						instance._previousUri = null;
						Task render = instance.RenderPreviewAsync(newDoc);
					}
					else if (newDoc.GetFullUri0() != instance._previousUri)
					{
						instance._previousUri = newDoc.GetFullUri0();
						Task render = instance.RenderPreviewAsync(newDoc);
					}
				}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_previousUriSemaphore);
			}

		}

		private static readonly BitmapImage _voiceNoteImage = new BitmapImage() { UriSource = new Uri("ms-appx:///Assets/voice-200.png", UriKind.Absolute) };

		private bool _isMultiPage = false;
		public bool IsMultiPage { get { return _isMultiPage; } set { _isMultiPage = value; RaisePropertyChanged_UI(); } }

		private uint _height = 0;
		private uint _width = 0;
		#endregion properties


		#region ctor
		public DocumentView()
		{
			_height = (uint)(double)Application.Current.Resources["MiniatureHeight"];
			_width = (uint)(double)Application.Current.Resources["MiniatureWidth"];
			//DataContextChanged += OnDataContextChanged;

			InitializeComponent();
		}
		#endregion ctor


		#region render
		private string _previousUri = null;

		private static SemaphoreSlimSafeRelease _previousUriSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		//private async void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
		//{
		//	try
		//	{
		//		await _previousUriSemaphore.WaitAsync(); //.ConfigureAwait(false); // LOLLO NOTE we need accesses to DataContext and other UIControl properties to run in the UI thread, across the app!

		//		var newDoc = DataContext as Document;
		//		if (newDoc == null)
		//		{
		//			_previousUri = null;
		//			Task render = RenderPreviewAsync(newDoc);
		//		}
		//		else if (newDoc.GetFullUri0() != _previousUri)
		//		{
		//			_previousUri = newDoc.GetFullUri0();
		//			Task render = RenderPreviewAsync(newDoc);
		//		}
		//	}
		//	finally
		//	{
		//		SemaphoreSlimSafeRelease.TryRelease(_previousUriSemaphore);
		//	}
		//}

		private async Task RenderPreviewAsync(Document doc)
		{
			IsMultiPage = false;

			string ext = string.Empty;
			if (doc != null)
			{
				ext = Path.GetExtension(doc.Uri0).ToLower();
				if (ext == DocumentExtensions.PDF_EXTENSION)
				{
					await RenderFirstPDFPageAsync().ConfigureAwait(false);
				}
				//else if (ext == DocumentExtensions.TXT_EXTENSION)
				//{
				//	await RenderFirstTxtPageAsync().ConfigureAwait(false);
				//}
				else if (DocumentExtensions.IMAGE_EXTENSIONS.Contains(ext))
				{
					await RenderImageMiniatureAsync().ConfigureAwait(false);
				}
				else if (DocumentExtensions.HTML_EXTENSIONS.Contains(ext))
				{
					await RenderHtmlMiniatureAsync().ConfigureAwait(false);
				}
				else if (DocumentExtensions.AUDIO_EXTENSIONS.Contains(ext))
				{
					await RenderAudioMiniatureAsync().ConfigureAwait(false);
				}
			}
			else
			{
				await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
				{
					ShowImageViewer();
					ImageViewer.Source = null;
				}).AsTask().ConfigureAwait(false);
			}

			//await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
			//{
			//    Debug.WriteLine("RenderPreviewImageAsync() ended");
			//    Debug.WriteLine("RenderPreviewImageAsync() found ext = " + ext);
			//    if (WebViewer != null) Debug.WriteLine("WebViewer Visibility = " + WebViewer.Visibility.ToString());
			//    else Debug.WriteLine("WebViewer is null");
			//    if (ImageViewer != null) Debug.WriteLine("ImageViewer Visibility = " + ImageViewer.Visibility.ToString());
			//    else Debug.WriteLine("ImageViewer is null");
			//});
		}

		private async Task RenderHtmlMiniatureAsync()
		{
			string ssss = await DocumentExtensions.GetTextFromFileAsync(Document?.GetFullUri0()).ConfigureAwait(false);
			await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
			{
				ShowWebViewer();
				try
				{
					WebViewer.NavigateToString(ssss);
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
			}).AsTask().ConfigureAwait(false);
		}
		private async Task RenderHtmlMiniature2Async()
		{
			// LOLLO the WebView sometimes renders, sometimes not. This is pedestrian but works better than the other method.
			try
			{
				Uri uri = new Uri(Document?.GetFullUri0());
				if (uri != null)
				{
					await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
					{
						try
						{
							ShowWebViewer();
							WebViewer.Navigate(uri);
						}
						catch (Exception ex)
						{
							Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
						}
					}).AsTask().ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
			}
		}
		private async Task RenderAudioMiniatureAsync()
		{
			try
			{
				await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
				{
					ShowImageViewer();
					ImageViewer.Source = _voiceNoteImage;
				}).AsTask().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
			}
		}
		private async Task RenderImageMiniatureAsync()
		{
			try
			{
				var imgFile = await StorageFile.GetFileFromPathAsync(Document?.GetFullUri0()).AsTask().ConfigureAwait(false);
				if (imgFile != null)
				{
					using (IRandomAccessStream stream = await imgFile.OpenAsync(FileAccessMode.Read).AsTask().ConfigureAwait(false))
					{
						await DisplayImageFileAsync(stream).ConfigureAwait(false);
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}
		}
		private async Task RenderFirstPDFPageAsync()
		{
			try
			{
				var pdfFile = await StorageFile.GetFileFromPathAsync(Document?.GetFullUri0()).AsTask().ConfigureAwait(false);
				var pdfDocument = await PdfDocument.LoadFromFileAsync(pdfFile).AsTask().ConfigureAwait(false);
				if (pdfDocument?.PageCount > 0)
				{
					using (var pdfPage = pdfDocument.GetPage(0))
					{
						var renderOptions = GetPdfRenderOptions(pdfPage);
						if (renderOptions != null)
						{
							IsMultiPage = pdfDocument.PageCount > 1; // LOLLO TODO MAYBE deal with multi pages with tiff too ?
							using (var stream = new InMemoryRandomAccessStream())
							{
								await pdfPage.RenderToStreamAsync(stream, renderOptions).AsTask().ConfigureAwait(false);
								await stream.FlushAsync().AsTask().ConfigureAwait(false);
								await DisplayImageFileAsync(stream).ConfigureAwait(false);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
			}
		}
		//private async Task RenderFirstTxtPageAsync()
		//{
		//	try
		//	{
		//		var txtFile = await StorageFile.GetFileFromPathAsync(Document?.Uri0).AsTask().ConfigureAwait(false);

		//		string txt = await DocumentExtensions.GetTextFromFileAsync(Document?.Uri0).ConfigureAwait(false);

		//		var pdfDocument = await PdfDocument.LoadFromFileAsync(txtFile).AsTask().ConfigureAwait(false);
		//		if (pdfDocument?.PageCount > 0)
		//		{
		//			using (var pdfPage = pdfDocument.GetPage(0))
		//			{
		//				var renderOptions = GetPdfRenderOptions(pdfPage);
		//				if (renderOptions != null)
		//				{
		//					IsMultiPage = pdfDocument.PageCount > 1;
		//					using (var stream = new InMemoryRandomAccessStream())
		//					{
		//						await pdfPage.RenderToStreamAsync(stream, renderOptions).AsTask().ConfigureAwait(false);
		//						await stream.FlushAsync().AsTask().ConfigureAwait(false);
		//						await DisplayImageFileAsync(stream).ConfigureAwait(false);
		//					}
		//				}
		//			}
		//		}
		//	}
		//	catch (Exception ex)
		//	{
		//		await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
		//	}
		//}
		private async Task RenderFirstPDFPageWithFileCacheAsync()
		{
			// LOLLO TODO MAYBE we could reuse this method across different documents, on condition we reference the file somehow, to check if it already exists
			try
			{
				var pdfFile = await StorageFile.GetFileFromPathAsync(Document?.GetFullUri0()).AsTask().ConfigureAwait(false);

				var pdfDocument = await PdfDocument.LoadFromFileAsync(pdfFile);

				if (pdfDocument != null && pdfDocument.PageCount > 0)
				{
					using (var pdfPage = pdfDocument.GetPage(0))
					{
						var renderOptions = GetPdfRenderOptions(pdfPage);
						if (renderOptions != null)
						{
							StorageFolder tempFolder = ApplicationData.Current.TemporaryFolder;
							StorageFile jpgFile = await tempFolder.CreateFileAsync(Guid.NewGuid().ToString() + ".png", CreationCollisionOption.ReplaceExisting);
							if (jpgFile != null)
							{
								IsMultiPage = pdfDocument.PageCount > 1;
								using (IRandomAccessStream randomStream = await jpgFile.OpenAsync(FileAccessMode.ReadWrite))
								{
									await pdfPage.RenderToStreamAsync(randomStream, renderOptions).AsTask().ConfigureAwait(false);
									await randomStream.FlushAsync();
								}
								await DisplayImageFileAsync(jpgFile);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
			}
		}
		private PdfPageRenderOptions GetPdfRenderOptions(PdfPage pdfPage)
		{
			PdfPageRenderOptions output = null;
			if (pdfPage != null)
			{
				double xZoomFactor = pdfPage.Size.Height / _height;
				double yZoomFactor = pdfPage.Size.Width / _width;
				double zoomFactor = Math.Max(xZoomFactor, yZoomFactor);
				if (zoomFactor > 0)
				{
					output = new PdfPageRenderOptions()
					{
						DestinationHeight = (uint)(pdfPage.Size.Height / zoomFactor),
						DestinationWidth = (uint)(pdfPage.Size.Width / zoomFactor)
					};
				}
			}
			return output;
		}
		private async Task DisplayImageFileAsync(StorageFile file)
		{
			if (file != null)
			{
				IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read).AsTask().ConfigureAwait(false);
				await DisplayImageFileAsync(stream).ConfigureAwait(false);
			}
		}
		//private static void OnPixelHeightChanged(DependencyObject obj, DependencyProperty prop)
		//{

		//}
		//private static void OnPixelWidthChanged(DependencyObject obj, DependencyProperty prop)
		//{

		//}

		private async Task DisplayImageFileAsync(IRandomAccessStream stream)
		{
			if (stream != null)
			{
				await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
				{
					ShowImageViewer();
					BitmapImage src = new BitmapImage() { DecodePixelHeight = (int)_height };
					src.SetSource(stream);
					ImageViewer.Source = src;
				}).AsTask().ConfigureAwait(false);
			}
		}
		private void ShowWebViewer()
		{
			ImageViewer.Visibility = Visibility.Collapsed;
			WebViewer.Visibility = Visibility.Visible;
		}
		private void ShowImageViewer()
		{
			ImageViewer.Visibility = Visibility.Visible;
			WebViewer.Visibility = Visibility.Collapsed;
		}
		#endregion render


		#region events
		public class DocumentClickedArgs : EventArgs
		{
			private Wallet _wallet = null;
			public Wallet Wallet { get { return _wallet; } }

			private Document _document = null;
			public Document Document { get { return _document; } }

			public DocumentClickedArgs(Wallet wallet, Document document)
			{
				_wallet = wallet;
				_document = document;
			}
		}
		public event EventHandler<DocumentClickedArgs> DeleteClicked;

		public event EventHandler<DocumentClickedArgs> DocumentClicked;
		#endregion events


		#region event handlers
		private void OnItemDelete_Tapped(object sender, TappedRoutedEventArgs e)
		{
			e.Handled = true;
			if (IsDeleteButtonEnabled) DeleteClicked?.Invoke(this, new DocumentClickedArgs(Wallet, Document));
		}

		private void OnPreview_Tapped(object sender, TappedRoutedEventArgs e)
		{
			e.Handled = true;
			DocumentClicked?.Invoke(this, new DocumentClickedArgs(Wallet, Document));
		}

		private void OnMainBorder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			if (IsClickSensitive) OnPreview_Tapped(sender, e);
		}
		#endregion event handlers
	}
}
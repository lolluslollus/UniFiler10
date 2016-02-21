﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using Utilz;
using Utilz.Controlz;
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
		private static readonly double _widthSmall = (double)(Application.Current.Resources["MiniatureWidthSmall"]);
		private static readonly double _widthLarge = (double)(Application.Current.Resources["MiniatureWidth"]);
		private static readonly double _heightSmall = (double)(Application.Current.Resources["MiniatureHeightSmall"]);
		private static readonly double _heightLarge = (double)(Application.Current.Resources["MiniatureHeight"]);
		private static readonly double _quarterHeightSmall = (double)(Application.Current.Resources["MiniatureQuarterHeightSmall"]);
		private static readonly double _quarterHeightLarge = (double)(Application.Current.Resources["MiniatureQuarterHeight"]);

		private bool IsSmallScreen
		{
			get { return (bool)GetValue(IsSmallScreenProperty); }
			set { SetValue(IsSmallScreenProperty, value); }
		}
		private static readonly DependencyProperty IsSmallScreenProperty =
			DependencyProperty.Register("IsSmallScreen", typeof(bool), typeof(DocumentView), new PropertyMetadata(true, OnIsSmallScreenChanged));

		private static void OnIsSmallScreenChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			var instance = obj as DocumentView;
			if (instance == null || args.NewValue == args.OldValue) return;
			var isSmallScreen = args.NewValue is bool && (bool)args.NewValue;
			instance.QuarterHeightAdjusted = isSmallScreen ? _quarterHeightSmall : _quarterHeightLarge;
			instance.HeightAdjusted = isSmallScreen ? _heightSmall : _heightLarge;
			instance.WidthAdjusted = isSmallScreen ? _widthSmall : _widthLarge;
		}

		public double HeightAdjusted
		{
			get { return (double)GetValue(HeightAdjustedProperty); }
			private set { SetValue(HeightAdjustedProperty, value); }
		}
		public static readonly DependencyProperty HeightAdjustedProperty =
			DependencyProperty.Register("HeightAdjusted", typeof(double), typeof(DocumentView), new PropertyMetadata((double)(Application.Current.Resources["MiniatureHeightSmall"]), OnWidthOrHeightChanged));

		public double QuarterHeightAdjusted
		{
			get { return (double)GetValue(QuarterHeightAdjustedProperty); }
			private set { SetValue(QuarterHeightAdjustedProperty, value); }
		}
		public static readonly DependencyProperty QuarterHeightAdjustedProperty =
			DependencyProperty.Register("QuarterHeightAdjusted", typeof(double), typeof(DocumentView), new PropertyMetadata((double)(Application.Current.Resources["MiniatureQuarterHeightSmall"])));

		public double WidthAdjusted
		{
			get { return (double)GetValue(WidthAdjustedProperty); }
			private set { SetValue(WidthAdjustedProperty, value); }
		}
		public static readonly DependencyProperty WidthAdjustedProperty =
			DependencyProperty.Register("WidthAdjusted", typeof(double), typeof(DocumentView), new PropertyMetadata((double)(Application.Current.Resources["MiniatureWidthSmall"]), OnWidthOrHeightChanged));
		private static void OnWidthOrHeightChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			var instance = obj as DocumentView;
			if (instance == null || args.NewValue == args.OldValue) return;

			try
			{
				var doc = instance.Document;
				if (doc == null) return;
				double height = instance.HeightAdjusted;
				double width = instance.WidthAdjusted;
				if (height == _heightSmall && width != _widthSmall) return; // avoid rendering once when height changes and again when width changes
				if (height == _heightLarge && width != _widthLarge) return; // avoid rendering once when height changes and again when width changes

				if (Path.GetExtension(doc.Uri0).ToLower() == DocumentExtensions.PDF_EXTENSION)
				{
					Task render = Task.Run(() => instance.RenderPreviewAsync(doc, height, width));
				}
			}
			catch (ArgumentException ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}

		public string Caption
		{
			get { return (string)GetValue(CaptionProperty); }
			set { SetValue(CaptionProperty, value); }
		}
		public static readonly DependencyProperty CaptionProperty =
			DependencyProperty.Register("Caption", typeof(string), typeof(DocumentView), new PropertyMetadata(""));


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

		public bool IsSaveButtonEnabled
		{
			get { return (bool)GetValue(IsSaveButtonEnabledProperty); }
			set { SetValue(IsSaveButtonEnabledProperty, value); }
		}
		public static readonly DependencyProperty IsSaveButtonEnabledProperty =
			DependencyProperty.Register("IsSaveButtonEnabled", typeof(bool), typeof(DocumentView), new PropertyMetadata(false));

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
		private static void OnDocumentChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			try
			{
				//await _previousUriSemaphore.WaitAsync(); //.ConfigureAwait(false); // LOLLO NOTE we need accesses to DataContext and other UIControl properties to run in the UI thread, across the app!
				var instance = obj as DocumentView;
				if (instance == null) return;
				var doc = instance.Document;
				double height = instance.HeightAdjusted;
				double width = instance.WidthAdjusted;

				if (doc == null && instance._previousUri != null)
				{
					instance._previousUri = null;
					Task render = Task.Run(() => instance.RenderPreviewAsync(doc, height, width));
				}
				else if (doc.GetFullUri0() != instance._previousUri)
				{
					instance._previousUri = doc.GetFullUri0();
					Task render = Task.Run(() => instance.RenderPreviewAsync(doc, height, width));
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			//finally
			//{
			//	SemaphoreSlimSafeRelease.TryRelease(_previousUriSemaphore);
			//}
		}

		private static readonly BitmapImage _voiceNoteImage = new BitmapImage() { UriSource = new Uri("ms-appx:///Assets/voice-200.png", UriKind.Absolute) };

		private volatile bool _isMultiPage = false;
		public bool IsMultiPage { get { return _isMultiPage; } set { _isMultiPage = value; RaisePropertyChanged_UI(); } }
		#endregion properties


		#region ctor
		public DocumentView()
		{
			InitializeComponent();
		}
		#endregion ctor


		#region render
		private string _previousUri = null;

		//private readonly SemaphoreSlimSafeRelease _previousUriSemaphore = new SemaphoreSlimSafeRelease(1, 1);

		private async Task RenderPreviewAsync(Document doc, double height, double width)
		{
			// LOLLO TODO make sure this is not called too often!
			IsMultiPage = false;

			string ext = string.Empty;
			if (doc != null)
			{
				try
				{
					ext = Path.GetExtension(doc.Uri0).ToLower();
				}
				catch (ArgumentException ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}

				if (ext == DocumentExtensions.PDF_EXTENSION)
				{
					await RenderFirstPDFPageAsync(doc, height, width).ConfigureAwait(false);
				}
				//else if (ext == DocumentExtensions.TXT_EXTENSION)
				//{
				//	await RenderFirstTxtPageAsync().ConfigureAwait(false);
				//}
				else if (DocumentExtensions.IMAGE_EXTENSIONS.Contains(ext))
				{
					await RenderImageMiniatureAsync(doc).ConfigureAwait(false);
				}
				else if (DocumentExtensions.HTML_EXTENSIONS.Contains(ext))
				{
					await RenderHtmlMiniatureAsync(doc).ConfigureAwait(false);
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

		private async Task RenderHtmlMiniatureAsync(Document doc)
		{
			try
			{
				string uri = doc?.GetFullUri0();

				string ssss = await DocumentExtensions.GetTextFromFileAsync(uri).ConfigureAwait(false);
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
			catch { }
		}
		//private async Task RenderHtmlMiniature2Async()
		//{
		//	// LOLLO the WebView sometimes renders, sometimes not. This is pedestrian but works better than the other method.
		//	try
		//	{
		//		string uriStr = string.Empty;
		//		await RunInUiThreadAsync(delegate { uriStr = Document?.GetFullUri0(); }).ConfigureAwait(false);

		//		Uri uri = new Uri(uriStr);
		//		await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
		//		{
		//			try
		//			{
		//				ShowWebViewer();
		//				WebViewer.Navigate(uri);
		//			}
		//			catch (Exception ex)
		//			{
		//				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
		//			}
		//		}).AsTask().ConfigureAwait(false);
		//	}
		//	catch (Exception ex)
		//	{
		//		await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
		//	}
		//}
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
		private async Task RenderImageMiniatureAsync(Document doc)
		{
			try
			{
				string uri = doc?.GetFullUri0();
				var imgFile = await StorageFile.GetFileFromPathAsync(uri).AsTask().ConfigureAwait(false);
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
		private async Task RenderFirstPDFPageAsync(Document doc, double height, double width)
		{
			try
			{
				string uri = doc?.GetFullUri0();

				var pdfFile = await StorageFile.GetFileFromPathAsync(uri).AsTask().ConfigureAwait(false);
				var pdfDocument = await PdfDocument.LoadFromFileAsync(pdfFile).AsTask().ConfigureAwait(false);
				if (pdfDocument?.PageCount > 0)
				{
					using (var pdfPage = pdfDocument.GetPage(0))
					{
						var renderOptions = GetPdfRenderOptions(pdfPage, height, width);
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

		//private async Task RenderFirstPDFPageWithFileCacheAsync()
		//{
		//	// LOLLO TODO MAYBE we could reuse this method across different documents, on condition we reference the file somehow, to check if it already exists
		//	try
		//	{
		//		var pdfFile = await StorageFile.GetFileFromPathAsync(Document?.GetFullUri0()).AsTask().ConfigureAwait(false);

		//		var pdfDocument = await PdfDocument.LoadFromFileAsync(pdfFile);

		//		if (pdfDocument != null && pdfDocument.PageCount > 0)
		//		{
		//			using (var pdfPage = pdfDocument.GetPage(0))
		//			{
		//				var renderOptions = GetPdfRenderOptions(pdfPage);
		//				if (renderOptions != null)
		//				{
		//					StorageFolder tempFolder = ApplicationData.Current.LocalCacheFolder;
		//					StorageFile jpgFile = await tempFolder.CreateFileAsync(Guid.NewGuid().ToString() + ".png", CreationCollisionOption.ReplaceExisting);
		//					if (jpgFile != null)
		//					{
		//						IsMultiPage = pdfDocument.PageCount > 1;
		//						using (IRandomAccessStream randomStream = await jpgFile.OpenAsync(FileAccessMode.ReadWrite))
		//						{
		//							await pdfPage.RenderToStreamAsync(randomStream, renderOptions).AsTask().ConfigureAwait(false);
		//							await randomStream.FlushAsync();
		//						}
		//						await DisplayImageFileAsync(jpgFile);
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
		private PdfPageRenderOptions GetPdfRenderOptions(PdfPage pdfPage, double height, double width)
		{
			PdfPageRenderOptions output = null;
			if (pdfPage == null) return null;

			double xZoomFactor = pdfPage.Size.Height / height; // _height;
			double yZoomFactor = pdfPage.Size.Width / width; // _width;
			double zoomFactor = Math.Max(xZoomFactor, yZoomFactor);
			if (zoomFactor > 0)
			{
				output = new PdfPageRenderOptions()
				{
					DestinationHeight = (uint)(pdfPage.Size.Height / zoomFactor),
					DestinationWidth = (uint)(pdfPage.Size.Width / zoomFactor)
				};
			}
			return output;
		}
		//private async Task DisplayImageFileAsync(StorageFile file)
		//{
		//	if (file != null)
		//	{
		//		IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read).AsTask().ConfigureAwait(false);
		//		await DisplayImageFileAsync(stream).ConfigureAwait(false);
		//	}
		//}

		private async Task DisplayImageFileAsync(IRandomAccessStream stream)
		{
			if (stream != null)
			{
				await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
				{
					ShowImageViewer();
					BitmapImage src = new BitmapImage() { DecodePixelHeight = (int)HeightAdjusted }; // _height };
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
			private readonly Wallet _wallet = null;
			public Wallet Wallet { get { return _wallet; } }

			private readonly Document _document = null;
			public Document Document { get { return _document; } }

			public DocumentClickedArgs(Wallet wallet, Document document)
			{
				_wallet = wallet;
				_document = document;
			}
		}
		public event EventHandler<DocumentClickedArgs> DeleteClicked;

		public event EventHandler<DocumentClickedArgs> DocumentClicked;

		public event EventHandler<DocumentClickedArgs> SaveClicked;
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

		private void OnSave_Tapped(object sender, TappedRoutedEventArgs e)
		{
			e.Handled = true;
			SaveClicked?.Invoke(this, new DocumentClickedArgs(Wallet, Document));
		}
		#endregion event handlers
	}
}
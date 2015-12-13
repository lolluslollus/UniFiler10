using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
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
using Windows.Data.Pdf;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using Windows.System;
using System.Diagnostics;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236
// LOLLO WebView: http://blogs.msdn.com/b/wsdevsol/archive/2012/10/18/nine-things-you-need-to-know-about-webview.aspx

namespace UniFiler10.Views
{
	public sealed partial class DocumentView : ObservableControl
	{
		#region properties
		public bool IsDeleteEnabled
		{
			get { return (bool)GetValue(IsDeleteEnabledProperty); }
			set { SetValue(IsDeleteEnabledProperty, value); }
		}
		public static readonly DependencyProperty IsDeleteEnabledProperty =
			DependencyProperty.Register("IsDeleteEnabled", typeof(bool), typeof(DocumentView), new PropertyMetadata(true));

		public FolderVM FolderVM
		{
			get { return (FolderVM)GetValue(BinderVMProperty); }
			set { SetValue(BinderVMProperty, value); }
		}
		public static readonly DependencyProperty BinderVMProperty =
			DependencyProperty.Register("FolderVM", typeof(FolderVM), typeof(DocumentView), new PropertyMetadata(null));

		private static readonly BitmapImage _voiceNoteImage = new BitmapImage() { UriSource = new Uri("ms-appx:///Assets/voice-200.png", UriKind.Absolute) };

		public Wallet Wallet
		{
			get { return (Wallet)GetValue(WalletProperty); }
			set { SetValue(WalletProperty, value); }
		}
		public static readonly DependencyProperty WalletProperty =
			DependencyProperty.Register("Wallet", typeof(Wallet), typeof(DocumentView), new PropertyMetadata(null));

		public Folder Folder
		{
			get { return (Folder)GetValue(FolderProperty); }
			set { SetValue(FolderProperty, value); }
		}
		public static readonly DependencyProperty FolderProperty =
			DependencyProperty.Register("Folder", typeof(Folder), typeof(DocumentView), new PropertyMetadata(null));

		private bool _isMultiPage = false;
		public bool IsMultiPage { get { return _isMultiPage; } set { _isMultiPage = value; RaisePropertyChanged_UI(); } }

		private uint _height = 0;
		private uint _width = 0;
		#endregion properties


		#region construct
		public DocumentView()
		{
			_height = (uint)(double)Application.Current.Resources["MiniatureHeight"];
			_width = (uint)(double)Application.Current.Resources["MiniatureWidth"];
			DataContextChanged += OnDataContextChanged;

			InitializeComponent();
		}
		#endregion construct


		#region render
		private string _previousUri = null;
		//public string PreviousUri { get { return _previousUri; } private set { _previousUri = value; RaisePropertyChanged_UI(); } }

		private static SemaphoreSlimSafeRelease _previousUriSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		private async void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
		{
			try
			{
				await _previousUriSemaphore.WaitAsync(); //.ConfigureAwait(false); // LOLLO NOTE we need accesses to DataContext and other UIControl properties to run in the UI thread, across the app!
														 //if (args != null)
														 //{
														 // var newDoc = args.NewValue as Document;
				var newDoc = DataContext as Document;
				if (newDoc == null)
				{
					_previousUri = null;
					Task render = RenderPreviewAsync(newDoc);
					// await RenderPreviewAsync(newDoc).ConfigureAwait(false);
				}
				else if (newDoc.GetFullUri0() != _previousUri)
				{
					_previousUri = newDoc.GetFullUri0();
					Task render = RenderPreviewAsync(newDoc);
					// await RenderPreviewAsync(newDoc).ConfigureAwait(false);
				}
				//}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_previousUriSemaphore);
			}
		}

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
			string ssss = await DocumentExtensions.GetTextFromFileAsync((DataContext as Document)?.GetFullUri0()).ConfigureAwait(false);
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
				Uri uri = new Uri((DataContext as Document)?.GetFullUri0());
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
				var imgFile = await StorageFile.GetFileFromPathAsync((DataContext as Document)?.GetFullUri0()).AsTask().ConfigureAwait(false);
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
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
			}
		}
		private async Task RenderFirstPDFPageAsync()
		{
			try
			{
				var pdfFile = await StorageFile.GetFileFromPathAsync((DataContext as Document)?.GetFullUri0()).AsTask().ConfigureAwait(false);
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
		//		var txtFile = await StorageFile.GetFileFromPathAsync((DataContext as Document)?.Uri0).AsTask().ConfigureAwait(false);

		//		string txt = await DocumentExtensions.GetTextFromFileAsync((DataContext as Document)?.Uri0).ConfigureAwait(false);

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
				var pdfFile = await StorageFile.GetFileFromPathAsync((DataContext as Document)?.GetFullUri0()).AsTask().ConfigureAwait(false);

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
					//src.RegisterPropertyChangedCallback(BitmapImage.PixelHeightProperty, OnPixelHeightChanged);
					//src.RegisterPropertyChangedCallback(BitmapImage.PixelWidthProperty, OnPixelWidthChanged);
					src.SetSource(stream);
					ImageViewer.Source = src;
				}).AsTask().ConfigureAwait(false);
			}
		}
		private void ShowWebViewer()
		{
			ImageViewer.Visibility = Visibility.Collapsed;
			WebViewer.Visibility = Visibility.Visible;
			//WebViewBox.Visibility = Visibility.Visible;
		}
		private void ShowImageViewer()
		{
			ImageViewer.Visibility = Visibility.Visible;
			//WebViewBox.Visibility = Visibility.Collapsed;
			WebViewer.Visibility = Visibility.Collapsed;
		}

		//private void WebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
		//{
		//    Debug.WriteLine("WebViewer.NavigationCompleted fired");
		//    Debug.WriteLine("WebViewer Visibility = " + WebViewer.Visibility.ToString());
		//    Debug.WriteLine("ImageViewer Visibility = " + ImageViewer.Visibility.ToString());
		//}

		//private void WebView_NavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
		//{
		//    Debug.WriteLine("WebViewer.NavigationFailed fired");
		//    Debug.WriteLine("WebViewer Visibility = " + WebViewer.Visibility.ToString());
		//    Debug.WriteLine("ImageViewer Visibility = " + ImageViewer.Visibility.ToString());
		//}
		#endregion render

		#region event handlers
		private async void OnItemDelete_Tapped(object sender, TappedRoutedEventArgs e)
		{
			e.Handled = true;
			if (IsDeleteEnabled && FolderVM != null)
			{
				if (await FolderVM.RemoveDocumentFromWalletAsync(Wallet, DataContext as Document).ConfigureAwait(false))
				{
					// if there are no more documents in the wallet, delete the wallet
					await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
					{
						if (Wallet?.Documents?.Count <= 0)
						{
							Task del2 = FolderVM?.RemoveWalletFromFolderAsync(Folder, Wallet);
						}
					}).AsTask().ConfigureAwait(false);
				}
			}
		}

		private async void OnPreview_Tapped(object sender, TappedRoutedEventArgs e)
		{
			//Debug.WriteLine("WebViewer Visibility = " + WebViewer.Visibility.ToString());
			//Debug.WriteLine("ImageViewer Visibility = " + ImageViewer.Visibility.ToString());
			//WebViewer.Refresh();
			//return;

			//if (IsRedirectTapped) return;
			e.Handled = true;

			var doc = DataContext as Document;
			if (doc != null && !string.IsNullOrWhiteSpace(doc.Uri0))
			{
				var file = await StorageFile.GetFileFromPathAsync(doc.GetFullUri0()).AsTask(); //.ConfigureAwait(false);
				if (file != null)
				{
					bool isOk = false;
					try
					{
						//isOk = await Launcher.LaunchFileAsync(file, new LauncherOptions() { DisplayApplicationPicker = true }).AsTask().ConfigureAwait(false);
						isOk = await Launcher.LaunchFileAsync(file).AsTask().ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
					}
				}
			}
		}
		#endregion event handlers
	}
}

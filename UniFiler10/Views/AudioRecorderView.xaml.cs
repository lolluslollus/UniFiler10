using System;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.Data.Runtime;
using UniFiler10.ViewModels;
using Utilz;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	/// <summary>
	/// This control is supposed to run inside a Popup.
	/// Showing or hiding the popup will open or close the control.
	/// </summary>
	public sealed partial class AudioRecorderView : OpenableObservableControl, IMessageWriter, IRecorder
	{
		#region properties
		//private static SemaphoreSlimSafeRelease _vmSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		//public BinderContentVM VM
		//{
		//	get { return (BinderContentVM)GetValue(VMProperty); }
		//	set { SetValue(VMProperty, value); }
		//}
		//public static readonly DependencyProperty VMProperty =
		//	DependencyProperty.Register("VM", typeof(BinderContentVM), typeof(AudioRecorderView), new PropertyMetadata(null));

		private AudioRecorder _audioRecorder = null;
		private MediaCapture _mediaCapture;

		private string _lastMessage = string.Empty;
		public string LastMessage { get { return _lastMessage; } set { _lastMessage = value; RaisePropertyChanged_UI(); } }

		// Prevent the screen from sleeping while the camera is running
		//private readonly DisplayRequest _displayRequest = new DisplayRequest();

		// For listening to media property changes
		//private readonly SystemMediaTransportControls _systemMediaControls = SystemMediaTransportControls.GetForCurrentView();

		private SemaphoreSlimSafeRelease _triggerSemaphore = null;
		#endregion properties


		#region IRecorder
		[STAThread]
		public Task<bool> StartAsync(StorageFile file)
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				bool isOk = false;
				RecordingStoryboard.Begin();
				try
				{
					_audioRecorder = new AudioRecorder(this, file);
					_audioRecorder.UnrecoverableError += OnAudioRecorder_UnrecoverableError;
					await _audioRecorder.OpenAsync();
					// adjust the microphone volume. You need MediaCapture, apparently, and she needs STAThread. Ridiculous.
					_mediaCapture = new MediaCapture();
					// LOLLO TODO the following fails with the phone
					// var settings = new MediaCaptureInitializationSettings { AudioDeviceId = RuntimeData.Instance?.AudioDevice?.Id, MediaCategory = MediaCategory.Other, StreamingCaptureMode = StreamingCaptureMode.Audio };
					// the following works with the phone
					var settings = new MediaCaptureInitializationSettings { StreamingCaptureMode = StreamingCaptureMode.Audio };
					_mediaCapture.Failed += OnMediaCapture_Failed;
					await _mediaCapture.InitializeAsync(settings);
					_mediaCapture.AudioDeviceController.Muted = false;
					_mediaCapture.AudioDeviceController.VolumePercent = (float)VolumeSlider.Value;

					//_mediaCapture.AudioDeviceController.VolumePercent = 100.0F;
					// The following is useless, it was a feeble attempt at getting a graphical display of audio levels. It fails, don't use it.
					//await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate { PreviewControl.Source = _mediaCapture; });
					//await _mediaCapture.StartPreviewAsync();
					isOk = await _audioRecorder.RecordStartAsync().ConfigureAwait(false);
					if (isOk)
					{
						_triggerSemaphore = new SemaphoreSlimSafeRelease(0, 1); // this semaphore is always closed at the beginning
						try
						{
							await _triggerSemaphore.WaitAsync().ConfigureAwait(false);
						}
						catch (Exception ex)
						{
							if (SemaphoreSlimSafeRelease.IsAlive(_triggerSemaphore))
							{
								Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
							}
						}
					}
					else
					{
						NotifyOfFailure();
					}
				}
				catch (Exception ex)
				{
					await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
				}
				return isOk;
			});
		}

		[STAThread]
		public Task<bool> StopAsync()
		{
			SemaphoreSlimSafeRelease.TryRelease(_triggerSemaphore);
			return RunFunctionWhileOpenAsyncTB(delegate
			{
				return Stop2Async();
			});
		}
		private async Task<bool> Stop2Async()
		{
			await StopRecordingAsync().ConfigureAwait(false);

			var mc = _mediaCapture;
			if (mc != null)
			{
				mc.Failed -= OnMediaCapture_Failed;
				try
				{
					mc.Dispose();
				}
				catch { }
			}
			_mediaCapture = null;

			var audioRecorder = _audioRecorder;
			if (audioRecorder != null)
			{
				audioRecorder.UnrecoverableError -= OnAudioRecorder_UnrecoverableError;
				await audioRecorder.CloseAsync();
				audioRecorder.Dispose();
			}
			_audioRecorder = null;

			StopAllAnimations();
			return true;
		}
		#endregion IRecorder


		public AudioRecorderView()
		{
			//Loaded += OnLoaded;
			//Unloaded += OnUnloaded;
			//Application.Current.Resuming += OnResuming;
			//Application.Current.Suspending += OnSuspending;

			InitializeComponent();
		}
		/// <summary>
		/// I need this override to stop any running media recording
		/// </summary>
		/// <returns></returns>
		public override async Task<bool> CloseAsync()
		{
			if (!_isOpen) return false;
			SemaphoreSlimSafeRelease.TryRelease(_triggerSemaphore);
			return await base.CloseAsync().ConfigureAwait(false);
		}
		protected override async Task CloseMayOverrideAsync()
		{
			await Stop2Async().ConfigureAwait(false);
			SemaphoreSlimSafeRelease.TryDispose(_triggerSemaphore);
			_triggerSemaphore = null;
		}

		//private bool _isLoaded = false;
		//private bool _isLoadedWhenSuspending = false;
		//private async void OnSuspending(object sender, SuspendingEventArgs e)
		//{
		//	var deferral = e.SuspendingOperation.GetDeferral();

		//	//_isLoadedWhenSuspending = _isLoaded;
		//	await CloseAsync().ConfigureAwait(false);

		//	deferral.Complete();
		//}

		//private async void OnResuming(object sender, object e)
		//{
		//	//if (_isLoadedWhenSuspending) await OpenAsync().ConfigureAwait(false);
		//}

		//private async void OnLoaded(object sender, RoutedEventArgs e)
		//{
		//	_isLoaded = true;
		//	await OpenAsync().ConfigureAwait(false);
		//}

		//private async void OnUnloaded(object sender, RoutedEventArgs e)
		//{
		//	_isLoaded = false;
		//	await CloseAsync().ConfigureAwait(false);
		//}

		private void OnOwnBackButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task stop = StopAsync();
		}

		private async void OnAudioRecorder_UnrecoverableError(object sender, EventArgs e)
		{
			await StopRecordingAsync();
			NotifyOfFailure();
		}

		private async void OnMediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
		{
			await StopRecordingAsync();
			NotifyOfFailure();
		}

		private async Task StopRecordingAsync()
		{
			bool isOk = false;
			if (_audioRecorder != null) isOk = await _audioRecorder.RecordStopAsync();
			await RunInUiThreadAsync(delegate
			{
				//VM?.EndRecordAudio();
				RecordingStoryboard.SkipToFill();
				RecordingStoryboard.Stop();
			}).ConfigureAwait(false);
			if (!isOk)
			{
				NotifyOfFailure();
			}
		}

		private void StopAllAnimations()
		{
			Task stopAllAnims = RunInUiThreadAsync(delegate
			{
				RecordingStoryboard.SkipToFill();
				RecordingStoryboard.Stop();
				FailureStoryboard.SkipToFill();
				FailureStoryboard.Stop();
			});
		}

		private void NotifyOfFailure()
		{
			Task beginFailureAnim = RunInUiThreadAsync(delegate
			{
				FailureStoryboard.Begin();
				if (LastMessage == RuntimeData.GetText("AudioRecordingStarted") || LastMessage == RuntimeData.GetText("AudioRecordingStopped"))
				{
					LastMessage = RuntimeData.GetText("AudioRecordingInterrupted");
				}
			});
		}
	}
}

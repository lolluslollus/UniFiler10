using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UniFiler10.Data.Runtime;
using UniFiler10.ViewModels;
using Utilz;
using Utilz.Controlz;
using Windows.Media.Capture;
using Windows.Storage;
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
		private AudioRecorder _audioRecorder = null;
		private MediaCapture _mediaCapture;

		private readonly object _lastMessageLocker = new object();
		private volatile string _lastMessage = string.Empty;
		public string LastMessage { get { lock (_lastMessageLocker) { return _lastMessage; } } set { lock (_lastMessageLocker) { _lastMessage = value; } RaisePropertyChanged_UI(); } }

		// For listening to media property changes
		//private readonly SystemMediaTransportControls _systemMediaControls = SystemMediaTransportControls.GetForCurrentView();

		private SemaphoreSlimSafeRelease _recordLockingSemaphore = null;
		#endregion properties


		#region IRecorder
		private volatile bool _isRecording = false;
		public bool IsRecording { get { return _isRecording; } private set { _isRecording = value; RaisePropertyChanged_UI(); } }

		[STAThread]
		public Task<bool> RecordAsync(StorageFile file, CancellationToken cancToken)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (file == null || cancToken == null) return false;
				bool isOk = await StartRecordingAsync(file).ConfigureAwait(false);

				// Lock the thread asynchronously until explicitly closed from the caller. 
				// If an error prevented starting recording, stay open to display error messages.
				// The lock will last until the cancellation token is cancelled or the semaphore is released or disposed.
				_recordLockingSemaphore = new SemaphoreSlimSafeRelease(0, 1); // this semaphore is always closed at the beginning
				try
				{
					await _recordLockingSemaphore.WaitAsync(cancToken).ConfigureAwait(false);
					Debug.WriteLine("I am past _triggerSemaphore");
				}
				catch (OperationCanceledException)
				{
					isOk = false;
					Debug.WriteLine("I am past _triggerSemaphore after OperationCanceledException");
				}
				catch (Exception ex)
				{
					Debug.WriteLine("I am past _triggerSemaphore after exception: " + ex.ToString());
					if (SemaphoreSlimSafeRelease.IsAlive(_recordLockingSemaphore))
					{
						isOk = false;
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
					}
				}

				return isOk;
			});
		}
		#endregion IRecorder


		#region lifecycle
		public AudioRecorderView()
		{
			InitializeComponent();
		}

		[STAThread]
		protected override async Task CloseMayOverrideAsync()
		{
			await StopRecordingAsync().ConfigureAwait(false);

			SemaphoreSlimSafeRelease.TryDispose(_recordLockingSemaphore);
			_recordLockingSemaphore = null;

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

			Task stopAllAnims = RunInUiThreadAsync(delegate
			{
				RecordingStoryboard.SkipToFill();
				RecordingStoryboard.Stop();
				FailureStoryboard.SkipToFill();
				FailureStoryboard.Stop();
			});
		}
		#endregion lifecycle


		#region event handlers
		private async void OnStopRecordingButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			await StopRecordingAsync().ConfigureAwait(false);
			SemaphoreSlimSafeRelease.TryRelease(_recordLockingSemaphore);
		}

		private async void OnAudioRecorder_UnrecoverableError(object sender, EventArgs e)
		{
			await StopRecordingAsync().ConfigureAwait(false);
			NotifyOfFailure();
		}

		private async void OnMediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
		{
			await StopRecordingAsync().ConfigureAwait(false);
			NotifyOfFailure(errorEventArgs?.Message);
		}
		#endregion event handlers


		#region utilz
		private async Task<bool> StartRecordingAsync(StorageFile file)
		{
			bool isOk = false;

			Task stb = RunInUiThreadAsync(delegate
			{
				RecordingStoryboard.Begin();
			});

			try
			{
				_audioRecorder = new AudioRecorder(this, file);
				_audioRecorder.UnrecoverableError += OnAudioRecorder_UnrecoverableError;
				await _audioRecorder.OpenAsync();
				// adjust the microphone volume. You need MediaCapture, apparently, and she needs STAThread. Ridiculous.
				_mediaCapture = new MediaCapture();
				// LOLLO NOTE the following fails with the phone
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

				_isRecording = true;
				isOk = await _audioRecorder.StartRecordingAsync().ConfigureAwait(false);
				if (!isOk)
				{
					await Logger.AddAsync("AudioRecordingCannotStart", Logger.ForegroundLogFilename);
					NotifyOfFailure(RuntimeData.GetText("AudioRecordingCannotStart"));
				}
			}
			catch (Exception ex)
			{
				isOk = false;
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
				NotifyOfFailure(ex.Message);
			}

			return isOk;
		}

		private async Task StopRecordingAsync()
		{
			if (!_isRecording) return;

			bool isOk = false;

			var ar = _audioRecorder;
			if (ar != null)
			{
				_isRecording = false;
				isOk = await ar.StopRecordingAsync();
			}

			await RunInUiThreadAsync(delegate
			{
				RecordingStoryboard.SkipToFill();
				RecordingStoryboard.Stop();
			}).ConfigureAwait(false);

			if (!isOk) NotifyOfFailure();
		}

		private void NotifyOfFailure(string msg = "")
		{
			Task beginFailureAnim = RunInUiThreadAsync(delegate
			{
				FailureStoryboard.Begin();
				if (string.IsNullOrWhiteSpace(msg))
				{
					if (LastMessage == RuntimeData.GetText("AudioRecordingStarted") || LastMessage == RuntimeData.GetText("AudioRecordingStopped"))
					{
						LastMessage = RuntimeData.GetText("AudioRecordingInterrupted");
					}
				}
				else
				{
					LastMessage = msg;
				}
			});
		}
		#endregion utilz
	}
}
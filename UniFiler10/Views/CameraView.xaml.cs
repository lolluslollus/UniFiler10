using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.Data.Runtime;
using UniFiler10.ViewModels;
using Utilz;
using Windows.ApplicationModel;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UniFiler10.Views
{
	public sealed partial class CameraView : OpenableObservableControl, IRecorder
	{
		#region properties
		//public BinderContentVM VM
		//{
		//	get { return (BinderContentVM)GetValue(VMProperty); }
		//	set { SetValue(VMProperty, value); }
		//}
		//public static readonly DependencyProperty VMProperty =
		//	DependencyProperty.Register("VM", typeof(BinderContentVM), typeof(CameraView), new PropertyMetadata(null));

		private string _lastMessage = string.Empty;
		public string LastMessage { get { return _lastMessage; } set { _lastMessage = value; RaisePropertyChanged_UI(); } }

		// Receive notifications about rotation of the device and UI and apply any necessary rotation to the preview stream and UI controls       
		private readonly DisplayInformation _displayInformation = DisplayInformation.GetForCurrentView();
		private readonly SimpleOrientationSensor _orientationSensor = SimpleOrientationSensor.GetDefault();
		private SimpleOrientation _deviceOrientation = SimpleOrientation.NotRotated;
		private DisplayOrientations _displayOrientation = DisplayOrientations.Portrait;

		// Rotation metadata to apply to the preview stream and recorded videos (MF_MT_VIDEO_ROTATION)
		// Reference: http://msdn.microsoft.com/en-us/library/windows/apps/xaml/hh868174.aspx
		private static readonly Guid RotationKey = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");

		// Prevent the screen from sleeping while the camera is running
		private readonly DisplayRequest _displayRequest = new DisplayRequest();

		// For listening to media property changes
		private readonly SystemMediaTransportControls _systemMediaControls = SystemMediaTransportControls.GetForCurrentView();

		// MediaCapture and its state variables
		private MediaCapture _mediaCapture;
		private volatile bool _isInitialized;
		private volatile bool _isPreviewing;
		private bool? _isFlashDesired = false;
		public bool? IsFlashDesired { get { return _isFlashDesired; } set { _isFlashDesired = value; RaisePropertyChanged_UI(); } }
		//private bool _isRecordingVideo;

		// Information about the camera device
		private bool _isMirroringPreview;
		private bool _isExternalCamera;

		private StorageFile _file = null;
		private SemaphoreSlimSafeRelease _triggerSemaphore = null;
		#endregion properties


		#region IRecorder
		[STAThread] //LOLLO test
		public Task<bool> StartAsync(StorageFile file)
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				_file = file;
				await SetupUiAsync();
				bool isOk = await InitializeCameraAsync().ConfigureAwait(false);
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
				//else
				//{
				//	Task beginFailureAnim = RunInUiThreadAsync(delegate { FailureStoryboard.Begin(); });
				//}
				return isOk;
			});
		}
		[STAThread] //LOLLO test
		public Task<bool> StopAsync()
		{
			SemaphoreSlimSafeRelease.TryRelease(_triggerSemaphore);
			return RunFunctionWhileOpenAsyncTB(delegate
			{
				return Stop2Async();
			});
		}
		private async Task TakePhotoAndStopAsync()
		{
			SemaphoreSlimSafeRelease.TryRelease(_triggerSemaphore);
			await RunFunctionWhileOpenAsyncT(async delegate
			{
				await TakePhotoAsync();
				await Stop2Async();
			}).ConfigureAwait(false);
		}
		private async Task<bool> Stop2Async()
		{
			await CleanupCameraAsync();
			await CleanupUiAsync().ConfigureAwait(false);

			return true;
		}
		#endregion IRecorder


		#region Constructor, lifecycle and navigation
		public CameraView()
		{
			//Loaded += OnLoaded;
			//Unloaded += OnUnloaded;
			//Application.Current.Resuming += OnResuming;
			//Application.Current.Suspending += OnSuspending;

			InitializeComponent();
			//VideoButton.Visibility = Visibility.Collapsed;
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

		//private async void OnResuming(object sender, object e) // LOLLO TODO test OnSuspending and OnResuming
		//{
		//	if (_isLoadedWhenSuspending) await OpenAsync().ConfigureAwait(false);
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
		#endregion Constructor, lifecycle and navigation


		#region Event handlers
		/// <summary>
		/// In the event of the app being minimized this method handles media property change events. If the app receives a mute
		/// notification, it is no longer in the foregroud.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		//     private async void OnSystemMediaControls_PropertyChanged(SystemMediaTransportControls sender, SystemMediaTransportControlsPropertyChangedEventArgs args)
		//     {
		//         await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async delegate
		//         {
		//             // Only handle this event if this page is currently being displayed
		//             //if (args.Property == SystemMediaTransportControlsProperty.SoundLevel && Frame.CurrentSourcePageType == typeof(CameraPage))
		//             if (args.Property == SystemMediaTransportControlsProperty.SoundLevel)
		//             {
		//                 // Check to see if the app is being muted. If so, it is being minimized.
		//                 // Otherwise if it is not initialized, it is being brought into focus.
		//		// LOLLO take this away, we dont need it for stills
		//                 if (sender.SoundLevel == SoundLevel.Muted)
		//                 {
		//                     await CleanupCameraAsync(); // this will not be awaited, but it does not matter
		//                 }
		//                 else if (!_isInitialized)
		//                 {
		//                     await InitializeCameraAsync(); // this will not be awaited, but it does not matter
		//                 }
		//             }
		//         });
		//     }

		/// <summary>
		/// Occurs each time the simple orientation sensor reports a new sensor reading.
		/// </summary>
		/// <param name="sender">The event source.</param>
		/// <param name="args">The event data.</param>
		private void OnOrientationSensor_OrientationChanged(SimpleOrientationSensor sender, SimpleOrientationSensorOrientationChangedEventArgs args)
		{
			if (args.Orientation != SimpleOrientation.Faceup && args.Orientation != SimpleOrientation.Facedown)
			{
				// Only update the current orientation if the device is not parallel to the ground. This allows users to take pictures of documents (FaceUp)
				// or the ceiling (FaceDown) in portrait or landscape, by first holding the device in the desired orientation, and then pointing the camera
				// either up or down, at the desired subject.
				//Note: This assumes that the camera is either facing the same way as the screen, or the opposite way. For devices with cameras mounted
				//      on other panels, this logic should be adjusted.
				_deviceOrientation = args.Orientation;

				//await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateButtonOrientation());
			}
		}

		/// <summary>
		/// This event will fire when the page is rotated, when the DisplayInformation.AutoRotationPreferences value set in the SetupUiAsync() method cannot be not honored.
		/// </summary>
		/// <param name="sender">The event source.</param>
		/// <param name="args">The event data.</param>
		private async void OnDisplayInformation_OrientationChanged(DisplayInformation sender, object args)
		{
			_displayOrientation = sender.CurrentOrientation;

			if (_isPreviewing)
			{
				await SetPreviewRotationAsync();
			}

			//await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateButtonOrientation());
		}

		private void OnPhotoButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task stop = TakePhotoAndStopAsync();
		}

		private void OnIsFlashDesired_Tapped(object sender, TappedRoutedEventArgs e)
		{
			IsFlashDesired = !_isFlashDesired;
			UpdateFlash();
		}

		private void OnHardwareButtons_CameraPressed(object sender, CameraEventArgs e)
		{
			Task stop = TakePhotoAndStopAsync();
		}

		//private async void OnVideoButton_Tapped(object sender, TappedRoutedEventArgs e)
		//{
		//await RunFunctionWhileOpenAsyncT(async delegate
		//{
		//    if (!_isRecordingVideo)
		//    {
		//        await StartRecordingVideoAsync();
		//    }
		//    else
		//    {
		//        await StopRecordingVideoAsync();
		//    }

		//    // After starting or stopping video recording, update the UI to reflect the MediaCapture state
		//    UpdateCaptureControls();
		//}).ConfigureAwait(false);
		//}

		//private async void OnMediaCapture_RecordLimitationExceeded(MediaCapture sender)
		//{
		//    // This is a notification that recording has to stop, and the app is expected to finalize the recording

		//    //await StopRecordingVideoAsync();

		//    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateCaptureControls());
		//}

		private async void OnMediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
		{
			//LastMessage = string.Format("MediaCapture_Failed: (0x{0:X}) {1}", errorEventArgs.Code, errorEventArgs.Message);
			LastMessage = string.Format(RuntimeData.GetText("CameraCaptureFailedW2Params"), errorEventArgs.Code, errorEventArgs.Message);

			await CleanupCameraAsync();

			//await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateCaptureControls());
		}
		#endregion Event handlers


		#region MediaCapture methods
		/// <summary>
		/// Initializes the MediaCapture, registers events, gets camera device information for mirroring and rotating, starts preview and unlocks the UI
		/// </summary>
		/// <returns></returns>
		[STAThread]
		private async Task<bool> InitializeCameraAsync()
		{
			LastMessage = RuntimeData.GetText("CameraInitialising");

			if (_mediaCapture == null)
			{
				// Attempt to get the back camera if one is available, but use any camera device if not
				//var cameraDevice = await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Back);
				var cameraDevice = RuntimeData.Instance?.VideoDevice;
				if (cameraDevice == null)
				{
					LastMessage = RuntimeData.GetText("CameraDeviceNotFound");
					return false;
				}
				// LOLLO TODO test this
				var test = cameraDevice.Properties;
				var keys = test.Keys;

				// Create MediaCapture and its settings
				_mediaCapture = new MediaCapture();

				// Register for a notification when video recording has reached the maximum time and when something goes wrong
				//_mediaCapture.RecordLimitationExceeded += OnMediaCapture_RecordLimitationExceeded;
				_mediaCapture.Failed += OnMediaCapture_Failed;

				var settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id, MediaCategory = MediaCategory.Media, StreamingCaptureMode = StreamingCaptureMode.Video };

				// Initialize MediaCapture
				try
				{
					await _mediaCapture.InitializeAsync(settings);
					_isInitialized = true;
				}
				catch (UnauthorizedAccessException)
				{
					LastMessage = RuntimeData.GetText("CameraNoAccess");
					return false;
				}
				catch (Exception ex)
				{
					//                    LastMessage = string.Format("Exception when initializing MediaCapture with {0}: {1}", cameraDevice.Id, ex.ToString());
					LastMessage = string.Format(RuntimeData.GetText("CameraInitialiseExceptionW1Params"), cameraDevice.Id, ex.ToString());
					return false;
				}

				// If initialisation was ok, activate its button
				FlashButton.IsEnabled = _mediaCapture.VideoDeviceController.FlashControl.Supported;

				// If initialization succeeded, start the preview
				// Figure out where the camera is located
				if (cameraDevice.EnclosureLocation == null || cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Unknown)
				{
					// No information on the location of the camera, assume it's an external camera, not integrated on the device
					_isExternalCamera = true;
				}
				else
				{
					// Camera is fixed on the device
					_isExternalCamera = false;

					// Only mirror the preview if the camera is on the front panel
					_isMirroringPreview = (cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
				}

				UpdateFlash();

				LastMessage = string.Empty;
				await StartPreviewAsync();

				await UpdateCaptureControlsAsync();
			}
			return true;
		}

		/// <summary>
		/// Starts the preview and adjusts it for for rotation and mirroring after making a request to keep the screen on
		/// </summary>
		/// <returns></returns>
		private async Task StartPreviewAsync()
		{
			// Prevent the device from sleeping while the preview is running
			_displayRequest.RequestActive();

			// Set the preview source in the UI and mirror it if necessary
			PreviewControl.Source = _mediaCapture;
			PreviewControl.FlowDirection = _isMirroringPreview ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

			// Start the preview
			try
			{
				await _mediaCapture.StartPreviewAsync();
				_isPreviewing = true;
			}
			catch (Exception ex)
			{
				//LastMessage = string.Format("Exception when starting the preview: {0}", ex.ToString());
				LastMessage = string.Format(RuntimeData.GetText("CameraPreviewStartExceptionW1Params"), ex.ToString());
			}

			// Initialize the preview to the current orientation
			if (_isPreviewing)
			{
				await SetPreviewRotationAsync();
			}
		}

		/// <summary>
		/// Gets the current orientation of the UI in relation to the device (when AutoRotationPreferences cannot be honored) and applies a corrective rotation to the preview
		/// </summary>
		private async Task SetPreviewRotationAsync()
		{
			// Only need to update the orientation if the camera is mounted on the device
			if (_isExternalCamera) return;

			// Calculate which way and how far to rotate the preview
			int rotationDegrees = ConvertDisplayOrientationToDegrees(_displayOrientation);

			// The rotation direction needs to be inverted if the preview is being mirrored
			if (_isMirroringPreview)
			{
				rotationDegrees = (360 - rotationDegrees) % 360;
			}

			if (_mediaCapture != null)
			{
				// Add rotation metadata to the preview stream to make sure the aspect ratio / dimensions match when rendering and getting preview frames
				var props = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
				props.Properties.Add(RotationKey, rotationDegrees);
				await _mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);
			}
		}

		/// <summary>
		/// Stops the preview and deactivates a display request, to allow the screen to go into power saving modes
		/// </summary>
		/// <returns></returns>
		private async Task StopPreviewAsync()
		{
			// Stop the preview
			try
			{
				_isPreviewing = false;
				await _mediaCapture.StopPreviewAsync();
			}
			catch (Exception ex)
			{
				//LastMessage = string.Format("Exception when stopping the preview: {0}", ex.ToString());
				LastMessage = string.Format(RuntimeData.GetText("CameraPreviewStopExceptionWParams"), ex.ToString());
			}

			// Use the dispatcher because this method is sometimes called from non-UI threads
			await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				// Cleanup the UI
				PreviewControl.Source = null;
				try
				{
					// Allow the device screen to sleep now that the preview is stopped
					_displayRequest.RequestRelease();
				}
				catch { }
			});
		}

		/// <summary>
		/// Takes a photo to a StorageFile and adds rotation metadata to it
		/// </summary>
		/// <returns></returns>
		[STAThread]
		private async Task TakePhotoAsync()
		{
			// While taking a photo, keep the video button enabled only if the camera supports simultaneously taking pictures and recording video
			//VideoButton.IsEnabled = _mediaCapture.MediaCaptureSettings.ConcurrentRecordAndPhotoSupported;

			// Make the button invisible if it's disabled, so it's obvious it cannot be interacted with
			//VideoButton.Opacity = VideoButton.IsEnabled ? 1 : 0;

			using (var stream = new InMemoryRandomAccessStream())
			{
				var photoOrientation = PhotoOrientation.Normal;

				try
				{
					LastMessage = RuntimeData.GetText("CameraTakingPhoto");
					await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);

					// LOLLO TODO check this
					var test = ImageEncodingProperties.CreateJpeg();
					var keys = test.Properties.Keys;
					var testXR = ImageEncodingProperties.CreateJpegXR();
					var keysXR = testXR.Properties.Keys;

					LastMessage = RuntimeData.GetText("CameraTakenPhoto");

					photoOrientation = ConvertOrientationToPhotoOrientation(GetCameraOrientation());
				}
				catch (Exception ex)
				{
					//LastMessage = string.Format("Exception when taking a photo: {0}", ex.ToString());
					LastMessage = string.Format(RuntimeData.GetText("CameraTakePhotoExceptionW1Params"), ex.ToString());
				}
				finally
				{
					await ReencodeAndSavePhotoAsync(stream, photoOrientation).ConfigureAwait(false);
				}
			}

			// Done taking a photo, so re-enable the button
			//VideoButton.IsEnabled = true;
			//VideoButton.Opacity = 1;
		}

		/// <summary>
		/// Records an MP4 video to a StorageFile and adds rotation metadata to it
		/// </summary>
		/// <returns></returns>
		//private async Task StartRecordingVideoAsync()
		//{
		//    try
		//    {
		//        // Create storage file in Pictures Library
		//        var videoFile = await KnownFolders.PicturesLibrary.CreateFileAsync("SimpleVideo.mp4", CreationCollisionOption.GenerateUniqueName);

		//        var encodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);

		//        // Calculate rotation angle, taking mirroring into account if necessary
		//        var rotationAngle = 360 - ConvertDeviceOrientationToDegrees(GetCameraOrientation());
		//        encodingProfile.Video.Properties.Add(RotationKey, PropertyValue.CreateInt32(rotationAngle));

		//        LastMessage = "Starting recording...";

		//        await _mediaCapture.StartRecordToStorageFileAsync(encodingProfile, videoFile);
		//        _isRecordingVideo = true;

		//        LastMessage = "Started recording!";
		//    }
		//    catch (Exception ex)
		//    {
		//        LastMessage = string.Format("Exception when starting video recording: {0}", ex.ToString());
		//    }
		//}

		/// <summary>
		/// Stops recording a video
		/// </summary>
		/// <returns></returns>
		//private async Task StopRecordingVideoAsync()
		//{
		//    try
		//    {
		//        LastMessage = "Stopping recording...";

		//        _isRecordingVideo = false;
		//        await _mediaCapture.StopRecordAsync();

		//        LastMessage = "Stopped recording!";
		//    }
		//    catch (Exception ex)
		//    {
		//        LastMessage = string.Format("Exception when stopping video recording: {0}", ex.ToString());
		//    }
		//}

		/// <summary>
		/// Cleans up the camera resources (after stopping any video recording and/or preview if necessary) and unregisters from MediaCapture events
		/// </summary>
		/// <returns></returns>
		private async Task CleanupCameraAsync()
		{
			if (_isInitialized)
			{
				// If a recording is in progress during cleanup, stop it to save the recording
				//if (_isRecordingVideo)
				//{
				//    await StopRecordingVideoAsync();
				//}

				if (_isPreviewing)
				{
					// The call to stop the preview is included here for completeness, but can be
					// safely removed if a call to MediaCapture.Dispose() is being made later,
					// as the preview will be automatically stopped at that point
					await StopPreviewAsync();
					await UpdateCaptureControlsAsync();
				}

				_isInitialized = false;
			}

			if (_mediaCapture != null)
			{
				//_mediaCapture.RecordLimitationExceeded -= OnMediaCapture_RecordLimitationExceeded;
				_mediaCapture.Failed -= OnMediaCapture_Failed;
				try
				{
					_mediaCapture.Dispose();
				}
				catch { }
				_mediaCapture = null;
			}
		}
		#endregion MediaCapture methods


		#region Helper functions
		/// <summary>
		/// Attempts to lock the page orientation, hide the StatusBar (on Phone) and registers event handlers for hardware buttons and orientation sensors
		/// </summary>
		/// <returns></returns>
		private async Task SetupUiAsync()
		{
			// Attempt to lock page to landscape orientation to prevent the CaptureElement from rotating, as this gives a better experience
			DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;

			// Hide the status bar
			if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
			{
				await Windows.UI.ViewManagement.StatusBar.GetForCurrentView().HideAsync();
			}

			// Populate orientation variables with the current state
			_displayOrientation = _displayInformation.CurrentOrientation;
			if (_orientationSensor != null)
			{
				_deviceOrientation = _orientationSensor.GetCurrentOrientation();
			}

			RegisterEventHandlers();
		}

		/// <summary>
		/// Unregisters event handlers for hardware buttons and orientation sensors, allows the StatusBar (on Phone) to show, and removes the page orientation lock
		/// </summary>
		/// <returns></returns>
		private async Task CleanupUiAsync()
		{
			await RunInUiThreadAsync(delegate
			{
				UnregisterEventHandlers();
			});
			// Show the status bar
			if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
			{
				await Windows.UI.ViewManagement.StatusBar.GetForCurrentView().ShowAsync();
			}

			// Revert orientation preferences
			DisplayInformation.AutoRotationPreferences = DisplayOrientations.None;
		}

		/// <summary>
		/// This method will update the icons, enable/disable and show/hide the photo/video buttons depending on the current state of the app and the capabilities of the device
		/// </summary>
		private Task UpdateCaptureControlsAsync()
		{
			return RunInUiThreadAsync(delegate
			{
				// The buttons should only be enabled if the preview started sucessfully
				PhotoButton.IsEnabled = _isPreviewing;
				//VideoButton.IsEnabled = false; // _isPreviewing;

				// Update recording button to show "Stop" icon instead of red "Record" icon
				//StartRecordingIcon.Visibility = _isRecordingVideo ? Visibility.Collapsed : Visibility.Visible;
				//StopRecordingIcon.Visibility = _isRecordingVideo ? Visibility.Visible : Visibility.Collapsed;

				// If the camera doesn't support simultaneosly taking pictures and recording video, disable the photo button on record
				//    if (_isInitialized && !_mediaCapture.MediaCaptureSettings.ConcurrentRecordAndPhotoSupported)
				//    {
				//        PhotoButton.IsEnabled = !_isRecordingVideo;

				//        // Make the button invisible if it's disabled, so it's obvious it cannot be interacted with
				//        PhotoButton.Opacity = PhotoButton.IsEnabled ? 1 : 0;
				//    }
			});
		}

		private void UpdateFlash()
		{
			if (_mediaCapture == null) return;
			if (_mediaCapture.VideoDeviceController.FlashControl.Supported && _isFlashDesired == true)
			{
				_mediaCapture.VideoDeviceController.FlashControl.Auto = true;
				_mediaCapture.VideoDeviceController.FlashControl.Enabled = true;
			}
			else
			{
				_mediaCapture.VideoDeviceController.FlashControl.Enabled = false;
			}
		}
		/// <summary>
		/// Registers event handlers for hardware buttons and orientation sensors, and performs an initial update of the UI rotation
		/// </summary>
		private void RegisterEventHandlers()
		{
			if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
			{
				HardwareButtons.CameraPressed += OnHardwareButtons_CameraPressed;
			}
			// If there is an orientation sensor present on the device, register for notifications
			if (_orientationSensor != null)
			{
				_orientationSensor.OrientationChanged += OnOrientationSensor_OrientationChanged;

				// Update orientation of buttons with the current orientation
				//UpdateButtonOrientation();
			}

			_displayInformation.OrientationChanged += OnDisplayInformation_OrientationChanged;
			//_systemMediaControls.PropertyChanged += OnSystemMediaControls_PropertyChanged;
		}

		/// <summary>
		/// Unregisters event handlers for hardware buttons and orientation sensors
		/// </summary>
		private void UnregisterEventHandlers()
		{
			if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
			{
				HardwareButtons.CameraPressed -= OnHardwareButtons_CameraPressed;
			}

			if (_orientationSensor != null)
			{
				_orientationSensor.OrientationChanged -= OnOrientationSensor_OrientationChanged;
			}

			_displayInformation.OrientationChanged -= OnDisplayInformation_OrientationChanged;
			//_systemMediaControls.PropertyChanged -= OnSystemMediaControls_PropertyChanged;
		}

		///// <summary>
		///// Attempts to find and return a device mounted on the panel specified, and on failure to find one it will return the first device listed
		///// </summary>
		///// <param name="desiredPanel">The desired panel on which the returned device should be mounted, if available</param>
		///// <returns></returns>
		//public static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
		//{
		//    // Get available devices for capturing pictures
		//    var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

		//    // Get the desired camera by panel
		//    DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

		//    // If there is no device mounted on the desired panel, return the first device found
		//    return desiredDevice ?? allVideoDevices.FirstOrDefault();
		//}

		/// <summary>
		/// Applies the given orientation to a photo stream and saves it as a StorageFile
		/// </summary>
		/// <param name="stream">The photo stream</param>
		/// <param name="photoOrientation">The orientation metadata to apply to the photo</param>
		/// <returns></returns>
		private async Task ReencodeAndSavePhotoAsync(IRandomAccessStream inputStream, PhotoOrientation photoOrientation)
		{
			try
			{
				var decoder = await BitmapDecoder.CreateAsync(inputStream);

				//StorageFile file = await GetFileAsync();

				using (var outputStream = await _file.OpenAsync(FileAccessMode.ReadWrite))
				{
					var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);

					var bitmapProperties = new BitmapPropertySet { { "System.Photo.Orientation", new BitmapTypedValue(photoOrientation, PropertyType.UInt16) } };

					await encoder.BitmapProperties.SetPropertiesAsync(bitmapProperties);
					await encoder.FlushAsync();
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
				Debugger.Break();
			}
			finally
			{
				//VM?.ForceEndShootAsync();
			}
		}
		///// <summary>
		///// If I take multiple photos, I need multiple files
		///// </summary>
		///// <returns></returns>
		//private async Task<StorageFile> GetFileAsync()
		//{
		//	StorageFile result = null;
		//	if (_file == null) return result;

		//	BasicProperties fileProperties = await _file.GetBasicPropertiesAsync().AsTask().ConfigureAwait(false);
		//	if (fileProperties?.Size > 0)
		//	{
		//		var dir = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(_file.Path));
		//		if (dir != null)
		//		{
		//			result = await dir.CreateFileAsync(_file.Name, CreationCollisionOption.GenerateUniqueName);
		//		}
		//	}
		//	else if (fileProperties?.Size == 0)
		//	{
		//		result = _file;
		//	}
		//	return result;
		//}
		#endregion Helper functions


		#region Rotation helpers
		/// <summary>
		/// Calculates the current camera orientation from the device orientation by taking into account whether the camera is external or facing the user
		/// </summary>
		/// <returns>The camera orientation in space, with an inverted rotation in the case the camera is mounted on the device and is facing the user</returns>
		private SimpleOrientation GetCameraOrientation()
		{
			if (_isExternalCamera)
			{
				// Cameras that are not attached to the device do not rotate along with it, so apply no rotation
				return SimpleOrientation.NotRotated;
			}

			var result = _deviceOrientation;

			// Account for the fact that, on portrait-first devices, the camera sensor is mounted at a 90 degree offset to the native orientation
			if (_displayInformation.NativeOrientation == DisplayOrientations.Portrait)
			{
				switch (result)
				{
					case SimpleOrientation.Rotated90DegreesCounterclockwise:
						result = SimpleOrientation.NotRotated;
						break;
					case SimpleOrientation.Rotated180DegreesCounterclockwise:
						result = SimpleOrientation.Rotated90DegreesCounterclockwise;
						break;
					case SimpleOrientation.Rotated270DegreesCounterclockwise:
						result = SimpleOrientation.Rotated180DegreesCounterclockwise;
						break;
					case SimpleOrientation.NotRotated:
						result = SimpleOrientation.Rotated270DegreesCounterclockwise;
						break;
				}
			}

			// If the preview is being mirrored for a front-facing camera, then the rotation should be inverted
			if (_isMirroringPreview)
			{
				// This only affects the 90 and 270 degree cases, because rotating 0 and 180 degrees is the same clockwise and counter-clockwise
				switch (result)
				{
					case SimpleOrientation.Rotated90DegreesCounterclockwise:
						return SimpleOrientation.Rotated270DegreesCounterclockwise;
					case SimpleOrientation.Rotated270DegreesCounterclockwise:
						return SimpleOrientation.Rotated90DegreesCounterclockwise;
				}
			}

			return result;
		}

		/// <summary>
		/// Converts the given orientation of the device in space to the corresponding rotation in degrees
		/// </summary>
		/// <param name="orientation">The orientation of the device in space</param>
		/// <returns>An orientation in degrees</returns>
		private static int ConvertDeviceOrientationToDegrees(SimpleOrientation orientation)
		{
			switch (orientation)
			{
				case SimpleOrientation.Rotated90DegreesCounterclockwise:
					return 90;
				case SimpleOrientation.Rotated180DegreesCounterclockwise:
					return 180;
				case SimpleOrientation.Rotated270DegreesCounterclockwise:
					return 270;
				case SimpleOrientation.NotRotated:
				default:
					return 0;
			}
		}

		/// <summary>
		/// Converts the given orientation of the app on the screen to the corresponding rotation in degrees
		/// </summary>
		/// <param name="orientation">The orientation of the app on the screen</param>
		/// <returns>An orientation in degrees</returns>
		private static int ConvertDisplayOrientationToDegrees(DisplayOrientations orientation)
		{
			switch (orientation)
			{
				case DisplayOrientations.Portrait:
					return 90;
				case DisplayOrientations.LandscapeFlipped:
					return 180;
				case DisplayOrientations.PortraitFlipped:
					return 270;
				case DisplayOrientations.Landscape:
				default:
					return 0;
			}
		}

		/// <summary>
		/// Converts the given orientation of the device in space to the metadata that can be added to captured photos
		/// </summary>
		/// <param name="orientation">The orientation of the device in space</param>
		/// <returns></returns>
		private static PhotoOrientation ConvertOrientationToPhotoOrientation(SimpleOrientation orientation)
		{
			switch (orientation)
			{
				case SimpleOrientation.Rotated90DegreesCounterclockwise:
					return PhotoOrientation.Rotate90;
				case SimpleOrientation.Rotated180DegreesCounterclockwise:
					return PhotoOrientation.Rotate180;
				case SimpleOrientation.Rotated270DegreesCounterclockwise:
					return PhotoOrientation.Rotate270;
				case SimpleOrientation.NotRotated:
				default:
					return PhotoOrientation.Normal;
			}
		}

		/// <summary>
		/// Uses the current device orientation in space and page orientation on the screen to calculate the rotation
		/// transformation to apply to the controls
		/// </summary>
		//private void UpdateButtonOrientation()
		//{
		//	int device = ConvertDeviceOrientationToDegrees(_deviceOrientation);
		//	int display = ConvertDisplayOrientationToDegrees(_displayOrientation);

		//	if (_displayInformation.NativeOrientation == DisplayOrientations.Portrait)
		//	{
		//		device -= 90;
		//	}

		//	// Combine both rotations and make sure that 0 <= result < 360
		//	var angle = (360 + display + device) % 360;

		//	// Rotate the buttons in the UI to match the rotation of the device
		//	var transform = new RotateTransform { Angle = angle };

		//	// The RenderTransform is safe to use (i.e. it won't cause layout issues) in this case, because these buttons have a 1:1 aspect ratio
		//	PhotoButton.RenderTransform = transform;
		//	//VideoButton.RenderTransform = transform;
		//}
		#endregion Rotation helpers
	}
}

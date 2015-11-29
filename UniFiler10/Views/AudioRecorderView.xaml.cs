using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Services;
using UniFiler10.ViewModels;
using Utilz;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Windows.Phone.UI.Input;
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
	public sealed partial class AudioRecorderView : OpenableObservableControl, IMessageWriter
	{
		private static SemaphoreSlimSafeRelease _vmSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		public BinderContentVM VM
		{
			get { return (BinderContentVM)GetValue(VMProperty); }
			set { SetValue(VMProperty, value); }
		}
		public static readonly DependencyProperty VMProperty =
			DependencyProperty.Register("VM", typeof(BinderContentVM), typeof(AudioRecorderView), new PropertyMetadata(null/*, OnVMChanged*/));
		///// <summary>
		///// LOLLO VM may not be available yet when OnLoaded fires, it is required though, hence the complexity
		///// </summary>
		///// <param name="obj"></param>
		///// <param name="args"></param>
		//private static async void OnVMChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		//{
		//	try
		//	{
		//		await _vmSemaphore.WaitAsync(); //.ConfigureAwait(false);

		//		var instance = obj as AudioRecorderView;
		//		if (instance.IsOpen)
		//		{
		//			if (instance != null && args.NewValue is BinderContentVM && args.NewValue != args.OldValue)
		//			{
		//				await instance.CloseAsync().ConfigureAwait(false);
		//				await instance.TryOpenAsync().ConfigureAwait(false);
		//			}
		//			else if (instance != null && args.NewValue == null)
		//			{
		//				await instance.CloseAsync().ConfigureAwait(false);
		//			}
		//		}
		//	}
		//	finally
		//	{
		//		SemaphoreSlimSafeRelease.TryRelease(_vmSemaphore);
		//	}
		//}


		private AudioRecorder _audioRecorder = null;

		private string _lastMessage = string.Empty;
		public string LastMessage { get { return _lastMessage; } set { _lastMessage = value; RaisePropertyChanged_UI(); } }

		// Prevent the screen from sleeping while the camera is running
		//private readonly DisplayRequest _displayRequest = new DisplayRequest();

		// For listening to media property changes
		//private readonly SystemMediaTransportControls _systemMediaControls = SystemMediaTransportControls.GetForCurrentView();

		public AudioRecorderView()
		{
			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
			Application.Current.Resuming += OnResuming;
			Application.Current.Suspending += OnSuspending;

			InitializeComponent();
		}

		private bool _isLoaded = false;
		private bool _isLoadedWhenSuspending = false;
		private async void OnSuspending(object sender, SuspendingEventArgs e)
		{
			var deferral = e.SuspendingOperation.GetDeferral();

			_isLoadedWhenSuspending = _isLoaded;
			await CloseAsync().ConfigureAwait(false);

			deferral.Complete();
		}

		private async void OnResuming(object sender, object e)
		{
			if (_isLoadedWhenSuspending) await OpenAsync().ConfigureAwait(false);
		}

		private async void OnLoaded(object sender, RoutedEventArgs e)
		{
			_isLoaded = true;
			await OpenAsync().ConfigureAwait(false);
		}

		private async void OnUnloaded(object sender, RoutedEventArgs e)
		{
			_isLoaded = false;
			await CloseAsync().ConfigureAwait(false);
		}

		private async void OnOwnBackButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			await CloseAsync().ConfigureAwait(false);
		}

		protected override async Task OpenMayOverrideAsync()
		{
			if (VM != null)
			{
				RecordingStoryboard.Begin();
				_audioRecorder = new AudioRecorder(this, VM);
				await _audioRecorder.OpenAsync();
				await _audioRecorder.RecordStartAsync().ConfigureAwait(false);
			}
		}
		protected override async Task CloseMayOverrideAsync()
		{
			await StopRecordingAsync().ConfigureAwait(false);

			var audioRecorder = _audioRecorder;
			if (audioRecorder != null)
			{
				await audioRecorder.CloseAsync();
				audioRecorder.Dispose();
			}
			_audioRecorder = null;
		}

		private async Task StopRecordingAsync()
		{
			if (_audioRecorder != null) await _audioRecorder.RecordStopAsync();
			VM?.EndRecordAudio();
			RecordingStoryboard.SkipToFill();
			RecordingStoryboard.Stop();
		}
	}
}

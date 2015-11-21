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
	public sealed partial class AudioRecorderView : BackableOpenableObservableControl, IMessageWriter
	{
		private static SemaphoreSlimSafeRelease _vmSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		public BinderVM VM
		{
			get { return (BinderVM)GetValue(VMProperty); }
			set { SetValue(VMProperty, value); }
		}
		public static readonly DependencyProperty VMProperty =
			DependencyProperty.Register("VM", typeof(BinderVM), typeof(AudioRecorderView), new PropertyMetadata(null, OnVMChanged));
		/// <summary>
		/// LOLLO VM may not be available yet when OnLoaded fires, it is required though, hence the complexity
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="args"></param>
		private static async void OnVMChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			try
			{
				await _vmSemaphore.WaitAsync().ConfigureAwait(false);

				var instance = obj as AudioRecorderView;
				//if (instance != null && instance._isLoaded && args.NewValue is BinderVM && args.NewValue != args.OldValue)

				if (instance != null && args.NewValue is BinderVM && args.NewValue != args.OldValue)
				{
					await instance.CloseAsync().ConfigureAwait(false);
					await instance.TryOpenAsync().ConfigureAwait(false);
				}
				else if (instance != null && args.NewValue == null)
				{
					await instance.CloseAsync().ConfigureAwait(false);
				}

				//if (instance != null && instance.IsOpen)
				//{
				//	if (args.NewValue is BinderVM && args.NewValue != args.OldValue)
				//	{
				//		await instance.CloseAsync().ConfigureAwait(false);
				//		await instance.TryOpenAsync().ConfigureAwait(false);
				//	}
				//	else if (args.NewValue == null)
				//	{
				//		await instance.CloseAsync().ConfigureAwait(false);
				//	}
				//}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_vmSemaphore);
			}
		}


		private AudioRecorder _audioRecorder = null;

		private string _lastMessage = string.Empty;
		public string LastMessage { get { return _lastMessage; } set { _lastMessage = value; RaisePropertyChanged_UI(); } }

		// Prevent the screen from sleeping while the camera is running
		//private readonly DisplayRequest _displayRequest = new DisplayRequest();

		// For listening to media property changes
		//private readonly SystemMediaTransportControls _systemMediaControls = SystemMediaTransportControls.GetForCurrentView();

		public AudioRecorderView()
		{
			OpenCloseWhenLoadedUnloaded = true;
			IsEnabled = false;
			InitializeComponent();
		}

		protected override async Task<bool> OpenMayOverrideAsync()
		{
			if (VM != null)
			{
				_audioRecorder = new AudioRecorder(this, VM);
				await _audioRecorder.OpenAsync();

				await _audioRecorder.RecordStartAsync().ConfigureAwait(false);

				RegisterBackEventHandlers();
				return true;
			}
			return false;
		}
		protected override async Task CloseMayOverrideAsync()
		{
			UnregisterBackEventHandlers();

			await StopRecordingAsync().ConfigureAwait(false);

			_audioRecorder?.Dispose();
			_audioRecorder = null;
		}

		private async Task StopRecordingAsync()
		{
			if (_audioRecorder != null) await _audioRecorder.RecordStopAsync();
			VM?.EndRecordAudio();
		}
		protected override void GoBackMustOverride()
		{
			var vm = VM; if (vm == null) return;
			vm.IsAudioRecorderOverlayOpen = false;
		}
	}
}

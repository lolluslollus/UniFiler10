﻿using System;
using System.Linq;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using Utilz.Data;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Enumeration;
using Windows.Networking.Connectivity;

namespace UniFiler10.Data.Runtime
{
	public sealed class RuntimeData : OpenableObservableData
	{
		#region properties
		private volatile bool _isTrial = true;
		public bool IsTrial { get { return _isTrial; } set { _isTrial = value; RaisePropertyChanged_UI(); } }

		private volatile int _trialResidualDays = -1;
		public int TrialResidualDays { get { return _trialResidualDays; } set { _trialResidualDays = value; RaisePropertyChanged_UI(); } }

		private volatile bool _isConnectionAvailable = false;
		public bool IsConnectionAvailable
		{
			get { return _isConnectionAvailable; }
			private set
			{
				if (_isConnectionAvailable != value)
				{
					_isConnectionAvailable = value;
					RaisePropertyChanged_UI();
				}
			}
		}
		private void UpdateIsConnectionAvailable()
		{
			var profile = NetworkInformation.GetInternetConnectionProfile();
			if (profile == null)
			{
				IsConnectionAvailable = false;
			}
			else
			{
				var level = profile.GetNetworkConnectivityLevel();
				if (level == NetworkConnectivityLevel.InternetAccess || level == NetworkConnectivityLevel.LocalAccess)
				{
					//if (_briefcase.IsOpen)
					//{
					if (
						_briefcase.IsAllowMeteredConnection
						||
						NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost()?.NetworkCostType == NetworkCostType.Unrestricted
						)
					{
						IsConnectionAvailable = true;
					}
					//}
					//else
					//{
					//	IsConnectionAvailable = false;
					//}
				}
				else
				{
					IsConnectionAvailable = false;
				}
			}
		}

		private volatile bool _isCameraAvailable = false;
		public bool IsCameraAvailable
		{
			get { return _isCameraAvailable; }
			private set
			{
				if (_isCameraAvailable != value)
				{
					_isCameraAvailable = value;
					RaisePropertyChanged_UI();
				}
			}
		}

		private async Task UpdateIsCameraAvailableAsync()
		{
			_videoDevice = await FindCameraDeviceByPanelAsync(Panel.Back).ConfigureAwait(false);
			IsCameraAvailable = _videoDevice?.IsEnabled == true;
		}

		private volatile DeviceInformation _videoDevice = null;
		public DeviceInformation VideoDevice { get { return _videoDevice; } }

		private volatile bool _isMicrophoneAvailable = false;
		public bool IsMicrophoneAvailable
		{
			get { return _isMicrophoneAvailable; }
			private set
			{
				if (_isMicrophoneAvailable != value)
				{
					_isMicrophoneAvailable = value;
					RaisePropertyChanged_UI();
				}
			}
		}

		private async Task UpdateIsMicrophoneAvailableAsync()
		{
			_audioDevice = await FindMicrophoneDeviceByPanelAsync(Panel.Back).ConfigureAwait(false);
			IsMicrophoneAvailable = _audioDevice?.IsEnabled == true;
		}

		private volatile DeviceInformation _audioDevice = null;
		public DeviceInformation AudioDevice { get { return _audioDevice; } }

		private static readonly ResourceLoader _resourceLoader = new ResourceLoader();
		/// <summary>
		/// Gets a text from the resources, but not in the complex form such as "Resources/NewFieldValue/Text"
		/// For that, you need Windows.ApplicationModel.Resources.Core.ResourceManager.Current.MainResourceMap.GetValue("Resources/NewFieldValue/Text", ResourceContext.GetForCurrentView()).ValueAsString;
		/// However, that must be called from a view, and this class is not.
		/// </summary>
		/// <param name="resourceName"></param>
		/// <returns></returns>
		public static string GetText(string resourceName)
		{
			// localization localisation globalization globalisation
			string name = string.Empty;
			if (!string.IsNullOrWhiteSpace(resourceName)) name = _resourceLoader.GetString(resourceName);
			return name ?? string.Empty;
		}
		#endregion properties

		#region construct lifecycle
		private static RuntimeData _instance = null;
		public static RuntimeData Instance
		{
			get
			{
				lock (_instanceLocker)
				{
					return _instance;
				}
			}
		}

		private static readonly object _instanceLocker = new object();
		public static RuntimeData GetInstance(Briefcase briefcase, bool isLight)
		{
			lock (_instanceLocker)
			{
				if (_instance == null /*|| _instance._isDisposed*/ || _instance._isLight != isLight)
				{
					_instance = new RuntimeData(briefcase, isLight);
				}
				return _instance;
			}
		}

		private static Briefcase _briefcase = null;
		private readonly bool _isLight = false;

		private RuntimeData(Briefcase briefcase, bool isLight)
		{
			_briefcase = briefcase;
			_isLight = isLight;
			if (isLight) return;


			_videoDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.VideoCapture);
			_audioDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.AudioCapture);
		}
		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			AddHandlers();
			UpdateIsConnectionAvailable();
			if (_isLight) return;

			_videoDeviceWatcher.Start();
			_audioDeviceWatcher.Start();
			await UpdateIsCameraAvailableAsync().ConfigureAwait(false);
			await UpdateIsMicrophoneAvailableAsync().ConfigureAwait(false);
		}
		protected override Task CloseMayOverrideAsync()
		{
			RemoveHandlers();
			_videoDeviceWatcher.Stop();
			_audioDeviceWatcher.Stop();
			return Task.CompletedTask;
		}
		#endregion lifecycle

		#region event handlers
		private volatile bool _isHandlersActive = false;
		private static DeviceWatcher _videoDeviceWatcher = null;
		private static DeviceWatcher _audioDeviceWatcher = null;

		private void AddHandlers()
		{
			if (!_isHandlersActive)
			{
				_isHandlersActive = true;
				NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
				_briefcase.PropertyChanged += OnPersistentData_PropertyChanged;

				if (_isLight) return;

				_videoDeviceWatcher.EnumerationCompleted += OnVideoDeviceWatcher_EnumerationCompleted;
				_videoDeviceWatcher.Added += OnVideoDeviceWatcher_Added;
				_videoDeviceWatcher.Updated += OnVideoDeviceWatcher_Updated;
				_videoDeviceWatcher.Removed += OnVideoDeviceWatcher_Removed;

				_audioDeviceWatcher.EnumerationCompleted += OnAudioDeviceWatcher_EnumerationCompleted;
				_audioDeviceWatcher.Added += OnAudioDeviceWatcher_Added;
				_audioDeviceWatcher.Updated += OnAudioDeviceWatcher_Updated;
				_audioDeviceWatcher.Removed += OnAudioDeviceWatcher_Removed;
			}
		}

		private void RemoveHandlers()
		{
			NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
			_briefcase.PropertyChanged -= OnPersistentData_PropertyChanged;

			_videoDeviceWatcher.EnumerationCompleted -= OnVideoDeviceWatcher_EnumerationCompleted;
			_videoDeviceWatcher.Added -= OnVideoDeviceWatcher_Added;
			_videoDeviceWatcher.Updated -= OnVideoDeviceWatcher_Updated;
			_videoDeviceWatcher.Removed -= OnVideoDeviceWatcher_Removed;

			_audioDeviceWatcher.EnumerationCompleted -= OnAudioDeviceWatcher_EnumerationCompleted;
			_audioDeviceWatcher.Added -= OnAudioDeviceWatcher_Added;
			_audioDeviceWatcher.Updated -= OnAudioDeviceWatcher_Updated;
			_audioDeviceWatcher.Removed -= OnAudioDeviceWatcher_Removed;

			_isHandlersActive = false;
		}

		private void OnNetworkStatusChanged(object sender)
		{
			UpdateIsConnectionAvailable();
		}
		private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Briefcase.IsAllowMeteredConnection)
				|| (e.PropertyName == nameof(Briefcase.IsOpen) && _briefcase.IsOpen))
				UpdateIsConnectionAvailable();
		}

		private async void OnAudioDeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
		{
			await UpdateIsMicrophoneAvailableAsync().ConfigureAwait(false);
		}
		private async void OnAudioDeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
		{
			await UpdateIsMicrophoneAvailableAsync().ConfigureAwait(false);
		}
		private async void OnAudioDeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
		{
			await UpdateIsMicrophoneAvailableAsync().ConfigureAwait(false);
		}
		private async void OnAudioDeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
		{
			await UpdateIsMicrophoneAvailableAsync().ConfigureAwait(false);
		}

		private async void OnVideoDeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
		{
			await UpdateIsCameraAvailableAsync().ConfigureAwait(false);
		}
		private async void OnVideoDeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
		{
			await UpdateIsCameraAvailableAsync().ConfigureAwait(false);
		}
		private async void OnVideoDeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
		{
			await UpdateIsCameraAvailableAsync().ConfigureAwait(false);
		}
		private async void OnVideoDeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
		{
			await UpdateIsCameraAvailableAsync().ConfigureAwait(false);
		}

		#endregion event handlers

		#region helpers
		private static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Panel desiredPanel)
		{
			// Get available devices for capturing pictures
			var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

			// Get the desired camera by panel
			DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

			// If there is no device mounted on the desired panel, return the first device found
			return desiredDevice ?? allVideoDevices.FirstOrDefault();
		}
		private static async Task<DeviceInformation> FindMicrophoneDeviceByPanelAsync(Panel desiredPanel)
		{
			// Get available devices for capturing pictures
			var allAudioDevices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);

			// Get the desired camera by panel
			DeviceInformation desiredDevice = allAudioDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

			// If there is no device mounted on the desired panel, return the first device found
			return desiredDevice ?? allAudioDevices.FirstOrDefault();
		}
		#endregion helpers
	}
}

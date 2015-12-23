using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Model;
using UniFiler10.Data.Runtime;
using Utilz;
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Storage;

namespace UniFiler10.ViewModels
{
	public class FolderVM : OpenableObservableData
	{
		#region properties
		// public const string DEFAULT_AUDIO_FILE_NAME = "Audio.mp3"; // LOLLO TODO this fails with the phone, wav is good
		public const string DEFAULT_AUDIO_FILE_NAME = "Audio.wav";
		public const string DEFAULT_PHOTO_FILE_NAME = "Photo.jpg";

		private IRecorder _audioRecorder = null;
		//private IRecorder _camera = null;
		private Folder _folder = null;
		public Folder Folder { get { return _folder; } }

		private RuntimeData _runtimeData = null;
		public RuntimeData RuntimeData { get { return _runtimeData; } private set { _runtimeData = value; RaisePropertyChanged_UI(); } }

		//private bool _isCameraOverlayOpen = false;
		//public bool IsCameraOverlayOpen
		//{
		//	get { return _isCameraOverlayOpen; }
		//	set { _isCameraOverlayOpen = value; RaisePropertyChanged_UI(); }
		//}
		private bool _isAudioRecorderOverlayOpen = false;
		public bool IsAudioRecorderOverlayOpen
		{
			get { return _isAudioRecorderOverlayOpen; }
			set { _isAudioRecorderOverlayOpen = value; RaisePropertyChanged_UI(); }
		}
		#endregion properties


		#region ctor and dispose
		public FolderVM(Folder folder, IRecorder audioRecorder/*, IRecorder camera*/)
		{
			_folder = folder;
			_audioRecorder = audioRecorder;
			//_camera = camera;
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_folder = null; // do not dispose it, only briefcase may do so.
			_folderCategorySelector?.Dispose();
			_folderCategorySelector = null;
		}
		#endregion ctor and dispose


		#region open close
		protected override async Task OpenMayOverrideAsync()
		{
			RuntimeData = RuntimeData.Instance;
			UpdateCurrentFolderCategories();

			await ResumeAfterShootingAsync().ConfigureAwait(false);
			//return Task.CompletedTask;
		}

		/// <summary>
		/// I need this override to stop any running media recording
		/// </summary>
		/// <returns></returns>
		public override async Task<bool> CloseAsync()
		{
			if (!_isOpen) return false;

			await Logger.AddAsync("FolderVM closing", Logger.ForegroundLogFilename, Logger.Severity.Info).ConfigureAwait(false);

			var ar = _audioRecorder;
			if (ar != null)
			{
				await ar.CloseAsync().ConfigureAwait(false);
			}
			IsAudioRecorderOverlayOpen = false;

			//var cam = _camera;
			//if (cam != null)
			//{
			//	await cam.CloseAsync().ConfigureAwait(false);
			//}
			//IsCameraOverlayOpen = false;


			return await base.CloseAsync().ConfigureAwait(false);
		}
		#endregion open close


		#region user actions
		public Task<bool> TrySetFieldValueAsync(DynamicField dynFld, string newValue)
		{
			return RunFunctionWhileOpenAsyncTB(delegate
			{
				return dynFld?.TrySetFieldValueAsync(newValue);
			});
		}
		public Task<bool> RemoveWalletFromFolderAsync(Wallet wallet)
		{
			return RunFunctionWhileOpenAsyncTB(delegate
			{
				return _folder?.RemoveWalletAsync(wallet);
			});
		}
		public Task<bool> RemoveDocumentFromWalletAsync(Wallet wallet, Document doc)
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				if (wallet != null)
				{
					await wallet.OpenAsync();
					return await wallet.RemoveDocumentAsync(doc);
				}
				return false;
			});
		}
		#endregion user actions


		#region add media
		public async Task LoadMediaFileAsync() // LOLLO TODO file open picker causes a suspend on the phone, so the app quits before the file is saved
		{
			await RunFunctionWhileOpenAsyncT(async delegate
			{
				var folder = _folder;
				if (folder != null && folder.IsOpen)
				{
					var file = await DocumentExtensions.PickMediaFileAsync();
					await folder.ImportMediaFileIntoNewWalletAsync(file, true).ConfigureAwait(false);
				}
			});
		}
		public async Task LoadMediaFileAsync(Wallet parentWallet) // LOLLO TODO file open picker causes a suspend on the phone, so the app quits before the file is saved
		{
			await RunFunctionWhileOpenAsyncT(async delegate
			{
				var folder = _folder;
				if (folder != null && folder.IsOpen && parentWallet != null)
				{
					var file = await DocumentExtensions.PickMediaFileAsync();
					await parentWallet.ImportMediaFileAsync(file, true).ConfigureAwait(false);
				}
			});
		}
		//public async Task ShootAsync()
		//{
		//	if (!_isCameraOverlayOpen && RuntimeData.Instance?.IsCameraAvailable == true)
		//	{
		//		await RunFunctionWhileOpenAsyncT(async delegate
		//		{
		//			if (!_isCameraOverlayOpen && RuntimeData.Instance?.IsCameraAvailable == true)
		//			{
		//				var folder = _folder;
		//				var file = await CreateAudioPhotoFileAsync(DEFAULT_PHOTO_FILE_NAME);
		//				if (folder != null && file != null)
		//				{
		//					IsCameraOverlayOpen = true;
		//					await _camera.OpenAsync();
		//					await _camera.StartAsync(file); // this locks until explicitly unlocked
		//					await _camera.CloseAsync();
		//					await folder.ImportMediaFileIntoNewWalletAsync(file, false).ConfigureAwait(false);
		//					IsCameraOverlayOpen = false;
		//				}
		//			}
		//		}).ConfigureAwait(false);
		//	}
		//}
		private readonly object _captureLock = new object();
		private bool _isCapturing = false;
		public async Task ShootAsync(bool createWallet, Wallet parentWallet)
		{
			if (RuntimeData.Instance?.IsCameraAvailable != true) return;
			if (!createWallet && parentWallet == null) return;
			await RunFunctionWhileOpenAsyncA(delegate
			{
				if (RuntimeData.Instance?.IsCameraAvailable != true) return;
				if (!createWallet && parentWallet == null) return;
				var folder = _folder;
				if (folder != null)
				{
					try // CameraCaptureUI causes a suspend on the phone, so the app quits before the photo is saved
					{
						lock (_captureLock) { if (_isCapturing) return; else _isCapturing = true; }

						var captureUI = new CameraCaptureUI();
						captureUI.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
						captureUI.PhotoSettings.MaxResolution = CameraCaptureUIMaxPhotoResolution.HighestAvailable;

						var captureTask = captureUI.CaptureFileAsync(CameraCaptureUIMode.Photo).AsTask();
						var afterCaptureTask = captureTask.ContinueWith(delegate
						{
							return AfterCaptureTask(captureTask, folder, createWallet, parentWallet);
						});
						//var afterCaptureTask = captureTask.ContinueWith(async delegate
						//{
						//	var photoFile = captureTask.Result;
						//	if (photoFile == null || folder == null)
						//	{
						//		// User cancelled photo capture
						//		return;
						//	}
						//	else
						//	{
						//		if (createWallet)
						//		{
						//			await folder.ImportMediaFileIntoNewWalletAsync(photoFile, true).ConfigureAwait(false);
						//		}
						//		else if (!createWallet && parentWallet != null)
						//		{
						//			await parentWallet.ImportMediaFileAsync(photoFile, true).ConfigureAwait(false);
						//		}
						//		RegistryAccess.SetValue("ShootAsync.createWallet", string.Empty);
						//		RegistryAccess.SetValue("ShootAsync.parentWallet", string.Empty);

						//		await photoFile.DeleteAsync();

						//		await Logger.AddAsync("Photo file deleted", Logger.ForegroundLogFilename, Logger.Severity.Info).ConfigureAwait(false);
						//	}
						//	lock (_captureLock) { _isCapturing = false; }
						//});
					}
					catch (Exception ex)
					{
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
					}
				}
			}).ConfigureAwait(false);
		}

		private async Task AfterCaptureTask(Task<StorageFile> captureTask, Folder folder, bool createWallet, Wallet parentWallet)
		{
			try
			{
				var photoFile = await captureTask.ConfigureAwait(false);
				if (photoFile == null || folder == null)
				{
					// User cancelled photo capture
					await Logger.AddAsync("AfterCaptureTask quitting coz photoFile = null " + (photoFile == null).ToString() + " and folder = null " + (folder == null).ToString(), Logger.ForegroundLogFilename, Logger.Severity.Info).ConfigureAwait(false);
					return;
				}
				else
				{
					bool isAllSaved = false;
					if (createWallet && folder != null)
					{
						isAllSaved = await folder.ImportMediaFileIntoNewWalletAsync(photoFile, true).ConfigureAwait(false);
					}
					else if (!createWallet && parentWallet != null)
					{
						isAllSaved = await parentWallet.ImportMediaFileAsync(photoFile, true).ConfigureAwait(false);
					}
					if (isAllSaved)
					{
						ClearAfterShooting();
					}
					else
					{
						RegistryAccess.SetValue("ShootAsync.createWallet", createWallet.ToString());
						if (parentWallet != null) RegistryAccess.SetValue("ShootAsync.parentWallet", parentWallet.Id);
						RegistryAccess.SetValue("ShootAsync.folderPath", photoFile.Path);
					}

					await Logger.AddAsync("isAllSaved = " + isAllSaved.ToString(), Logger.ForegroundLogFilename, Logger.Severity.Info).ConfigureAwait(false);
				}
				await Logger.AddAsync("AfterCaptureTask ended", Logger.ForegroundLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
			}
			lock (_captureLock) { _isCapturing = false; }
		}

		private async Task ResumeAfterShootingAsync()
		{
			bool createWallet = false;
			bool wasShooting = bool.TryParse(RegistryAccess.GetValue("ShootAsync.createWallet"), out createWallet);

			if (wasShooting)
			{
				await Logger.AddAsync("FolderVM opened, was shooting before", Logger.ForegroundLogFilename, Logger.Severity.Info).ConfigureAwait(false);
				string parentWalletId = RegistryAccess.GetValue("ShootAsync.parentWallet");
				var wallet = Folder.Wallets.FirstOrDefault(wal => wal.Id == parentWalletId);
				var photoFileTask = StorageFile.GetFileFromPathAsync(RegistryAccess.GetValue("ShootAsync.folderPath")).AsTask();
				await AfterCaptureTask(photoFileTask, Folder, createWallet, wallet).ConfigureAwait(false);
				ClearAfterShooting();
			}
			else
			{
				await Logger.AddAsync("FolderVM opened, was NOT shooting before", Logger.ForegroundLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			}
		}
		private void ClearAfterShooting()
		{
			RegistryAccess.SetValue("ShootAsync.createWallet", string.Empty);
			RegistryAccess.SetValue("ShootAsync.parentWallet", string.Empty);
			RegistryAccess.SetValue("ShootAsync.folderPath", string.Empty);
		}

		public async Task RecordAudioAsync()
		{
			if (!_isAudioRecorderOverlayOpen && RuntimeData.Instance?.IsMicrophoneAvailable == true)
			{
				await RunFunctionWhileOpenAsyncT(async delegate
				{
					if (!_isAudioRecorderOverlayOpen && RuntimeData.Instance?.IsMicrophoneAvailable == true)
					{
						var folder = _folder;
						var file = await CreateAudioPhotoFileAsync(DEFAULT_AUDIO_FILE_NAME);
						if (folder != null && file != null)
						{
							IsAudioRecorderOverlayOpen = true;
							await _audioRecorder.OpenAsync();
							await _audioRecorder.StartAsync(file); // this locks until explicitly unlocked
							await _audioRecorder.CloseAsync();
							await folder.ImportMediaFileIntoNewWalletAsync(file, false).ConfigureAwait(false);
							IsAudioRecorderOverlayOpen = false;
						}
					}
				}).ConfigureAwait(false);
			}
		}
		private async Task<StorageFile> CreateAudioPhotoFileAsync(string fileName)
		{
			try
			{
				var dir = _folder?.DBManager?.Directory;
				if (dir != null)
				{
					var file = await dir.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);
					return file;
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return null;
		}
		#endregion add media


		#region edit categories
		public SwitchableObservableCollection<FolderCategorySelectorRow> _folderCategorySelector = new SwitchableObservableCollection<FolderCategorySelectorRow>();
		public SwitchableObservableCollection<FolderCategorySelectorRow> FolderCategorySelector { get { return _folderCategorySelector; } }
		public class FolderCategorySelectorRow : ObservableData
		{
			private string _name = string.Empty;
			public string Name
			{
				get { return _name; }
				set { if (_name != value) { _name = value; RaisePropertyChanged_UI(); } }
			}

			private bool _isOn = false;
			public bool IsOn
			{
				get { return _isOn; }
				set
				{
					if (_isOn != value)
					{
						_isOn = value; RaisePropertyChanged_UI();
						if (_isOn)
						{
							Task upd = _vm?.Folder?.AddDynamicCategoryAsync(_catId);
						}
						else
						{
							Task upd = _vm?.Folder?.RemoveDynamicCategoryAndItsFieldsAsync(_catId);
						}
					}
				}
			}

			private string _catId = null;

			private FolderVM _vm = null;

			internal FolderCategorySelectorRow(FolderVM vm, string name, string catId, bool isOn)
			{
				_vm = vm;
				_name = name;
				_catId = catId;
				_isOn = isOn;
			}
		}
		public void UpdateCurrentFolderCategories()
		{
			if (_folder?.DynamicCategories != null && MetaBriefcase.OpenInstance?.Categories != null)
			{
				_folderCategorySelector.Clear();
				foreach (var metaCat in MetaBriefcase.OpenInstance.Categories)
				{
					var newSelectorRow = new FolderCategorySelectorRow(this, metaCat.Name, metaCat.Id, _folder.DynamicCategories.Any(a => a.CategoryId == metaCat.Id));
					_folderCategorySelector.Add(newSelectorRow);
				}
			}
		}
		#endregion edit categories
	}


	public interface IRecorder : IOpenable
	{
		// Task<bool> StartAsync();
		/// <summary>
		/// This locks the caller asynchronously. StopAsync or CloseAsync unlock.
		/// </summary>
		/// <param name="file"></param>
		/// <returns></returns>
		Task<bool> StartAsync(StorageFile file);
		Task<bool> StopAsync();
	}
}
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

			if (SavingMediaFileEnded == null) SavingMediaFileEnded += OnSavingMediaFileEnded;

			await ResumeAfterShootingAsync().ConfigureAwait(false);
			await ResumeAfterFilePickAsync().ConfigureAwait(false);
		}

		private async void OnSavingMediaFileEnded(object sender, EventArgs e)
		{
			//			await Logger.AddAsync("saving ended caught", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			await ResumeAfterShootingAsync().ConfigureAwait(false);
			await ResumeAfterFilePickAsync().ConfigureAwait(false);
		}

		/// <summary>
		/// I need this override to stop any running media recording
		/// </summary>
		/// <returns></returns>
		public override async Task<bool> CloseAsync()
		{
			if (!_isOpen) return false;

			//await Logger.AddAsync("FolderVM closing", Logger.ForegroundLogFilename, Logger.Severity.Info).ConfigureAwait(false);

			SavingMediaFileEnded -= OnSavingMediaFileEnded;

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
		public void StartLoadMediaFile() // file open picker causes a suspend on the phone, so the app quits before the file is saved.
										 // LOLLO TODO Fix this across the app, wherever a picker is called
		{
			Task load = RunFunctionWhileOpenAsyncA(delegate
			{
				var folder = _folder;
				if (folder?.IsOpen == true)
				{
					lock (_captureLock) { if (_isCapturing) return; else _isCapturing = true; }
					//var file = await DocumentExtensions.PickMediaFileAsync();
					//await folder.ImportMediaFileIntoNewWalletAsync(file, true).ConfigureAwait(false);
					var directory = folder.DBManager?.Directory;
					if (directory != null)
					{
						var pickTask = DocumentExtensions.PickMediaFileAsync();
						var afterFilePickedTask = pickTask.ContinueWith(delegate
						{
							return AfterFilePickedTask(pickTask, directory, folder, null);
						});
					}
				}
				//				return Task.CompletedTask;
			});
			//await RunFunctionWhileOpenAsyncT(async delegate // LOLLO don't call the picker inside the semaphore, or it may crash if the app suspends (ie on the phone)
			//{
			//	var folder = _folder;
			//	if (folder != null && folder.IsOpen)
			//	{
			//		var file = await DocumentExtensions.PickMediaFileAsync();
			//		await folder.ImportMediaFileIntoNewWalletAsync(file, true).ConfigureAwait(false);
			//	}
			//});
		}
		public void StartLoadMediaFile(Wallet parentWallet)
		{
			Task load = RunFunctionWhileOpenAsyncA(delegate
			{
				var folder = _folder;
				if (folder?.IsOpen == true && parentWallet != null)
				{
					lock (_captureLock) { if (_isCapturing) return; else _isCapturing = true; }
					//var file = await DocumentExtensions.PickMediaFileAsync();
					//await parentWallet.ImportMediaFileAsync(file, true).ConfigureAwait(false);
					var directory = folder.DBManager?.Directory;
					if (directory != null)
					{
						var pickTask = DocumentExtensions.PickMediaFileAsync();
						var afterFilePickedTask = pickTask.ContinueWith(delegate
						{
							return AfterFilePickedTask(pickTask, directory, folder, parentWallet);
						});
					}
				}
				//return Task.CompletedTask;
			});
			//await RunFunctionWhileOpenAsyncT(async delegate // LOLLO don't call the picker inside the semaphore, or it may crash if the app suspends (ie on the phone)
			//{
			//	var folder = _folder;
			//	if (folder != null && folder.IsOpen && parentWallet != null)
			//	{
			//		var file = await DocumentExtensions.PickMediaFileAsync();
			//		await parentWallet.ImportMediaFileAsync(file, true).ConfigureAwait(false);
			//	}
			//});
		}
		private async Task AfterFilePickedTask(Task<StorageFile> pickTask, StorageFolder saveDirectory, Folder folder, Wallet parentWallet)
		{
			try
			{
				var file = await pickTask.ConfigureAwait(false);
				if (file == null || folder == null)
				{
					// User cancelled picking
					return;
				}
				else
				{
					StorageFile newFile = null;
					if (saveDirectory == null)
					{
						newFile = file;
					}
					else
					{
						newFile = await file.CopyAsync(saveDirectory, file.Name, NameCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false); // copy right after the picker or access will be forbidden later
					}

					bool isAllSaved = false;
					if (newFile != null)
					{
						if (parentWallet == null && folder != null)
						{
							isAllSaved = await folder.ImportMediaFileIntoNewWalletAsync(newFile, false).ConfigureAwait(false);
						}
						else if (parentWallet != null)
						{
							isAllSaved = await parentWallet.ImportMediaFileAsync(newFile, false).ConfigureAwait(false);
						}

						if (isAllSaved)
						{
							//RegistryAccess.SetValue("FilePicker.folderId", string.Empty);
							RegistryAccess.SetValue("FilePicker.parentWalletId", string.Empty);
							RegistryAccess.SetValue("FilePicker.filePath", string.Empty);
						}
						else
						{
							//if (folder != null) RegistryAccess.SetValue("FilePicker.folderId", folder.Id);
							if (parentWallet != null) RegistryAccess.SetValue("FilePicker.parentWalletId", parentWallet.Id);
							RegistryAccess.SetValue("FilePicker.filePath", newFile.Path);
							SavingMediaFileEnded?.Invoke(this, EventArgs.Empty);
						}
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex?.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}
			lock (_captureLock) { _isCapturing = false; }
		}
		private static event EventHandler SavingMediaFileEnded;
		private async Task ResumeAfterFilePickAsync()
		{
			string filePath = RegistryAccess.GetValue("FilePicker.filePath");
			bool wasPicking = !string.IsNullOrWhiteSpace(filePath);

			if (wasPicking)
			{
				string parentWalletId = RegistryAccess.GetValue("FilePicker.parentWalletId");
				var parentWallet = Folder.Wallets.FirstOrDefault(wal => wal.Id == parentWalletId);
				var pickFileTask = StorageFile.GetFileFromPathAsync(filePath).AsTask();

				await AfterFilePickedTask(pickFileTask, null, Folder, parentWallet).ConfigureAwait(false);
			}
			else
			{
				await Logger.AddAsync("FolderVM opened, was NOT picking before", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			}
		}

		//private void ClearAfterFilePick()
		//{
		//	//RegistryAccess.SetValue("FilePicker.folderId", string.Empty);
		//	RegistryAccess.SetValue("FilePicker.parentWalletId", string.Empty);
		//	RegistryAccess.SetValue("FilePicker.filePath", string.Empty);
		//}


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
		public void StartShoot(bool createWallet, Wallet parentWallet)
		{
			if (RuntimeData.Instance?.IsCameraAvailable != true) return;
			if (!createWallet && parentWallet == null) return;
			Task shoot = RunFunctionWhileOpenAsyncA(delegate
			{
				if (RuntimeData.Instance?.IsCameraAvailable != true) return;
				if (!createWallet && parentWallet == null) return;
				var folder = _folder;
				if (folder == null) return;

				try // CameraCaptureUI causes a suspend on the phone, so the app quits before the photo is saved
				{
					lock (_captureLock) { if (_isCapturing) return; else _isCapturing = true; }

					var captureUI = new CameraCaptureUI();
					captureUI.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
					captureUI.PhotoSettings.MaxResolution = CameraCaptureUIMaxPhotoResolution.HighestAvailable;

					var directory = folder?.DBManager?.Directory;
					var captureTask = captureUI.CaptureFileAsync(CameraCaptureUIMode.Photo).AsTask();
					var afterCaptureTask = captureTask.ContinueWith(delegate
					{
						return AfterCaptureTask(captureTask, directory, folder, createWallet, parentWallet);
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
			});
		}

		private async Task AfterCaptureTask(Task<StorageFile> captureTask, StorageFolder saveDirectory, Folder folder, bool createWallet, Wallet parentWallet)
		{
			try
			{
				var file = await captureTask.ConfigureAwait(false);
				if (file == null || folder == null)
				{
					// User cancelled photo capture
					return;
				}
				else
				{
					StorageFile newFile = null;
					if (saveDirectory == null)
					{
						newFile = file;
					}
					else
					{
						newFile = await file.CopyAsync(saveDirectory, file.Name, NameCollisionOption.GenerateUniqueName); // copy right after the picker or access will be forbidden later
					}

					bool isAllSaved = false;
					if (newFile != null)
					{
						if (createWallet && folder != null)
						{
							isAllSaved = await folder.ImportMediaFileIntoNewWalletAsync(newFile, false).ConfigureAwait(false);
						}
						else if (!createWallet && parentWallet != null)
						{
							isAllSaved = await parentWallet.ImportMediaFileAsync(newFile, false).ConfigureAwait(false);
						}

						if (isAllSaved)
						{
							RegistryAccess.SetValue("ShootAsync.createWallet", string.Empty);
							RegistryAccess.SetValue("ShootAsync.parentWallet", string.Empty);
							RegistryAccess.SetValue("ShootAsync.folderPath", string.Empty);
							await newFile.DeleteAsync().AsTask().ConfigureAwait(false);
						}
						else
						{
							RegistryAccess.SetValue("ShootAsync.createWallet", createWallet.ToString());
							if (parentWallet != null) RegistryAccess.SetValue("ShootAsync.parentWallet", parentWallet.Id);
							else RegistryAccess.SetValue("ShootAsync.parentWallet", string.Empty);
							RegistryAccess.SetValue("ShootAsync.folderPath", newFile.Path);
							SavingMediaFileEnded?.Invoke(this, EventArgs.Empty);
						}
					}
				}
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
				string parentWalletId = RegistryAccess.GetValue("ShootAsync.parentWallet");
				var parentWallet = Folder.Wallets.FirstOrDefault(wal => wal.Id == parentWalletId);
				var photoFileTask = StorageFile.GetFileFromPathAsync(RegistryAccess.GetValue("ShootAsync.folderPath")).AsTask();

				await AfterCaptureTask(photoFileTask, null, Folder, createWallet, parentWallet).ConfigureAwait(false);
			}
			//else
			//{
			//	await Logger.AddAsync("FolderVM opened, was NOT shooting before", Logger.ForegroundLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			//}
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
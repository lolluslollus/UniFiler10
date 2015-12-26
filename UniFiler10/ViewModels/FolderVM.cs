using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniFiler10.Controlz;
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
		// public const string DEFAULT_AUDIO_FILE_NAME = "Audio.mp3"; // LOLLO NOTE this fails with the phone, wav is good
		public const string DEFAULT_AUDIO_FILE_NAME = "Audio.wav";
		public const string DEFAULT_PHOTO_FILE_NAME = "Photo.jpg";

		private const string REG_FP_FOLDERID = "FilePicker.FolderId";
		private const string REG_FP_PARENTWALLETID = "FilePicker.ParentWalletId";
		private const string REG_FP_FILEPATH = "FilePicker.FilePath";

		private const string REG_SHOOT_FOLDERID = "ShootUi.FolderId";
		private const string REG_SHOOT_CREATEWALLET = "ShootUi.CreateWallet";
		private const string REG_SHOOT_PARENTWALLET = "ShootUi.ParentWallet";
		private const string REG_SHOOT_FILEPATH = "ShootUi.FilePath";


		private IRecorder _audioRecorder = null;
		private Folder _folder = null;
		public Folder Folder { get { return _folder; } }

		private RuntimeData _runtimeData = null;
		public RuntimeData RuntimeData { get { return _runtimeData; } private set { _runtimeData = value; RaisePropertyChanged_UI(); } }

		private bool _isAudioRecorderOverlayOpen = false;
		public bool IsAudioRecorderOverlayOpen
		{
			get { return _isAudioRecorderOverlayOpen; }
			set { _isAudioRecorderOverlayOpen = value; RaisePropertyChanged_UI(); }
		}

		private bool _isCanImportMedia = true;
		public bool IsCanImportMedia { get { return _isCanImportMedia; } private set { _isCanImportMedia = value; RaisePropertyChanged_UI(); } }

		private AnimationStarter _animationStarter = null;
		#endregion properties


		#region ctor and dispose
		public FolderVM(Folder folder, IRecorder audioRecorder/*, IRecorder camera*/, AnimationStarter animationStarter)
		{
			_folder = folder;
			_audioRecorder = audioRecorder;
			//_camera = camera;
			if (animationStarter == null) throw new ArgumentNullException("FolderVM ctor: animationStarter may not be null");
			_animationStarter = animationStarter;
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
			lock (_captureLock) { IsCanImportMedia = !IsPickingSaysTheRegistry() && !IsShootingSaysTheRegistry(); }
			UpdateCurrentFolderCategories();

			if (SavingMediaFileEnded == null) SavingMediaFileEnded += OnSavingMediaFileEnded;

			await ResumeAfterShootingAsync().ConfigureAwait(false);
			await ResumeAfterFilePickAsync().ConfigureAwait(false);
		}

		private async void OnSavingMediaFileEnded(object sender, EventArgs e)
		{
			await ResumeAfterShootingAsync().ConfigureAwait(false);
			await ResumeAfterFilePickAsync().ConfigureAwait(false);
		}

		/// <summary>
		/// I need this override to stop any running media recording, since they lock the semaphore.
		/// </summary>
		/// <returns></returns>
		public override async Task<bool> CloseAsync()
		{
			if (!_isOpen) return false;

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
		protected override Task CloseMayOverrideAsync()
		{
			SavingMediaFileEnded -= OnSavingMediaFileEnded;
			//_animationStarter.EndAllAnimations();
			return Task.CompletedTask;
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
		public void StartLoadMediaFile() // file open picker causes a suspend on the phone, so the app quits before the file is saved, hence the complexity.
		{
			Task load = RunFunctionWhileOpenAsyncA(delegate
			{
				var folder = _folder;
				if (folder?.IsOpen == true)
				{
					lock (_captureLock)
					{
						if (!_isCanImportMedia || IsPickingSaysTheRegistry() || IsShootingSaysTheRegistry())
						{
							IsCanImportMedia = false;
							return;
						}
						else IsCanImportMedia = false;
					}

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
			});
		}
		public void StartLoadMediaFile(Wallet parentWallet)
		{
			Task load = RunFunctionWhileOpenAsyncA(delegate
			{
				var folder = _folder;
				if (folder?.IsOpen == true && parentWallet != null)
				{
					lock (_captureLock)
					{
						if (!_isCanImportMedia || IsPickingSaysTheRegistry() || IsShootingSaysTheRegistry())
						{
							IsCanImportMedia = false;
							return;
						}
						else IsCanImportMedia = false;
					}

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
			});
		}
		private async Task AfterFilePickedTask(Task<StorageFile> pickTask, StorageFolder saveDirectory, Folder folder, Wallet parentWallet)
		{
			bool isAllSaved = false;
			try
			{
				var file = await pickTask.ConfigureAwait(false);
				if (file == null || folder == null)
				{
					// User cancelled picking
				}
				else
				{
					StorageFile newFile = null;
					_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
					if (saveDirectory == null)
					{
						newFile = file;
					}
					else
					{
						newFile = await file.CopyAsync(saveDirectory, file.Name, NameCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false); // copy right after the picker or access will be forbidden later
					}

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
							RegistryAccess.SetValue(REG_FP_FOLDERID, string.Empty);
							RegistryAccess.SetValue(REG_FP_PARENTWALLETID, string.Empty);
							RegistryAccess.SetValue(REG_FP_FILEPATH, string.Empty);
						}
						else // could not complete the operation: write away the relevant values, Resume() will follow up.
							 // this happens with low memory devices, that suspend the app when opening a picker or the camera ui.
						{
							RegistryAccess.SetValue(REG_FP_FOLDERID, folder.Id);
							if (parentWallet != null) RegistryAccess.SetValue(REG_FP_PARENTWALLETID, parentWallet.Id);
							else RegistryAccess.SetValue(REG_FP_PARENTWALLETID, string.Empty);
							RegistryAccess.SetValue(REG_FP_FILEPATH, newFile.Path);
							SavingMediaFileEnded?.Invoke(this, EventArgs.Empty);
						}
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex?.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}

			lock (_captureLock) { IsCanImportMedia = !IsPickingSaysTheRegistry() && !IsShootingSaysTheRegistry(); }

			_animationStarter.EndAllAnimations();
			if (isAllSaved) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			else _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
		}

		private bool IsPickingSaysTheRegistry()
		{
			string a = RegistryAccess.GetValue(REG_FP_FOLDERID);
			string b = RegistryAccess.GetValue(REG_FP_PARENTWALLETID);
			string c = RegistryAccess.GetValue(REG_FP_FILEPATH);
			return !(string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b) && string.IsNullOrWhiteSpace(c));
		}

		private static event EventHandler SavingMediaFileEnded;
		private async Task ResumeAfterFilePickAsync()
		{
			string filePath = RegistryAccess.GetValue(REG_FP_FILEPATH);
			bool wasPicking = !string.IsNullOrWhiteSpace(filePath);
			string folderId = RegistryAccess.GetValue(REG_FP_FOLDERID);

			if (wasPicking && Folder?.Id == folderId)
			{
				string parentWalletId = RegistryAccess.GetValue(REG_FP_PARENTWALLETID);
				var parentWallet = Folder.Wallets.FirstOrDefault(wal => wal.Id == parentWalletId);
				var pickFileTask = StorageFile.GetFileFromPathAsync(filePath).AsTask();

				await AfterFilePickedTask(pickFileTask, null, Folder, parentWallet).ConfigureAwait(false);
			}
			else
			{
				await Logger.AddAsync("FolderVM opened, was NOT picking before", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			}
		}

		private readonly object _captureLock = new object();

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
					lock (_captureLock)
					{
						if (!_isCanImportMedia || IsPickingSaysTheRegistry() || IsShootingSaysTheRegistry())
						{
							IsCanImportMedia = false;
							return;
						}
						else IsCanImportMedia = false;
					}

					var captureUI = new CameraCaptureUI();
					captureUI.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
					captureUI.PhotoSettings.MaxResolution = CameraCaptureUIMaxPhotoResolution.HighestAvailable;

					var directory = folder?.DBManager?.Directory;
					var captureTask = captureUI.CaptureFileAsync(CameraCaptureUIMode.Photo).AsTask();
					var afterCaptureTask = captureTask.ContinueWith(delegate
					{
						return AfterCaptureTask(captureTask, directory, folder, createWallet, parentWallet);
					});
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
			});
		}

		private async Task AfterCaptureTask(Task<StorageFile> captureTask, StorageFolder saveDirectory, Folder folder, bool createWallet, Wallet parentWallet)
		{
			bool isAllSaved = false;

			try
			{
				var file = await captureTask.ConfigureAwait(false);
				if (file == null || folder == null)
				{
					// User cancelled photo capture
				}
				else
				{
					StorageFile newFile = null;
					_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
					if (saveDirectory == null)
					{
						newFile = file;
					}
					else
					{
						newFile = await file.CopyAsync(saveDirectory, file.Name, NameCollisionOption.GenerateUniqueName); // copy right after the picker or access will be forbidden later
					}

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
							RegistryAccess.SetValue(REG_SHOOT_FOLDERID, string.Empty);
							RegistryAccess.SetValue(REG_SHOOT_CREATEWALLET, string.Empty);
							RegistryAccess.SetValue(REG_SHOOT_PARENTWALLET, string.Empty);
							RegistryAccess.SetValue(REG_SHOOT_FILEPATH, string.Empty);
							if (file != null) await file.DeleteAsync().AsTask().ConfigureAwait(false);
						}
						else // could not complete the operation: write away the relevant values, Resume() will follow up.
							 // this happens with low memory devices, that suspend the app when opening a picker or the camera ui.
						{
							RegistryAccess.SetValue(REG_SHOOT_FOLDERID, folder.Id);
							RegistryAccess.SetValue(REG_SHOOT_CREATEWALLET, createWallet.ToString());
							if (parentWallet != null) RegistryAccess.SetValue(REG_SHOOT_PARENTWALLET, parentWallet.Id);
							else RegistryAccess.SetValue(REG_SHOOT_PARENTWALLET, string.Empty);
							RegistryAccess.SetValue(REG_SHOOT_FILEPATH, newFile.Path);
							SavingMediaFileEnded?.Invoke(this, EventArgs.Empty);
						}
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
			}

			lock (_captureLock) { IsCanImportMedia = !IsPickingSaysTheRegistry() && !IsShootingSaysTheRegistry(); }

			_animationStarter.EndAllAnimations();
			if (isAllSaved) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			else _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
		}

		private bool IsShootingSaysTheRegistry()
		{
			string a = RegistryAccess.GetValue(REG_SHOOT_FOLDERID);
			string b = RegistryAccess.GetValue(REG_SHOOT_CREATEWALLET);
			string c = RegistryAccess.GetValue(REG_SHOOT_PARENTWALLET);
			string d = RegistryAccess.GetValue(REG_SHOOT_FILEPATH);
			return !(string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b) && string.IsNullOrWhiteSpace(c) && string.IsNullOrWhiteSpace(d));
		}
		private async Task ResumeAfterShootingAsync()
		{
			bool createWallet = false;
			bool wasShooting = bool.TryParse(RegistryAccess.GetValue(REG_SHOOT_CREATEWALLET), out createWallet);
			string folderId = RegistryAccess.GetValue(REG_SHOOT_FOLDERID);

			if (wasShooting && Folder?.Id == folderId)
			{
				await Logger.AddAsync("FolderVM opened, was shooting before", Logger.ForegroundLogFilename, Logger.Severity.Info).ConfigureAwait(false);

				string parentWalletId = RegistryAccess.GetValue(REG_SHOOT_PARENTWALLET);
				var parentWallet = Folder.Wallets.FirstOrDefault(wal => wal.Id == parentWalletId);
				var photoFileTask = StorageFile.GetFileFromPathAsync(RegistryAccess.GetValue(REG_SHOOT_FILEPATH)).AsTask();

				await Logger.AddAsync("FolderVM opened, was NOT shooting before", Logger.ForegroundLogFilename, Logger.Severity.Info).ConfigureAwait(false);
				try
				{
					await AfterCaptureTask(photoFileTask, null, Folder, createWallet, parentWallet).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
				}
			}
			else
			{
				await Logger.AddAsync("FolderVM opened, was NOT shooting before", Logger.ForegroundLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			}
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
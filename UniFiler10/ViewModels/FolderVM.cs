using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Model;
using UniFiler10.Data.Runtime;
using Utilz;
using Utilz.Data;
using Windows.Media.Capture;
using Windows.Storage;

namespace UniFiler10.ViewModels
{
	public class FolderVM : OpenableObservableDisposableData
	{
		#region properties
		private IRecorder _audioRecorderView = null;
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

		private static readonly object _isImportingLocker = new object();
		public bool IsImportingMedia
		{
			get
			{
				lock (_isImportingLocker)
				{
					string tf = RegistryAccess.GetValue(ConstantData.REG_IMPORT_MEDIA_IS_IMPORTING);
					return tf == true.ToString();
				}
			}
			private set
			{
				lock (_isImportingLocker)
				{
					RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_IS_IMPORTING, value.ToString());
					RaisePropertyChanged_UI();
				}
			}
		}

		private AnimationStarter _animationStarter = null;
		#endregion properties


		#region ctor and dispose
		public FolderVM(Folder folder, IRecorder audioRecorder/*, IRecorder camera*/, AnimationStarter animationStarter)
		{
			_folder = folder;
			_audioRecorderView = audioRecorder;
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
			UpdateCurrentFolderCategories();

			if (IsImportingMedia)
			{
				var folder = _folder;
				var directory = folder?.DBManager?.Directory;

				string folderId = RegistryAccess.GetValue(ConstantData.REG_IMPORT_MEDIA_FOLDERID);
				if (folder?.Id == folderId && directory != null)
				{
					try
					{
						string parentWalletId = RegistryAccess.GetValue(ConstantData.REG_IMPORT_MEDIA_PARENTWALLETID);
						var parentWallet = folder.Wallets.FirstOrDefault(wal => wal.Id == parentWalletId);

						bool wasShooting = RegistryAccess.GetValue(ConstantData.REG_IMPORT_MEDIA_IS_SHOOTING) == true.ToString();

						var file = await Pickers.GetLastPickedOpenFileAsync().ConfigureAwait(false);

						await ContinueAfterFilePickAsync(file, directory, folder, parentWallet, wasShooting).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						await Logger.AddAsync(ex.ToString(), Logger.AppEventsLogFilename).ConfigureAwait(false);
					}
				}
			}
		}

		protected override async Task CloseMayOverrideAsync()
		{
			Debug.WriteLine("CloseMayOverrideAsync() is about to close the audio recorder");
			var ar = _audioRecorderView;
			if (ar != null) await ar.CloseAsync();

			IsAudioRecorderOverlayOpen = false;
		}
		#endregion open close


		#region user actions
		public Task<bool> TrySetFieldValueAsync(DynamicField dynFld, string newValue)
		{
			return RunFunctionIfOpenAsyncTB(delegate
			{
				return dynFld?.TrySetFieldValueAsync(newValue);
			});
		}
		public Task<bool> RemoveWalletFromFolderAsync(Wallet wallet)
		{
			return RunFunctionIfOpenAsyncTB(delegate
			{
				return _folder?.RemoveWalletAsync(wallet);
			});
		}
		public Task<bool> RemoveDocumentFromWalletAsync(Wallet wallet, Document doc)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				bool isOk = false;
				if (wallet != null)
				{
					await wallet.OpenAsync();
					isOk = await wallet.RemoveDocumentAsync(doc);

					if (wallet.Documents.Count < 1) // if there are no more documents in the wallet, delete the wallet, too
					{
						var folder = _folder;
						if (folder != null)
						{
							isOk = isOk & await folder.RemoveWalletAsync(wallet).ConfigureAwait(false);
						}
						else
						{
							isOk = false;
						}
					}
				}
				return isOk;
			});
		}
		#endregion user actions


		#region add media
		public async void StartLoadMediaFile()
		{
			var folder = _folder;
			var directory = folder?.DBManager?.Directory;
			if (folder != null && directory != null && !IsImportingMedia)
			{
				IsImportingMedia = true;

				RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_FOLDERID, folder.Id);
				RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_PARENTWALLETID, string.Empty);
				RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_IS_SHOOTING, false.ToString());

				var file = await DocumentExtensions.PickMediaFileAsync().ConfigureAwait(false);

				// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
				// To avoid surprises, we try the following here under _isOpenSemaphore. If it does not run through, IsImportingMedia will stay true.
				// In OpenMayOverrideAsync, we check IsImportingMedia and, if true, we try again.
				// ContinueAfterFilePickAsync sets IsImportingMedia to false, so there won't be redundant attempts.
				await RunFunctionIfOpenThreeStateAsyncT(delegate
				{
					return ContinueAfterFilePickAsync(file, directory, folder, null);
				}).ConfigureAwait(false);
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		public async void StartLoadMediaFile(Wallet parentWallet)
		{
			var folder = _folder;
			var directory = folder?.DBManager?.Directory;
			if (folder != null && directory != null && !IsImportingMedia)
			{
				IsImportingMedia = true;

				RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_FOLDERID, folder.Id);
				RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_PARENTWALLETID, parentWallet == null ? string.Empty : parentWallet.Id);
				RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_IS_SHOOTING, false.ToString());

				var file = await DocumentExtensions.PickMediaFileAsync().ConfigureAwait(false);

				// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
				// To avoid surprises, we try the following here under _isOpenSemaphore. If it does not run through, IsImportingMedia will stay true.
				// In OpenMayOverrideAsync, we check IsImportingMedia and, if true, we try again.
				// ContinueAfterFilePickAsync sets IsImportingMedia to false, so there won't be redundant attempts.
				await RunFunctionIfOpenThreeStateAsyncT(delegate
				{
					return ContinueAfterFilePickAsync(file, directory, folder, parentWallet);
				}).ConfigureAwait(false);
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		public async void StartShoot(Wallet parentWallet)
		{
			var folder = _folder;
			var directory = folder?.DBManager?.Directory;
			if (folder != null && directory != null && !IsImportingMedia && RuntimeData.Instance?.IsCameraAvailable == true)
			{
				try
				{
					IsImportingMedia = true;

					RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_FOLDERID, folder.Id);
					RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_PARENTWALLETID, parentWallet == null ? string.Empty : parentWallet.Id);
					RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_IS_SHOOTING, true.ToString());

					var captureUI = new CameraCaptureUI();
					captureUI.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
					captureUI.PhotoSettings.MaxResolution = CameraCaptureUIMaxPhotoResolution.HighestAvailable;

					var file = await captureUI.CaptureFileAsync(CameraCaptureUIMode.Photo).AsTask();
					Pickers.SetLastPickedOpenFile(file); // little race here, hard to avoid and apparently harmless

					// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
					// To avoid surprises, we try the following here under _isOpenSemaphore. If it does not run through, IsImportingMedia will stay true.
					// In OpenMayOverrideAsync, we check IsImportingMedia and, if true, we try again.
					// ContinueAfterFilePickAsync sets IsImportingMedia to false, so there won't be redundant attempts.
					await RunFunctionIfOpenThreeStateAsyncT(delegate
					{
						return ContinueAfterFilePickAsync(file, directory, folder, parentWallet);
					}).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.AppEventsLogFilename);
					_animationStarter.EndAllAnimations();
					_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
				}
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		private async Task ContinueAfterFilePickAsync(StorageFile file, StorageFolder directory, Folder folder, Wallet parentWallet, bool deleteFile = false)
		{
			bool isImported = false;

			try
			{
				if (directory != null && file != null)
				{
					_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);

					StorageFile newFile = await file.CopyAsync(directory, file.Name, NameCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);

					if (parentWallet == null)
					{
						isImported = await folder.ImportMediaFileIntoNewWalletAsync(newFile, false).ConfigureAwait(false);

					}
					else
					{
						isImported = await parentWallet.ImportMediaFileAsync(newFile, false).ConfigureAwait(false);
					}

					if (!isImported)
					{
						// delete the copied file if something went wrong
						if (newFile != null) await newFile.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
						Logger.Add_TPL("isImported = false", Logger.AppEventsLogFilename, Logger.Severity.Info);
					}
					// delete the original file if it was a photo taken with CameraCaptureUI
					if (deleteFile) await file.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.AppEventsLogFilename).ConfigureAwait(false);
			}

			_animationStarter.EndAllAnimations();
			if (isImported) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			else _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);

			IsImportingMedia = false;
		}

		public async Task RecordAudioAsync()
		{
			if (!_isAudioRecorderOverlayOpen && RuntimeData.Instance?.IsMicrophoneAvailable == true)
			{
				await RunFunctionIfOpenAsyncT(async delegate
				{
					if (!_isAudioRecorderOverlayOpen && RuntimeData.Instance?.IsMicrophoneAvailable == true)
					{
						var folder = _folder;
						var file = await CreateAudioPhotoFileAsync(ConstantData.DEFAULT_AUDIO_FILE_NAME);
						if (folder != null && file != null)
						{
							try
							{
								IsAudioRecorderOverlayOpen = true;
								await _audioRecorderView.OpenAsync();
								await _audioRecorderView.RecordAsync(file, CancellationTokenSafe); // this locks until explicitly unlocked
								await _audioRecorderView.CloseAsync();
								IsAudioRecorderOverlayOpen = false;

								if (Cts?.IsCancellationRequested == false)
								{
									bool mediaImportedOk = await folder.ImportMediaFileIntoNewWalletAsync(file, false).ConfigureAwait(false);
									Debug.WriteLine("RecordAudioAsync(): mediaImportedOk = " + mediaImportedOk);
								}
								else
								{
									Debug.WriteLine("RecordAudioAsync(): recording interrupted");
								}
							}
							catch (Exception ex)
							{
								Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
							}
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
		public SwitchableObservableDisposableCollection<FolderCategorySelectorRow> _folderCategorySelector = new SwitchableObservableDisposableCollection<FolderCategorySelectorRow>();
		public SwitchableObservableDisposableCollection<FolderCategorySelectorRow> FolderCategorySelector { get { return _folderCategorySelector; } }
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
		/// <summary>
		/// This locks the caller asynchronously. StopAsync or CloseAsync unlock.
		/// </summary>
		/// <param name="file"></param>
		/// <returns></returns>
		Task<bool> RecordAsync(StorageFile file, CancellationToken cancToken);
		//Task<bool> StopAsync();
		bool IsRecording { get; }
	}
}
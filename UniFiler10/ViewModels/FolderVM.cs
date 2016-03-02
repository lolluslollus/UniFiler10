using System;
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
using Windows.System;

namespace UniFiler10.ViewModels
{
	public class FolderVM : OpenableObservableDisposableData
	{
		#region properties
		private readonly IRecorder _audioRecorderView = null;
		private Folder _folder = null;
		public Folder Folder { get { return _folder; } }

		private RuntimeData _runtimeData = null;
		public RuntimeData RuntimeData { get { return _runtimeData; } private set { _runtimeData = value; RaisePropertyChanged_UI(); } }

		private Briefcase _briefcase = null;
		public Briefcase Briefcase { get { return _briefcase; } private set { _briefcase = value; RaisePropertyChanged_UI(); } }

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
		private bool TrySetIsImportingMedia(bool newValue)
		{
			lock (_isImportingLocker)
			{
				if (IsImportingMedia != newValue)
				{
					IsImportingMedia = newValue;
					return true;
				}
				return false;
			}
		}
		private readonly AnimationStarter _animationStarter = null;
		#endregion properties


		#region lifecycle
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
			_folder = null; // do not dispose it, only briefcase may do so.
			base.Dispose(isDisposing);
		}

		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			await RunInUiThreadAsync(delegate
			{
				Briefcase = Briefcase.GetCurrentInstance();
				RuntimeData = RuntimeData.Instance;
				FolderCategorySelector = new SwitchableObservableDisposableCollection<FolderCategorySelectorRow>();
				UpdateCurrentFolderCategories();
			}).ConfigureAwait(false);

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
			if (ar != null) await ar.CloseAsync().ConfigureAwait(false);

			IsAudioRecorderOverlayOpen = false;

			_folderCategorySelector?.Dispose();
		}
		#endregion lifecycle


		#region user actions
		public Task<bool> TrySetFieldValueAsync(DynamicField dynFld, string newValue)
		{
			return RunFunctionIfOpenAsyncTB(() => dynFld?.TrySetFieldValueAsync(newValue));
		}
		public Task<bool> RemoveWalletFromFolderAsync(Wallet wallet)
		{
			return RunFunctionIfOpenAsyncTB(() => _folder?.RemoveWalletAsync(wallet));
		}
		public async Task<bool> OcrDocumentAsync(Wallet wallet, Document doc)
		{
			if (wallet == null || doc == null) return false;

			var textLines = await doc.GetTextFromPictureAsync().ConfigureAwait(false);
			if (textLines == null || !textLines.Any()) return false;

			var directory = wallet.DBManager?.Directory;
			if (directory == null) return false;

			var newFile = await directory.CreateFileAsync(Guid.NewGuid().ToString() + DocumentExtensions.TXT_EXTENSION).AsTask().ConfigureAwait(false);
			if (newFile == null) return false;

			var sb = new StringBuilder();
			foreach (var textLine in textLines) { sb.AppendLine(textLine); }
			await DocumentExtensions.WriteTextIntoFileAsync(sb.ToString(), newFile).ConfigureAwait(false);

			return await wallet.ImportFileAsync(newFile).ConfigureAwait(false);
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

		public async Task OpenDocument(Document doc)
		{
			if (doc == null) return;
			try
			{
				var file = await StorageFile.GetFileFromPathAsync(doc?.GetFullUri0()).AsTask(); //.ConfigureAwait(false);
				if (file == null) return;

				bool isOk = false;
				// LOLLO TODO maybe open a big textBlock to see text files?
				isOk = await Launcher.LaunchFileAsync(file, new LauncherOptions() { DisplayApplicationPicker = true }).AsTask().ConfigureAwait(false);
				//isOk = await Launcher.LaunchFileAsync(file).AsTask().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Debugger.Break();
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
			}
		}

		#endregion user actions


		#region add media
		public async void StartLoadMediaFile()
		{
			if (!IsOpen) return;
			var folder = _folder;
			var directory = folder?.DBManager?.Directory;
			if (folder != null && directory != null && TrySetIsImportingMedia(true))
			{
				RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_FOLDERID, folder.Id);
				RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_PARENTWALLETID, string.Empty);
				RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_IS_SHOOTING, false.ToString());

				var file = await DocumentExtensions.PickMediaFileAsync().ConfigureAwait(false);

				// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
				// To avoid surprises, we try the following here under _isOpenSemaphore. If it does not run through, IsImportingMedia will stay true.
				// In OpenMayOverrideAsync, we check IsImportingMedia and, if true, we try again.
				// ContinueAfterFilePickAsync sets IsImportingMedia to false, so there won't be redundant attempts.
				await RunFunctionIfOpenThreeStateAsyncT(() => ContinueAfterFilePickAsync(file, directory, folder, null)).ConfigureAwait(false);
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		public async void StartLoadMediaFile(Wallet parentWallet)
		{
			if (!IsOpen) return;
			var folder = _folder;
			var directory = folder?.DBManager?.Directory;
			if (folder != null && directory != null && TrySetIsImportingMedia(true))
			{
				RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_FOLDERID, folder.Id);
				RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_PARENTWALLETID, parentWallet == null ? string.Empty : parentWallet.Id);
				RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_IS_SHOOTING, false.ToString());

				var file = await DocumentExtensions.PickMediaFileAsync().ConfigureAwait(false);

				// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
				// To avoid surprises, we try the following here under _isOpenSemaphore. If it does not run through, IsImportingMedia will stay true.
				// In OpenMayOverrideAsync, we check IsImportingMedia and, if true, we try again.
				// ContinueAfterFilePickAsync sets IsImportingMedia to false, so there won't be redundant attempts.
				await RunFunctionIfOpenThreeStateAsyncT(() => ContinueAfterFilePickAsync(file, directory, folder, parentWallet)).ConfigureAwait(false);
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		public async void StartShoot(Wallet parentWallet)
		{
			if (!IsOpen) return;
			var folder = _folder;
			var directory = folder?.DBManager?.Directory;
			if (folder != null && directory != null && RuntimeData.Instance?.IsCameraAvailable == true && TrySetIsImportingMedia(true))
			{
				try
				{
					RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_FOLDERID, folder.Id);
					RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_PARENTWALLETID, parentWallet == null ? string.Empty : parentWallet.Id);
					RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_MEDIA_IS_SHOOTING, true.ToString());

					var captureUI = new CameraCaptureUI();
					captureUI.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
					captureUI.PhotoSettings.MaxResolution = Briefcase.GetCurrentInstance().CameraCaptureResolution;
					captureUI.PhotoSettings.AllowCropping = false;

					var file = await captureUI.CaptureFileAsync(CameraCaptureUIMode.Photo).AsTask();
					Pickers.SetLastPickedOpenFile(file); // little race here, hard to avoid and apparently harmless

					// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
					// To avoid surprises, we try the following here under _isOpenSemaphore. If it does not run through, IsImportingMedia will stay true.
					// In OpenMayOverrideAsync, we check IsImportingMedia and, if true, we try again.
					// ContinueAfterFilePickAsync sets IsImportingMedia to false, so there won't be redundant attempts.
					await RunFunctionIfOpenThreeStateAsyncT(() => ContinueAfterFilePickAsync(file, directory, folder, parentWallet)).ConfigureAwait(false);
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

		private async Task ContinueAfterFilePickAsync(IStorageFile file, IStorageFolder directory, Folder folder, Wallet parentWallet, bool deleteFile = false)
		{
			bool isImported = false;

			try
			{
				if (directory != null && file != null && await file.GetFileSizeAsync().ConfigureAwait(false) <= ConstantData.MAX_IMPORTABLE_MEDIA_FILE_SIZE)
				{
					_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);

					StorageFile newFile = await file.CopyAsync(directory, file.Name, NameCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);

					if (parentWallet == null)
					{
						isImported = await folder.ImportMediaFileIntoNewWalletAsync(newFile).ConfigureAwait(false);

					}
					else
					{
						isImported = await parentWallet.ImportFileAsync(newFile).ConfigureAwait(false);
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
			_animationStarter.StartAnimation(isImported
				? AnimationStarter.Animations.Success
				: AnimationStarter.Animations.Failure);

			IsImportingMedia = false;
		}

		public async Task RecordAudioAsync(Wallet wallet)
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
								bool hasRecorded = await _audioRecorderView.RecordAsync(file, CancToken); // this locks until explicitly unlocked
								await _audioRecorderView.CloseAsync();
								IsAudioRecorderOverlayOpen = false;

								if (hasRecorded)
								{
									if (wallet == null)
									{
										bool mediaImportedOk = await folder.ImportMediaFileIntoNewWalletAsync(file).ConfigureAwait(false);
										Debug.WriteLine("RecordAudioAsync(): mediaImportedOk = " + mediaImportedOk);
									}
									else
									{
										bool mediaImportedOk = await wallet.ImportFileAsync(file).ConfigureAwait(false);
										Debug.WriteLine("RecordAudioAsync(): mediaImportedOk = " + mediaImportedOk);
									}
								}
								else
								{
									await file.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
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
		public SwitchableObservableDisposableCollection<FolderCategorySelectorRow> _folderCategorySelector = null; // new SwitchableObservableDisposableCollection<FolderCategorySelectorRow>();
		public SwitchableObservableDisposableCollection<FolderCategorySelectorRow> FolderCategorySelector { get { return _folderCategorySelector; } private set { _folderCategorySelector = value; RaisePropertyChanged_UI(); } }
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
					if (_isOn == value) return;

					_isOn = value; RaisePropertyChanged_UI();
					if (value)
					{
						Task upd = _vm?.Folder?.AddDynamicCategoryAsync(_catId);
					}
					else
					{
						Task upd = _vm?.Folder?.RemoveDynamicCategoryAndItsFieldsAsync(_catId);
					}
				}
			}

			private readonly string _catId = null;

			private readonly FolderVM _vm = null;

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
			if (_folder?.DynamicCategories == null || MetaBriefcase.OpenInstance?.Categories == null) return;

			_folderCategorySelector.Clear();
			foreach (var metaCat in MetaBriefcase.OpenInstance.Categories)
			{
				var newSelectorRow = new FolderCategorySelectorRow(this, metaCat.Name, metaCat.Id, _folder.DynamicCategories.Any(a => a.CategoryId == metaCat.Id));
				_folderCategorySelector.Add(newSelectorRow);
			}
		}
		#endregion edit categories
	}


	public interface IRecorder : IOpenable
	{
		/// <summary>
		/// It starts recording and locks the caller asynchronously. Use the cancellation token to unlock it.
		/// It returns a bool telling if the recording was successful.
		/// </summary>
		/// <param name="file"></param>
		/// <param name="cancToken"></param>
		/// <returns></returns>
		Task<bool> RecordAsync(StorageFile file, CancellationToken cancToken);
		bool IsRecording { get; }
	}
}
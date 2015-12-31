using System;
using System.Collections.Generic;
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
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Storage;

namespace UniFiler10.ViewModels
{
	public class FolderVM : OpenableObservableData
	{
		#region properties
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

		private volatile bool _isCanImportMedia = true;
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
			UpdateCurrentFolderCategories();

			if (SavingMediaFileEnded == null) SavingMediaFileEnded += OnSavingMediaFileEnded;

			await ResumeAfterFilePickAsync().ConfigureAwait(false);
		}

		private async void OnSavingMediaFileEnded(object sender, EventArgs e)
		{
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
		public void StartLoadMediaFile() // file open picker causes a suspend on the phone, so the app quits before the file is saved, hence the complexity.
		{
			var folder = _folder;
			var directory = folder?.DBManager?.Directory;
			if (folder != null && directory != null && _isCanImportMedia)
			{
				IsCanImportMedia = false;
				var pickTask = DocumentExtensions.PickMediaFileAsync();
				var afterFilePickedTask = pickTask.ContinueWith(delegate
				{
					return ContinueAfterFilePickAsync(pickTask, directory, folder, null, true);
				});
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		public void StartLoadMediaFile(Wallet parentWallet) // file open picker causes a suspend on the phone, so the app quits before the file is saved, hence the complexity.
		{
			var folder = _folder;
			var directory = folder?.DBManager?.Directory;
			if (folder != null && directory != null && _isCanImportMedia)
			{
				IsCanImportMedia = false;
				var pickTask = DocumentExtensions.PickMediaFileAsync();
				var afterFilePickedTask = pickTask.ContinueWith(delegate
				{
					return ContinueAfterFilePickAsync(pickTask, directory, folder, parentWallet, true);
				});
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		public void StartShoot(Wallet parentWallet)
		{
			if (RuntimeData.Instance?.IsCameraAvailable != true) return;
			if (RuntimeData.Instance?.IsCameraAvailable != true) return;
			//if (parentWallet == null) return;
			var folder = _folder;
			var directory = folder?.DBManager?.Directory;
			if (folder != null && directory != null && _isCanImportMedia)
			{
				try // CameraCaptureUI causes a suspend on the phone, so the app quits before the photo is saved
				{
					IsCanImportMedia = false;

					var captureUI = new CameraCaptureUI();
					captureUI.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
					captureUI.PhotoSettings.MaxResolution = CameraCaptureUIMaxPhotoResolution.HighestAvailable;

					var captureTask = captureUI.CaptureFileAsync(CameraCaptureUIMode.Photo).AsTask();
					var afterCaptureTask = captureTask.ContinueWith(delegate
					{
						return ContinueAfterFilePickAsync(captureTask, directory, folder, parentWallet, true);
					});
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
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

		private async Task ContinueAfterFilePickAsync(Task<StorageFile> pickTask, StorageFolder directory, Folder folder, Wallet parentWallet, bool copyFile)
		{
			bool isImported = false;
			bool isNeedsContinuing = false;
			try
			{
				if (directory != null)
				{
					var file = await pickTask.ConfigureAwait(false);
					if (file != null)
					{
						_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);

						StorageFile newFile = null;
						if (copyFile) newFile = await file.CopyAsync(directory, file.Name, NameCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);
						else newFile = file;

						if (parentWallet == null)
						{
							isImported = await folder.ImportMediaFileIntoNewWalletAsync(newFile, false).ConfigureAwait(false);
							if (!isImported && !folder.IsOpen) isNeedsContinuing = true; // LOLLO if isOk is false because there was an error and not because the app was suspended, I must unlock impexp.
							else if (!isImported && folder.IsOpen) await newFile.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false); // there was an error: stop trying
						}
						else if (parentWallet != null)
						{
							isImported = await parentWallet.ImportMediaFileAsync(newFile, false).ConfigureAwait(false);
							if (!isImported && !parentWallet.IsOpen) isNeedsContinuing = true; // LOLLO if isOk is false because there was an error and not because the app was suspended, I must unlock impexp.
							else if (!isImported && parentWallet.IsOpen) await newFile.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false); // there was an error: stop trying
						}
						if (newFile == null || isImported) RegistryAccess.SetValue(ConstantData.REG_FP_FILEPATH, string.Empty);
						else RegistryAccess.SetValue(ConstantData.REG_FP_FILEPATH, newFile.Path);
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}

			_animationStarter.EndAllAnimations();

			if (isImported)
			{
				_animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			}
			if (isNeedsContinuing)
			{
				RegistryAccess.SetValue(ConstantData.REG_FP_CONTINUE_IMPORTING, true.ToString());
				if (folder != null) RegistryAccess.SetValue(ConstantData.REG_FP_FOLDERID, folder.Id);
				else RegistryAccess.SetValue(ConstantData.REG_FP_FOLDERID, string.Empty);
				if (parentWallet != null) RegistryAccess.SetValue(ConstantData.REG_FP_PARENTWALLETID, parentWallet.Id);
				else RegistryAccess.SetValue(ConstantData.REG_FP_PARENTWALLETID, string.Empty);
			}
			else // could not complete the operation: write away the relevant values, Resume() will follow up.
				 // this happens with low memory devices, that suspend the app when opening a picker or the camera ui.
			{
				RegistryAccess.SetValue(ConstantData.REG_FP_CONTINUE_IMPORTING, false.ToString());
				if (!isImported) _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
				RegistryAccess.SetValue(ConstantData.REG_FP_FOLDERID, string.Empty);
				RegistryAccess.SetValue(ConstantData.REG_FP_PARENTWALLETID, string.Empty);
				IsCanImportMedia = true;
			}

			SavingMediaFileEnded?.Invoke(this, EventArgs.Empty);
		}

		private async Task ResumeAfterFilePickAsync()
		{
			//string folderId = RegistryAccess.GetValue(ConstantData.REG_FP_FOLDERID);
			//var folder = _folder;
			//var directory = folder?.DBManager?.Directory;
			//if (folder?.Id == folderId && directory != null)
			//{
			//IsCanImportMedia = false;
			//_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
			string continueImporting = RegistryAccess.GetValue(ConstantData.REG_FP_CONTINUE_IMPORTING);
			if (continueImporting == true.ToString())
			{
				string folderId = RegistryAccess.GetValue(ConstantData.REG_FP_FOLDERID);
				var folder = _folder;
				var directory = folder?.DBManager?.Directory;
				if (folder?.Id == folderId && directory != null)
				{
					try
					{
						IsCanImportMedia = false;
						_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);

						string parentWalletId = RegistryAccess.GetValue(ConstantData.REG_FP_PARENTWALLETID);
						var parentWallet = Folder.Wallets.FirstOrDefault(wal => wal.Id == parentWalletId);
						string filePath = RegistryAccess.GetValue(ConstantData.REG_FP_FILEPATH);
						var pickFileTask = StorageFile.GetFileFromPathAsync(filePath).AsTask();
						await ContinueAfterFilePickAsync(pickFileTask, directory, folder, parentWallet, false).ConfigureAwait(false);
						return;
					}
					catch (Exception ex)
					{
						await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
						IsCanImportMedia = true;
					}
				}
			}
			//}
			else
			{
				IsCanImportMedia = true;
			}
		}

		private static event EventHandler SavingMediaFileEnded;


		public async Task RecordAudioAsync()
		{
			if (!_isAudioRecorderOverlayOpen && RuntimeData.Instance?.IsMicrophoneAvailable == true)
			{
				await RunFunctionWhileOpenAsyncT(async delegate
				{
					if (!_isAudioRecorderOverlayOpen && RuntimeData.Instance?.IsMicrophoneAvailable == true)
					{
						var folder = _folder;
						var file = await CreateAudioPhotoFileAsync(ConstantData.DEFAULT_AUDIO_FILE_NAME);
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
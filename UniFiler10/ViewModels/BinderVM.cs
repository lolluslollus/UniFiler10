using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Model;
using UniFiler10.Services;
using Utilz;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace UniFiler10.ViewModels
{
	public sealed class BinderVM : OpenableObservableData, IAudioFileGetter
	{
		private Binder _binder = null;
		public Binder Binder { get { return _binder; } private set { _binder = value; RaisePropertyChanged_UI(); } }

		private RuntimeData _runtimeData = null;
		public RuntimeData RuntimeData { get { return _runtimeData; } private set { _runtimeData = value; RaisePropertyChanged_UI(); } }


		#region construct dispose open close
		public BinderVM(Binder binder)
		{
			if (binder == null) throw new ArgumentNullException("BinderVM ctor: binder may not be null");

			Binder = binder;
			RuntimeData = RuntimeData.Instance;

			UpdateCurrentFolderCategories();

			_binder.PropertyChanged += OnBinder_PropertyChanged;
			UpdateOpenClose();
		}
		protected override Task CloseMayOverrideAsync()
		{
			EndRecordAudio();
			EndShoot();
			return Task.CompletedTask;
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			var binder = _binder;
			if (binder != null) binder.PropertyChanged -= OnBinder_PropertyChanged;

			_folderCategorySelector?.Dispose();
			_folderCategorySelector = null;
		}
		private void OnBinder_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Binder.CurrentFolder) || e.PropertyName == nameof(Binder.IsOpen))
			{
				UpdateCurrentFolderCategories();
			}
			if (e.PropertyName == nameof(Binder.IsOpen))
			{
				UpdateOpenClose();
			}
		}
		private void UpdateOpenClose()
		{
			if (_binder?.IsOpen == true)
			{
				Task open = OpenAsync();
			}
			else
			{
				Task close = CloseAsync();
			}
		}
		#endregion construct dispose open close

		#region actions
		public Task AddFolderAsync()
		{
			return _binder?.AddFolderAsync(new Folder());
		}
		public Task DeleteFolderAsync(Folder folder)
		{
			return _binder?.RemoveFolderAsync(folder);
		}
		public Task AddWalletToFolderAsync(Folder folder)
		{
			return folder?.AddWalletAsync(new Wallet());
		}
		public Task<bool> RemoveWalletFromFolderAsync(Folder folder, Wallet wallet)
		{
			return folder?.RemoveWalletAsync(wallet);
		}
		public Task AddEmptyDocumentToWalletAsync(Wallet wallet)
		{
			return wallet?.AddDocumentAsync(new Document());
		}
		public Task<bool> RemoveDocumentFromWalletAsync(Wallet wallet, Document doc)
		{
			return wallet?.RemoveDocumentAsync(doc);
		}
		public void OpenCover()
		{
			_binder?.SetIsCoverOpen(true);
		}
		public void GoBack()
		{
			var opener = _binder?.ParentPaneOpener;
			if (opener != null)
			{
				opener.IsShowingCover = true;
			}
		}
		public Task SelectFolderAsync(string folderId)
		{
			return _binder?.SetCurrentFolderIdAsync(folderId);
		}
		//public void TogglePaneOpen()
		//{
		//	_binder?.ToggleIsPaneOpen();
		//}
		#endregion actions

		#region save media
		private bool _isCameraOverlayOpen = false;
		public bool IsCameraOverlayOpen
		{
			get { return _isCameraOverlayOpen; }
			set { _isCameraOverlayOpen = value; RaisePropertyChanged_UI(); }
		}
		private bool _isAudioRecorderOverlayOpen = false;
		public bool IsAudioRecorderOverlayOpen
		{
			get { return _isAudioRecorderOverlayOpen; }
			set { _isAudioRecorderOverlayOpen = value; RaisePropertyChanged_UI(); }
		}

		public async Task LoadMediaFileAsync(Folder parentFolder)
		{
			var binder = _binder;
			if (binder != null && binder.IsOpen && parentFolder != null)
			{
				var file = await DocumentExtensions.PickMediaFileAsync();
				await parentFolder.ImportMediaFileIntoNewWalletAsync(file, true).ConfigureAwait(false);
			}
		}
		public async Task LoadMediaFileAsync(Wallet parentWallet)
		{
			var binder = _binder;
			if (binder != null && binder.IsOpen && parentWallet != null)
			{
				var file = await DocumentExtensions.PickMediaFileAsync();
				await parentWallet.ImportMediaFileAsync(file, true).ConfigureAwait(false);
			}
		}

		public async Task ShootAsync(Folder parentFolder)
		{
			var binder = _binder;
			if (binder != null && binder.IsOpen && !_isCameraOverlayOpen && parentFolder != null && RuntimeData.Instance?.IsCameraAvailable == true)
			{
				IsCameraOverlayOpen = true; // opens the Camera control
				await _photoSemaphore.WaitAsync(); // wait until someone calls EndShoot

				await parentFolder.ImportMediaFileIntoNewWalletAsync(GetPhotoFile(), false).ConfigureAwait(false);
			}
		}
		public async Task ShootAsync(Wallet parentWallet)
		{
			var binder = _binder;
			if (binder != null && binder.IsOpen && !_isCameraOverlayOpen && parentWallet != null && RuntimeData.Instance?.IsCameraAvailable == true)
			{
				IsCameraOverlayOpen = true; // opens the Camera control
				await _photoSemaphore.WaitAsync(); // wait until someone calls EndShoot

				await parentWallet.ImportMediaFileAsync(GetPhotoFile(), false).ConfigureAwait(false);
			}
		}
		public void EndShoot()
		{
			SemaphoreSlimSafeRelease.TryRelease(_photoSemaphore);
		}

		public async Task RecordAudioAsync(Folder parentFolder)
		{
			var binder = _binder;
			if (binder != null && binder.IsOpen && !_isAudioRecorderOverlayOpen && parentFolder != null && RuntimeData.Instance?.IsMicrophoneAvailable == true)
			{
				await CreateAudioFileAsync(); // required before we start any audio recording
				IsAudioRecorderOverlayOpen = true; // opens the AudioRecorder control
				await _audioSemaphore.WaitAsync(); // wait until someone calls EndRecordAudio

				await parentFolder.ImportMediaFileIntoNewWalletAsync(GetAudioFile(), false).ConfigureAwait(false);
			}
		}
		public void EndRecordAudio()
		{
			SemaphoreSlimSafeRelease.TryRelease(_audioSemaphore);
		}

		private static SemaphoreSlimSafeRelease _audioSemaphore = new SemaphoreSlimSafeRelease(0, 1); // this semaphore is red until explicitly released
		private StorageFile _audioFile = null;
		private async Task<StorageFile> CreateAudioFileAsync()
		{
			try
			{
				//var directory = ApplicationData.Current.LocalCacheFolder;
				var directory = await _binder.GetDirectoryAsync();
				_audioFile = await directory.CreateFileAsync("Audio.mp3", CreationCollisionOption.GenerateUniqueName);
				return _audioFile;
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return null;
		}
		public StorageFile GetAudioFile()
		{
			return _audioFile;
		}

		private static SemaphoreSlimSafeRelease _photoSemaphore = new SemaphoreSlimSafeRelease(0, 1); // this semaphore is red until explicitly released
		private StorageFile _photoFile = null;
		public async Task<StorageFile> CreatePhotoFileAsync()
		{
			try
			{
				//var directory = ApplicationData.Current.LocalCacheFolder;
				var binder = _binder;
				if (binder != null)
				{
					var directory = await binder.GetDirectoryAsync();
					if (directory != null)
					{
						_photoFile = await directory.CreateFileAsync("Photo.jpeg", CreationCollisionOption.GenerateUniqueName);
						return _photoFile;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return null;
		}
		private StorageFile GetPhotoFile()
		{
			return _photoFile;
		}
		//}
		#endregion save media

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
							Task upd = _vm._binder.CurrentFolder.AddDynamicCategoryAsync(_catId);
						}
						else
						{
							Task upd = _vm._binder.CurrentFolder.RemoveDynamicCategoryAsync(_catId);
						}
						//if (_vm?._binder?.CurrentFolder != null)
						//{
						//    _vm._binder.CurrentFolder.IsEditingCategories = false;
						//}
					}
				}
			}

			private string _catId = null;

			private BinderVM _vm = null;

			internal FolderCategorySelectorRow(BinderVM vm, string name, string catId, bool isOn)
			{
				_vm = vm;
				_name = name;
				_catId = catId;
				_isOn = isOn;
			}
		}
		public void UpdateCurrentFolderCategories()
		{
			if (_binder?.CurrentFolder?.DynamicCategories != null && MetaBriefcase.OpenInstance?.Categories != null)
			{
				_folderCategorySelector.Clear();
				foreach (var metaCat in MetaBriefcase.OpenInstance.Categories)
				{
					var newSelectorRow = new FolderCategorySelectorRow(this, metaCat.Name, metaCat.Id, _binder.CurrentFolder.DynamicCategories.Any(a => a.CategoryId == metaCat.Id));
					_folderCategorySelector.Add(newSelectorRow);
				}
			}
		}

		public void ToggleIsEditingCategories()
		{
			if (_binder?.CurrentFolder != null)
			{
				_binder.CurrentFolder.IsEditingCategories = !_binder.CurrentFolder.IsEditingCategories;
			}
		}
		#endregion edit categories

		#region edit field value
		public async Task<bool> ChangeFieldValueAsync(DynamicField dynFld, string newValue)
		{
			if (dynFld == null || dynFld.FieldDescription == null) return false;

			bool isOk = false;
			if (dynFld.FieldValue != null && dynFld.FieldValue.Vaalue != newValue)
			{
				var oldValue = dynFld.FieldValue.Vaalue;

				isOk = await dynFld.SetFieldValueIdAsync(newValue);

				if (!isOk) dynFld.FieldValue.Vaalue = oldValue;
			}
			else if (dynFld.FieldValue == null)
			{
				isOk = await dynFld.SetFieldValueIdAsync(newValue);
			}
			return isOk;
		}
		#endregion edit field value
	}
}

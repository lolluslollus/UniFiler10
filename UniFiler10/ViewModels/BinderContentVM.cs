using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Model;
using UniFiler10.Data.Runtime;
using Utilz;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace UniFiler10.ViewModels
{
	public sealed class BinderContentVM : OpenableObservableData, IAudioFileGetter
	{
		private Binder _binder = null;
		public Binder Binder { get { return _binder; } private set { _binder = value; RaisePropertyChanged_UI(); } }

		private RuntimeData _runtimeData = null;
		public RuntimeData RuntimeData { get { return _runtimeData; } private set { _runtimeData = value; RaisePropertyChanged_UI(); } }


		#region construct dispose open close
		public BinderContentVM()
		{
			RuntimeData = RuntimeData.Instance;
		}
		protected async override Task OpenMayOverrideAsync()
		{
			var briefcase = Briefcase.GetCreateInstance();
			await briefcase.OpenAsync();
			await briefcase.OpenCurrentBinderAsync();

			Binder = briefcase.CurrentBinder;
			if (_binder != null)
			{
				await _binder.OpenCurrentFolderAsync();
				_binder.PropertyChanged += OnBinder_PropertyChanged;
			}

			UpdateCurrentFolderCategories();
		}
		protected override Task CloseMayOverrideAsync()
		{
			var binder = _binder;
			if (binder != null) binder.PropertyChanged -= OnBinder_PropertyChanged;

			EndRecordAudio();
			EndShoot();

			// briefcase and other data model classes cannot be destroyed by view models. Only app.xaml may do so.
			Binder = null;

			return Task.CompletedTask;
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_folderCategorySelector?.Dispose();
			_folderCategorySelector = null;
		}
		private void OnBinder_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Binder.CurrentFolder) || e.PropertyName == nameof(Binder.IsOpen))
			{
				UpdateCurrentFolderCategories();
			}
		}
		#endregion construct dispose open close


		#region user actions
		//public async Task AddFolderAsync()
		//{
		//	var binder = _binder; if (binder == null) return;
		//	var folder = await binder.AddFolderAsync();
		//	await binder.SetCurrentFolderIdAsync(folder?.Id);
		//}
		//public Task DeleteFolderAsync(Folder folder)
		//{
		//	return _binder?.RemoveFolderAsync(folder);
		//}
		public Task AddWalletToFolderAsync(Folder folder)
		{
			return folder?.AddWalletAsync();
		}
		public Task<bool> RemoveWalletFromFolderAsync(Folder folder, Wallet wallet)
		{
			return folder?.RemoveWalletAsync(wallet);
		}
		public Task AddEmptyDocumentToWalletAsync(Wallet wallet)
		{
			return wallet?.AddDocumentAsync();
		}
		public async Task<bool> RemoveDocumentFromWalletAsync(Wallet wallet, Document doc)
		{
			if (wallet != null)
			{
				await wallet.OpenAsync();
				return await wallet.RemoveDocumentAsync(doc);
			}
			return false;
		}
		public Task SetCurrentFolderAsync(string folderId)
		{
			return _binder?.OpenFolderAsync(folderId);
		}
		public Task<bool> TrySetFieldValueAsync(DynamicField dynFld, string newValue)
		{
			return dynFld?.TrySetFieldValueAsync(newValue);
		}
		#endregion user actions


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
				await RunFunctionWhileOpenAsyncT(async delegate
				{
					IsCameraOverlayOpen = true; // opens the Camera control
					await _photoTriggerSemaphore.WaitAsync(); // wait until someone calls EndShoot

					await parentFolder.ImportMediaFileIntoNewWalletAsync(GetPhotoFile(), false).ConfigureAwait(false);
				}).ConfigureAwait(false);
			}
		}
		public async Task ShootAsync(Wallet parentWallet)
		{
			var binder = _binder;
			if (binder != null && binder.IsOpen && !_isCameraOverlayOpen && parentWallet != null && RuntimeData.Instance?.IsCameraAvailable == true)
			{
				await RunFunctionWhileOpenAsyncT(async delegate
				{
					IsCameraOverlayOpen = true; // opens the Camera control
					await _photoTriggerSemaphore.WaitAsync(); // wait until someone calls EndShoot

					await parentWallet.ImportMediaFileAsync(GetPhotoFile(), false).ConfigureAwait(false);
				}).ConfigureAwait(false);
			}
		}
		public void EndShoot()
		{
			SemaphoreSlimSafeRelease.TryRelease(_photoTriggerSemaphore);
			IsCameraOverlayOpen = false; // closes the Camera control
		}

		public async Task RecordAudioAsync(Folder parentFolder)
		{
			var binder = _binder;
			if (binder != null && binder.IsOpen && !_isAudioRecorderOverlayOpen && parentFolder != null && RuntimeData.Instance?.IsMicrophoneAvailable == true)
			{
				await RunFunctionWhileOpenAsyncT(async delegate
				{
					await CreateAudioFileAsync(); // required before we start any audio recording
					IsAudioRecorderOverlayOpen = true; // opens the AudioRecorder control
					await _audioTriggerSemaphore.WaitAsync(); // wait until someone calls EndRecordAudio

					await parentFolder.ImportMediaFileIntoNewWalletAsync(GetAudioFile(), false).ConfigureAwait(false);
				}).ConfigureAwait(false);
			}
		}
		public void EndRecordAudio()
		{
			SemaphoreSlimSafeRelease.TryRelease(_audioTriggerSemaphore);
			IsAudioRecorderOverlayOpen = false; // closes the AudioRecorder control
		}

		private static SemaphoreSlimSafeRelease _audioTriggerSemaphore = new SemaphoreSlimSafeRelease(0, 1); // this semaphore is red until explicitly released

		private StorageFile _audioFile = null;
		private async Task<StorageFile> CreateAudioFileAsync()
		{
			try
			{
				//var directory = ApplicationData.Current.LocalCacheFolder;
				_audioFile = await _binder.Directory.CreateFileAsync("Audio.mp3", CreationCollisionOption.GenerateUniqueName);
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

		private static SemaphoreSlimSafeRelease _photoTriggerSemaphore = new SemaphoreSlimSafeRelease(0, 1); // this semaphore is red until explicitly released

		private StorageFile _photoFile = null;
		public async Task<StorageFile> CreatePhotoFileAsync()
		{
			try
			{
				//var directory = ApplicationData.Current.LocalCacheFolder;
				var binder = _binder;
				if (binder != null)
				{
					var dir = _binder?.Directory;
					if (dir != null)
					{
						_photoFile = await dir.CreateFileAsync("Photo.jpeg", CreationCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);
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

			private BinderContentVM _vm = null;

			internal FolderCategorySelectorRow(BinderContentVM vm, string name, string catId, bool isOn)
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
		#endregion edit categories
	}
}

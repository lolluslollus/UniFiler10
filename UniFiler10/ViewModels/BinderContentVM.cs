using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Model;
using UniFiler10.Data.Runtime;
using UniFiler10.Views;
using Utilz;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace UniFiler10.ViewModels
{
	public sealed class BinderContentVM : OpenableObservableData
	{
		#region properties
		public const string DEFAULT_AUDIO_FILE_NAME = "Audio.mp3";
		public const string DEFAULT_PHOTO_FILE_NAME = "Photo.jpeg";

		private Binder _binder = null;
		public Binder Binder { get { return _binder; } private set { _binder = value; RaisePropertyChanged_UI(); } }

		private RuntimeData _runtimeData = null;
		public RuntimeData RuntimeData { get { return _runtimeData; } private set { _runtimeData = value; RaisePropertyChanged_UI(); } }

		private IRecorder _audioRecorder = null;
		private IRecorder _camera = null;
		#endregion properties


		#region construct dispose open close
		public BinderContentVM(IRecorder audioRecorder, IRecorder camera)
		{
			_audioRecorder = audioRecorder;
			_camera = camera;
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

			RuntimeData = RuntimeData.Instance;
			UpdateCurrentFolderCategories();
		}
		public override async Task<bool> CloseAsync()
		{
			if (!_isOpen) return false;

			var ar = _audioRecorder;
			if (ar != null)
			{
				await ar.CloseAsync().ConfigureAwait(false);
			}
			IsAudioRecorderOverlayOpen = false;

			var cam = _camera;
			if (cam != null)
			{
				await cam.CloseAsync().ConfigureAwait(false);
			}
			IsCameraOverlayOpen = false;


			return await base.CloseAsync();
		}
		protected override Task CloseMayOverrideAsync()
		{
			var binder = _binder;
			if (binder != null) binder.PropertyChanged -= OnBinder_PropertyChanged;

			// briefcase and other data model classes cannot be destroyed by view models. Only app.xaml may do so.
			_binder = null;

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
			if (binder?.IsOpen == true && parentFolder != null && !_isCameraOverlayOpen && RuntimeData.Instance?.IsCameraAvailable == true)
			{
				await RunFunctionWhileOpenAsyncT(async delegate
				{
					if (binder?.IsOpen == true && parentFolder != null && !_isCameraOverlayOpen && RuntimeData.Instance?.IsCameraAvailable == true)
					{
						var file = await CreateAudioPhotoFileAsync(DEFAULT_PHOTO_FILE_NAME);
						if (file != null)
						{
							IsCameraOverlayOpen = true;
							await _camera.OpenAsync();
							await _camera.StartAsync(file);
							await _camera.CloseAsync();
							await parentFolder.ImportMediaFileIntoNewWalletAsync(file, false).ConfigureAwait(false);
							IsCameraOverlayOpen = false;
						}
					}
				}).ConfigureAwait(false);
			}
		}
		public async Task ShootAsync(Wallet parentWallet)
		{
			var binder = _binder;
			if (binder?.IsOpen == true && parentWallet != null && !_isCameraOverlayOpen && RuntimeData.Instance?.IsCameraAvailable == true)
			{
				await RunFunctionWhileOpenAsyncT(async delegate
				{
					if (binder?.IsOpen == true && parentWallet != null && !_isCameraOverlayOpen && RuntimeData.Instance?.IsCameraAvailable == true)
					{
						var file = await CreateAudioPhotoFileAsync(DEFAULT_PHOTO_FILE_NAME);
						if (file != null)
						{
							IsCameraOverlayOpen = true;
							await _camera.OpenAsync();
							await _camera.StartAsync(file);
							await _camera.CloseAsync();
							await parentWallet.ImportMediaFileAsync(file, false).ConfigureAwait(false);
							IsCameraOverlayOpen = false;
						}
					}
				}).ConfigureAwait(false);
			}
		}

		public async Task RecordAudioAsync(Folder parentFolder)
		{
			var binder = _binder;
			if (binder?.IsOpen == true && parentFolder != null && !_isAudioRecorderOverlayOpen && RuntimeData.Instance?.IsMicrophoneAvailable == true)
			{
				await RunFunctionWhileOpenAsyncT(async delegate
				{
					if (binder?.IsOpen == true && parentFolder != null && !_isAudioRecorderOverlayOpen && RuntimeData.Instance?.IsMicrophoneAvailable == true)
					{
						var file = await CreateAudioPhotoFileAsync(DEFAULT_AUDIO_FILE_NAME);
						if (file != null)
						{
							IsAudioRecorderOverlayOpen = true;
							await _audioRecorder.OpenAsync();
							await _audioRecorder.StartAsync(file);
							await _audioRecorder.CloseAsync();
							await parentFolder.ImportMediaFileIntoNewWalletAsync(file, false).ConfigureAwait(false);
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
				var binder = _binder;
				if (binder != null)
				{
					var dir = binder.Directory;
					if (dir != null)
					{
						var file = await dir.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);
						return file;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return null;
		}
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

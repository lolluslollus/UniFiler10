using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Model;
using UniFiler10.Services;
using Utilz;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace UniFiler10.ViewModels
{
    public sealed class BinderVM : ObservableData, IDisposable
    {
        private Binder _binder = null;
        public Binder Binder { get { return _binder; } private set { _binder = value; RaisePropertyChanged_UI(); } }

        private RuntimeData _runtimeData = null;
        public RuntimeData RuntimeData { get { return _runtimeData; } private set { _runtimeData = value; RaisePropertyChanged_UI(); } }

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

        private SaveMedia _media = null;
        public SaveMedia Media { get { return _media; } }
        public BinderVM(Binder binder)
        {
            _media = new SaveMedia(this);
            Binder = binder;
            RuntimeData = RuntimeData.Instance;
            UpdateCurrentFolderCategories();
            Binder.PropertyChanged += OnBinder_PropertyChanged;
        }
        public void Dispose()
        {
            if (Binder != null) Binder.PropertyChanged -= OnBinder_PropertyChanged;
            _media?.Dispose();
            _media = null;
            ClearListeners();
        }
        private void OnBinder_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Binder.CurrentFolder)
                || e.PropertyName == nameof(Binder.IsOpen))
                UpdateCurrentFolderCategories();
        }

        public async Task AddFolderAsync()
        {
            if (_binder != null && _binder.IsOpen)
                await _binder.AddFolderAsync(new Folder()).ConfigureAwait(false);
        }
        public async Task DeleteFolderAsync(Folder folder)
        {
            if (_binder != null && _binder.IsOpen)
                await _binder.RemoveFolderAsync(folder).ConfigureAwait(false);
        }
        public async Task AddWalletToFolderAsync(Folder folder)
        {
            if (_binder != null && _binder.IsOpen && folder != null)
            {
                Wallet newWallet = new Wallet();
                await folder.AddWalletAsync(newWallet).ConfigureAwait(false);
            }
        }
        public async Task RemoveWalletFromFolderAsync(Folder folder, Wallet wallet)
        {
            if (_binder != null && _binder.IsOpen && folder != null && wallet != null)
            {
                bool isOk = await folder.RemoveWalletAsync(wallet).ConfigureAwait(false);
            }
        }
        public async Task AddEmptyDocumentToWalletAsync(Wallet wallet)
        {
            if (_binder != null && _binder.IsOpen && wallet != null)
            {
                Document newDoc = new Document();
                await wallet.AddDocumentAsync(newDoc).ConfigureAwait(false);
            }
        }
        public async Task RemoveDocumentFromWalletAsync(Wallet wallet, Document doc)
        {
            if (_binder != null && _binder.IsOpen && wallet != null)
            {
                await wallet.RemoveDocumentAsync(doc).ConfigureAwait(false);
            }
        }

        #region save media
        public class SaveMedia : IAudioFileGetter, IDisposable
        {
            private BinderVM _vm = null;
            internal SaveMedia(BinderVM vm)
            {
                _vm = vm;
            }
            public void Dispose()
            {
                EndRecordAudio();
                EndShoot();
            }
            public async Task LoadMediaFile(Folder parentFolder)
            {
                if (_vm._binder != null && parentFolder != null)
                {
                    await _vm._binder.RunFunctionWhileOpenAsyncT(async delegate
                    {
                        if (parentFolder != null)
                        {
                            var file = await PickMediaFile();
                            await parentFolder.ImportMediaFileIntoNewWalletAsync(file, true).ConfigureAwait(false);
                        }
                    }).ConfigureAwait(false);
                }
            }
            public async Task LoadMediaFile(Wallet parentWallet)
            {
                if (_vm._binder != null && parentWallet != null)
                {
                    await _vm._binder.RunFunctionWhileOpenAsyncT(async delegate
                    {
                        if (parentWallet != null)
                        {
                            var file = await PickMediaFile();
                            await parentWallet.ImportMediaFileIntoNewWalletAsync(file, true).ConfigureAwait(false);
                        }
                    }).ConfigureAwait(false);
                }
            }

            public async Task ShootAsync(Folder parentFolder)
            {
                if (_vm._binder != null && !_vm._isCameraOverlayOpen && parentFolder != null && RuntimeData.Instance?.IsCameraAvailable == true)
                {
                    await _vm._binder.RunFunctionWhileOpenAsyncT(async delegate
                    {
                        if (!_vm._isCameraOverlayOpen && parentFolder != null && RuntimeData.Instance?.IsCameraAvailable == true)
                        {
                            _vm.IsCameraOverlayOpen = true; // opens the Camera control
                            await _photoSemaphore.WaitAsync(); // wait until someone calls EndShoot

                            await parentFolder.ImportMediaFileIntoNewWalletAsync(GetPhotoFile(), false).ConfigureAwait(false);
                        }
                    }).ConfigureAwait(false);
                }
            }
            public async Task ShootAsync(Wallet parentWallet)
            {
                if (_vm._binder != null && !_vm._isCameraOverlayOpen && parentWallet != null && RuntimeData.Instance?.IsCameraAvailable == true)
                {
                    await _vm._binder.RunFunctionWhileOpenAsyncT(async delegate
                    {
                        if (!_vm._isCameraOverlayOpen && parentWallet != null && RuntimeData.Instance?.IsCameraAvailable == true)
                        {
                            _vm.IsCameraOverlayOpen = true; // opens the Camera control
                            await _photoSemaphore.WaitAsync(); // wait until someone calls EndShoot

                            await parentWallet.ImportMediaFileIntoNewWalletAsync(GetPhotoFile(), false).ConfigureAwait(false);
                        }
                    }).ConfigureAwait(false);
                }
            }
            public void EndShoot()
            {
                SemaphoreSlimSafeRelease.TryRelease(_photoSemaphore);
            }

            public async Task RecordAudioAsync(Folder parentFolder)
            {
                if (_vm._binder != null && !_vm._isAudioRecorderOverlayOpen && parentFolder != null && RuntimeData.Instance?.IsMicrophoneAvailable == true)
                {
                    await _vm._binder.RunFunctionWhileOpenAsyncT(async delegate
                    {
                        if (!_vm._isAudioRecorderOverlayOpen && parentFolder != null && RuntimeData.Instance?.IsMicrophoneAvailable == true)
                        {
                            await CreateAudioFileAsync(); // required before we start any audio recording
                            _vm.IsAudioRecorderOverlayOpen = true; // opens the AudioRecorder control
                            await _audioSemaphore.WaitAsync(); // wait until someone calls EndRecordAudio

                            await parentFolder.ImportMediaFileIntoNewWalletAsync(GetAudioFile(), false).ConfigureAwait(false);
                        }
                    }).ConfigureAwait(false);
                }
            }
            public void EndRecordAudio()
            {
                SemaphoreSlimSafeRelease.TryRelease(_audioSemaphore);
            }

            private async Task<StorageFile> PickMediaFile()
            {
                FileOpenPicker openPicker = new FileOpenPicker();
                openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                //openPicker.CommitButtonText=
                //openPicker.ViewMode = PickerViewMode.List;
                openPicker.FileTypeFilter.Add(".pdf");
                openPicker.FileTypeFilter.Add(".jpg");
                openPicker.FileTypeFilter.Add(".jpeg");
                openPicker.FileTypeFilter.Add(".png");
                openPicker.FileTypeFilter.Add(".bmp");
                openPicker.FileTypeFilter.Add(".tif");
                openPicker.FileTypeFilter.Add(".tiff");

                var file = await openPicker.PickSingleFileAsync();
                return file;
            }

            private static SemaphoreSlimSafeRelease _audioSemaphore = new SemaphoreSlimSafeRelease(0, 1); // this semaphore is red until explicitly released
            private StorageFile _audioFile = null;
            private async Task<StorageFile> CreateAudioFileAsync()
            {
                try
                {
                    //var directory = ApplicationData.Current.LocalCacheFolder;
                    var directory = await _vm._binder.GetDirectoryAsync();
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
                    var directory = await _vm._binder.GetDirectoryAsync();
                    _photoFile = await directory.CreateFileAsync("Photo.jpeg", CreationCollisionOption.GenerateUniqueName);
                    return _photoFile;
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
        }
        #endregion save media

        public readonly SwitchableObservableCollection<FolderCategorySelectorRow> _folderCategorySelector = new SwitchableObservableCollection<FolderCategorySelectorRow>();
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
                            Task upd = _vm.Binder.CurrentFolder.AddDynamicCategoryAsync(_catId);
                        }
                        else
                        {
                            Task upd = _vm.Binder.CurrentFolder.RemoveDynamicCategoryAsync(_catId);
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

        public bool ChangeFieldValue(DynamicField dynFld, string newValue)
        {
            if (dynFld == null) return false;

            if (dynFld.FieldDescription != null && dynFld.FieldDescription.IsAnyValueAllowed && dynFld.FieldValue != null && dynFld.FieldValue.Vaalue != newValue)
            {
                var oldValue = dynFld.FieldValue.Vaalue;
                if (string.IsNullOrWhiteSpace(newValue))
                {
                    dynFld.FieldValueId = null;
                    return true;
                }
                var newFldVal = new FieldValue() { IsCustom = true, IsJustAdded = true, Vaalue = newValue };
                if (dynFld.FieldDescription.AddPossibleValue(newFldVal))
                {
                    dynFld.FieldValueId = newFldVal.Id;
                    return true;
                }
                else
                {
                    dynFld.FieldValue.Vaalue = oldValue;
                }
            }
            return false;
        }
    }
}

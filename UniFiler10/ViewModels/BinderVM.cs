using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using UniFiler10.Data.Metadata;
using Utilz;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;
using Windows.ApplicationModel.Core;
using UniFiler10.Views;
using Windows.Storage.Streams;
using Windows.Storage.FileProperties;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Foundation;
using System.IO;
using Windows.Storage.Pickers;
using UniFiler10.Services;

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

        public BinderVM(Binder binder)
        {
            Binder = binder;
            RuntimeData = RuntimeData.Instance;
            UpdateCurrentFolderCategories();
            Binder.PropertyChanged += OnBinder_PropertyChanged;
        }
        public void Dispose()
        {
            if (Binder != null) Binder.PropertyChanged -= OnBinder_PropertyChanged;
            //EndRecordSound();
            SemaphoreSlimSafeRelease.TryRelease(_audioRecorderSemaphore);
            SemaphoreSlimSafeRelease.TryRelease(_shootSemaphore);
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
        public async Task LoadMediaFile(Folder parentFolder)
        {
            if (_binder != null && parentFolder != null)
            {
                await _binder.RunFunctionWhileOpenAsyncT(async delegate
                {
                    var file = await PickMediaFile();
                    await parentFolder.ImportMediaFileIntoNewWalletAsync(file).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }
        public async Task LoadMediaFile(Wallet parentWallet)
        {
            if (_binder != null && _binder.IsOpen && parentWallet != null)
            {
                await _binder.RunFunctionWhileOpenAsyncT(async delegate
                {
                    var file = await PickMediaFile();
                    await parentWallet.ImportMediaFileIntoNewWalletAsync(file).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        public async Task ShootAsync(Folder parentFolder)
        {
            if (_binder != null && !_isCameraOverlayOpen && parentFolder != null && RuntimeData.Instance?.IsCameraAvailable == true)
            {
                await _binder.RunFunctionWhileOpenAsyncT(async delegate
                {
                    IsCameraOverlayOpen = true;
                    await _shootSemaphore.WaitAsync();

                    var file = await GetShotFileAsync();
                    await parentFolder.ImportMediaFileIntoNewWalletAsync(file).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }
        public async Task ShootAsync(Wallet parentWallet)
        {
            if (_binder != null && !_isCameraOverlayOpen && parentWallet != null && RuntimeData.Instance?.IsCameraAvailable == true)
            {
                await _binder.RunFunctionWhileOpenAsyncT(async delegate
                {
                    IsCameraOverlayOpen = true;
                    await _shootSemaphore.WaitAsync();

                    var file = await GetShotFileAsync();
                    await parentWallet.ImportMediaFileIntoNewWalletAsync(file).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }
        public void EndShoot(IRandomAccessStream stream, PhotoOrientation photoOrientation = PhotoOrientation.Normal)
        {
            _shootStream = stream;
            _shootPhotoOrientation = photoOrientation;
            SemaphoreSlimSafeRelease.TryRelease(_shootSemaphore);
        }
        public void EndAudioRecorder()
        {
            SemaphoreSlimSafeRelease.TryRelease(_audioRecorderSemaphore);
        }

        public async Task RecordAudioAsync(Folder parentFolder)
        {
            if (_binder != null && !_isAudioRecorderOverlayOpen && parentFolder != null && RuntimeData.Instance?.IsMicrophoneAvailable == true)
            {
                await _binder.RunFunctionWhileOpenAsyncT(async delegate
                {
                    var directory = ApplicationData.Current.LocalCacheFolder;
                    _audioRecorderFile = await directory.CreateFileAsync("Audio.mp3", CreationCollisionOption.GenerateUniqueName);
                    IsAudioRecorderOverlayOpen = true;
                    await _audioRecorderSemaphore.WaitAsync();

                    await parentFolder.ImportMediaFileIntoNewWalletAsync(_audioRecorderFile).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
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
        private static SemaphoreSlimSafeRelease _audioRecorderSemaphore = new SemaphoreSlimSafeRelease(0, 1); // this semaphore is red until explicitly released
        private StorageFile _audioRecorderFile = null;
        public StorageFile AudioRecorderFile { get { return _audioRecorderFile; } }
        private static SemaphoreSlimSafeRelease _shootSemaphore = new SemaphoreSlimSafeRelease(0, 1); // this semaphore is red until explicitly released
        private IRandomAccessStream _shootStream = null;
        private PhotoOrientation _shootPhotoOrientation = PhotoOrientation.Normal;

        private async Task<StorageFile> GetShotFileAsync()
        {
            if (_shootStream != null)
            {
                try
                {
                    var directory = await _binder.GetDirectoryAsync();
                    var file = await directory.CreateFileAsync("Photo.jpeg", CreationCollisionOption.GenerateUniqueName);

                    var decoder = await BitmapDecoder.CreateAsync(_shootStream);
                    using (var outputStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);

                        var properties = new BitmapPropertySet { { "System.Photo.Orientation", new BitmapTypedValue(_shootPhotoOrientation, PropertyType.UInt16) } };

                        await encoder.BitmapProperties.SetPropertiesAsync(properties);
                        await encoder.FlushAsync();
                    }
                    return file;
                }
                catch (Exception ex)
                {
                    Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                }
                finally
                {
                    _shootStream?.Dispose();
                    _shootStream = null;
                }
            }
            return null;
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

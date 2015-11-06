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
            _audioRecorder?.Dispose(); _audioRecorder = null;
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
        private object _newWalletOrDocument = null;
        private object _parentOfNewWalletOrDocument = null;
        private AudioRecorder _audioRecorder = null;
        private StorageFile _audioFile = null;

        public async Task LoadMediaFile(Folder parentFolder)
        {
            if (_binder != null && _binder.IsOpen && parentFolder != null)
            {
                _newWalletOrDocument = new Wallet();
                _parentOfNewWalletOrDocument = parentFolder;

                var file = await PickMediaFile();
                await ImportMediaFile(file);
            }
        }
        public async Task LoadMediaFile(Wallet parentWallet)
        {
            if (_binder != null && _binder.IsOpen && parentWallet != null)
            {
                _newWalletOrDocument = new Document();
                _parentOfNewWalletOrDocument = parentWallet;

                var file = await PickMediaFile();
                await ImportMediaFile(file);
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

        private async Task ImportMediaFile(StorageFile file)
        {
            if (file != null)
            {
                await _binder.RunFunctionWhileOpenAsyncT(async delegate
                {
                    var directory = await _binder.GetDirectoryAsync();
                    var newFile = await file.CopyAsync(directory, file.Name, NameCollisionOption.GenerateUniqueName);
                    await AddFileToInfoDataAsync(newFile);
                }).ConfigureAwait(false);
            }
        }
        public async Task StartShootAsync(Folder parentFolder)
        {
            if (_binder != null && !_isCameraOverlayOpen && parentFolder != null && RuntimeData.Instance?.IsCameraAvailable == true)
            {
                await _binder.RunFunctionWhileOpenAsyncA(delegate
                {
                    _newWalletOrDocument = new Wallet();
                    _parentOfNewWalletOrDocument = parentFolder;
                    IsCameraOverlayOpen = true;
                }).ConfigureAwait(false);
            }
        }
        public async Task StartShootAsync(Wallet parentWallet)
        {
            if (_binder != null && !_isCameraOverlayOpen && parentWallet != null && RuntimeData.Instance?.IsCameraAvailable == true)
            {
                await _binder.RunFunctionWhileOpenAsyncA(delegate
                {
                    _newWalletOrDocument = new Document();
                    _parentOfNewWalletOrDocument = parentWallet;
                    IsCameraOverlayOpen = true;
                }).ConfigureAwait(false);
            }
        }
        public async Task StartRecordSoundAsync(Folder parentFolder)
        {
            if (_binder != null && parentFolder != null && RuntimeData.Instance?.IsMicrophoneAvailable == true)
            {
                await _binder.RunFunctionWhileOpenAsyncA(async delegate
                {
                    _newWalletOrDocument = new Wallet() { IsRecordingSound = true };
                    _parentOfNewWalletOrDocument = parentFolder;

                    if (await (_parentOfNewWalletOrDocument as Folder).AddWalletAsync(_newWalletOrDocument as Wallet))
                    {
                        //await _audioRecorder.OpenAsync().ConfigureAwait(false);

                        var directory = await _binder.GetDirectoryAsync().ConfigureAwait(false);
                        _audioFile = await directory.CreateFileAsync("Sound.mp3", CreationCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);

                        _audioRecorder = new AudioRecorder();
                        //await _audioRecorder.SetFileAsync(_audioFile).ConfigureAwait(false);
                        if (await _audioRecorder.OpenAsync(_audioFile).ConfigureAwait(false))
                        {
                            await _audioRecorder.RecordStartAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            if (_audioFile != null) await _audioFile.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
                        }
                    }
                }).ConfigureAwait(false);
            }
        }

        public async Task EndShootAsync(IRandomAccessStream stream, PhotoOrientation photoOrientation = PhotoOrientation.Normal)
        {
            if (_binder != null && _newWalletOrDocument != null && stream != null)
            {
                await _binder.RunFunctionWhileOpenAsyncT(async delegate
                {
                    using (var inputStream = stream)
                    {
                        var directory = await _binder.GetDirectoryAsync();
                        var file = await directory.CreateFileAsync("Photo.jpeg", CreationCollisionOption.GenerateUniqueName);

                        var decoder = await BitmapDecoder.CreateAsync(inputStream);
                        using (var outputStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);

                            var properties = new BitmapPropertySet { { "System.Photo.Orientation", new BitmapTypedValue(photoOrientation, PropertyType.UInt16) } };

                            await encoder.BitmapProperties.SetPropertiesAsync(properties);
                            await encoder.FlushAsync();
                        }

                        await AddFileToInfoDataAsync(file).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
            }
        }

        public async Task EndRecordSoundAsync()
        {
            if (_newWalletOrDocument is Wallet)
            {
                (_newWalletOrDocument as Wallet).IsRecordingSound = false;

                if (_binder != null && _audioRecorder != null && _audioFile != null)
                {
                    await _binder.RunFunctionWhileOpenAsyncT(async delegate
                    {
                        await _audioRecorder.RecordStopAsync(); // .ConfigureAwait(true)
                        await AddFileToWalletAsync(_audioFile).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                }
            }
        }
        private async Task AddFileToInfoDataAsync(StorageFile file)
        {
            if (file != null)
            {
                if (_newWalletOrDocument is Wallet)
                {
                    if (await (_parentOfNewWalletOrDocument as Folder).AddWalletAsync(_newWalletOrDocument as Wallet))
                    {
                        await (_newWalletOrDocument as Wallet).AddDocumentAsync(new Document() { Uri0 = file.Path });
                    }
                }
                else if (_newWalletOrDocument is Document)
                {
                    (_newWalletOrDocument as Document).Uri0 = file.Path;
                    await (_parentOfNewWalletOrDocument as Wallet).AddDocumentAsync(_newWalletOrDocument as Document);
                }
            }
            _newWalletOrDocument = null;
            _parentOfNewWalletOrDocument = null;
        }
        private async Task AddFileToWalletAsync(StorageFile file)
        {
            if (_binder != null && _binder.IsOpen && file != null)
            {
                if (_newWalletOrDocument is Wallet)
                {
                    await (_newWalletOrDocument as Wallet).AddDocumentAsync(new Document() { Uri0 = file.Path });
                }
            }
            _newWalletOrDocument = null;
            _parentOfNewWalletOrDocument = null;
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

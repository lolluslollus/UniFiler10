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

namespace UniFiler10.ViewModels
{
    public sealed class BinderVM : ObservableData, IDisposable
    {
        private Binder _binder = null;
        public Binder Binder { get { return _binder; } private set { _binder = value; RaisePropertyChanged_UI(); } }
        public BinderVM(Binder binder)
        {
            Binder = binder;
            UpdateCurrentFolderCategories();
            Binder.PropertyChanged += OnBinder_PropertyChanged;
        }
        public void Dispose()
        {
            if (Binder != null) Binder.PropertyChanged -= OnBinder_PropertyChanged;
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
                await folder.RemoveWalletAsync(wallet).ConfigureAwait(false);
            }
        }
        public async Task AddDocumentToWalletAsync(Wallet wallet)
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

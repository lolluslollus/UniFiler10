using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace UniFiler10.ViewModels
{
    public class BinderCoverVM : OpenableObservableData
    {
        private Binder _binder = null;
        public Binder Binder { get { return _binder; } private set { _binder = value; RaisePropertyChanged_UI(); } }

        private SwitchableObservableCollection<FolderPreview> _allFolderPreviews = new SwitchableObservableCollection<FolderPreview>();
        public SwitchableObservableCollection<FolderPreview> AllFolderPreviews { get { return _allFolderPreviews; } private set { _allFolderPreviews = value; RaisePropertyChanged_UI(); } }

        private bool _isAllFolderPaneOpen = false;
        public bool IsAllFoldersPaneOpen { get { return _isAllFolderPaneOpen; } set { _isAllFolderPaneOpen = value; RaisePropertyChanged_UI(); } }

        public class FolderPreview : ObservableData
        {
            protected string _folderId = string.Empty;
            public string FolderId { get { return _folderId; } set { if (_folderId != value) { _folderId = value; RaisePropertyChanged_UI(); } } }

            private string _name = string.Empty;
            public string Name { get { return _name; } set { if (_name != value) { _name = value; RaisePropertyChanged_UI(); } } }

            private string _uri0 = string.Empty;
            public string Uri0 { get { return _uri0; } set { if (_uri0 != value) { _uri0 = value; RaisePropertyChanged_UI(); } } }

            private Document _document = null;
            public Document Document { get { return _document; } set { _document = value; RaisePropertyChanged_UI(); } }
        }

        #region construct dispose open close
        public BinderCoverVM(Binder binder)
        {
            if (binder == null) throw new ArgumentNullException("BinderCoverVM ctor: binder may not be null");

            Binder = binder;
            //RuntimeData = RuntimeData.Instance;
            //UpdateCurrentFolderCategories();
            UpdateOpenClose();
            //UpdateUri();
        }

        protected override Task OpenMayOverrideAsync()
        {
            _binder.PropertyChanged += OnBinder_PropertyChanged;
            return Task.CompletedTask;
        }

        protected override Task CloseMayOverrideAsync()
        {
            if (_binder != null)
            {
                _binder.PropertyChanged -= OnBinder_PropertyChanged;
                _binder.IsCoverOpen = false; // LOLLO TODO it makes no sense to serialise this property since we always st it automatically.
            }
            return Task.CompletedTask;
        }
        private void OnBinder_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Binder.IsOpen))
            {
                UpdateOpenClose();
            }
            //else if (e.PropertyName == nameof(Binder.Uri0))
            //{
            //    UpdateUri();
            //}
        }
        private void UpdateOpenClose()
        {
            if (_binder.IsOpen)
            {
                Task open = OpenAsync();
            }
            else
            {
                Task close = CloseAsync();
            }
        }
        //private void UpdateUri()
        //{
        //    if (!string.IsNullOrWhiteSpace(_document?.Uri0))
        //    {

        //    }
        //}
        #endregion construct dispose open close

        #region actions
        public Task CloseCoverAsync()
        {
            return RunFunctionWhileOpenAsyncA(delegate
            {
                if (_binder != null) _binder.IsCoverOpen = false;
            });
        }

        public Task ReadAllFoldersAsync()
        {
            return _binder?.RunFunctionWhileOpenAsyncT(async delegate
            {
                if (!IsAllFoldersPaneOpen) return;

                var folders = await _binder.DbManager.GetFoldersAsync().ConfigureAwait(false);
                var wallets = await _binder.DbManager.GetWalletsAsync().ConfigureAwait(false);
                var documents = await _binder.DbManager.GetDocumentsAsync().ConfigureAwait(false);

                var folderPreviews = new List<FolderPreview>();

                foreach (var fol in folders)
                {
                    var folderPreview = new FolderPreview() { Name = fol.Name, FolderId = fol.Id };
                    bool exit = false;
                    foreach (var wal in wallets.Where(w => w.ParentId == fol.Id))
                    {
                        foreach (var doc in documents.Where(d => d.ParentId == wal.Id))
                        {
                            if (!string.IsNullOrWhiteSpace(doc.Uri0))
                            {
                                //var file = await StorageFile.GetFileFromPathAsync(doc.Uri0).AsTask().ConfigureAwait(false);
                                //{
                                //    if (file != null)
                                //    {
                                //doc.OpenAsync();
                                folderPreview.Uri0 = doc.Uri0;
                                folderPreview.Document = doc;
                                exit = true;
                                //    }
                                //}
                            }
                            if (exit) break;
                        }
                        if (exit) break;
                    }
                    folderPreviews.Add(folderPreview);
                }

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
                {
                    _allFolderPreviews.Clear();
                    _allFolderPreviews.AddRange(folderPreviews);
                }).AsTask().ConfigureAwait(false);
            });
        }
        //public Task ReadAllFoldersAsync()
        //{
        //    return _binder?.RunFunctionWhileOpenAsyncA(delegate
        //    {
        //        List<FolderPreview> newAllFolderPreviews = new List<FolderPreview>();
        //        foreach (var fol in _binder.Folders)
        //        {
        //            bool exit = false;
        //            var prev = new FolderPreview() { Name = fol.Name };
        //            foreach (var wal in fol.Wallets)
        //            {
        //                foreach (var doc in wal.Documents)
        //                {
        //                    if (!string.IsNullOrWhiteSpace(doc.Uri0))
        //                    { // LOLLO TODO you may need to open the folders to get their previews
        //                        prev.Uri0 = doc.Uri0;
        //                        prev.Document = doc;
        //                        exit = true;
        //                    }
        //                    if (exit) break;
        //                }
        //                if (exit) break;
        //            }
        //            if (prev.Document != null) newAllFolderPreviews.Add(prev);
        //        }
        //        AllFolderPreviews.Clear();
        //        AllFolderPreviews.AddRange(newAllFolderPreviews);
        //    });
        //}
        public Task SelectFolderAsync(string folderId)
        {
            return _binder.RunFunctionWhileOpenAsyncA(delegate { _binder.CurrentFolderId = folderId; _binder.IsCoverOpen = false; });
        }

        #endregion actions
    }
}

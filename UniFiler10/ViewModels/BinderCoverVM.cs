using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using UniFiler10.Utilz;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace UniFiler10.ViewModels
{
    public class BinderCoverVM : OpenableObservableData
    {
        #region properties
        private const int HOW_MANY_IN_RECENT = 10;
        private const int REFRESH_INTERVAL_LONG_MSEC = 5000;
        private const int REFRESH_INTERVAL_SHORT_MSEC = 25;

        private Binder _binder = null;
        public Binder Binder { get { return _binder; } private set { _binder = value; RaisePropertyChanged_UI(); } }

        private SwitchableObservableCollection<FolderPreview> _allFolderPreviews = new SwitchableObservableCollection<FolderPreview>();
        public SwitchableObservableCollection<FolderPreview> AllFolderPreviews { get { return _allFolderPreviews; } private set { _allFolderPreviews = value; RaisePropertyChanged_UI(); } }

        private bool _isAllFolderPaneOpen = false;
        public bool IsAllFoldersPaneOpen { get { return _isAllFolderPaneOpen; } set { _isAllFolderPaneOpen = value; RaisePropertyChanged_UI(); if (_isAllFolderPaneOpen) { Task upd = UpdateFoldersAsync(0); } } }

        private SwitchableObservableCollection<FolderPreview> _recentFolderPreviews = new SwitchableObservableCollection<FolderPreview>();
        public SwitchableObservableCollection<FolderPreview> RecentFolderPreviews { get { return _recentFolderPreviews; } private set { _recentFolderPreviews = value; RaisePropertyChanged_UI(); } }

        private bool _isRecentFolderPaneOpen = false;
        public bool IsRecentFoldersPaneOpen { get { return _isRecentFolderPaneOpen; } set { _isRecentFolderPaneOpen = value; RaisePropertyChanged_UI(); if (_isRecentFolderPaneOpen) { Task upd = UpdateFoldersAsync(0); } } }

        public class FolderPreview : ObservableData
        {
            protected string _folderId = string.Empty;
            public string FolderId { get { return _folderId; } set { if (_folderId != value) { _folderId = value; RaisePropertyChanged_UI(); } } }

            private string _folderName = string.Empty;
            public string FolderName { get { return _folderName; } set { if (_folderName != value) { _folderName = value; RaisePropertyChanged_UI(); } } }

            private string _documentUri0 = string.Empty;
            public string DocumentUri0 { get { return _documentUri0; } set { if (_documentUri0 != value) { _documentUri0 = value; RaisePropertyChanged_UI(); } } }

            private Document _document = null;
            public Document Document { get { return _document; } set { _document = value; RaisePropertyChanged_UI(); } }
        }

        IAnimationStarter _animationStarter = null;
        #endregion properties


        #region construct dispose open close
        public BinderCoverVM(Binder binder, IAnimationStarter animationStarter)
        {
            if (binder == null) throw new ArgumentNullException("BinderCoverVM ctor: binder may not be null");
            if(animationStarter==null) throw new ArgumentNullException("BinderCoverVM ctor: animationStarter may not be null");

            Binder = binder;
            _animationStarter = animationStarter;

            UpdateOpenClose();
        }

        protected override Task OpenMayOverrideAsync()
        {
            _binder.PropertyChanged += OnBinder_PropertyChanged;
            RegisterFolderChanged();
            IsRecentFoldersPaneOpen = true;

            _refreshIntervalMsec = REFRESH_INTERVAL_SHORT_MSEC;
            SetIsDirty(true);

            return Task.CompletedTask;
        }

        protected override Task CloseMayOverrideAsync()
        {
            if (_binder != null)
            {
                _binder.PropertyChanged -= OnBinder_PropertyChanged;
            }
            UnregisterFolderChanged();
            return Task.CompletedTask;
        }
        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            _allFolderPreviews?.Dispose();
            _allFolderPreviews = null;

            _recentFolderPreviews?.Dispose();
            _recentFolderPreviews = null;
        }
        private void OnBinder_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Binder.IsOpen))
            {
                UpdateOpenClose();
            }
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
        #endregion construct dispose open close


        #region binder data
        private int _refreshIntervalMsec = REFRESH_INTERVAL_LONG_MSEC;

        private bool IsAnyDirty { get { return _isAllFoldersDirty || _isRecentFoldersDirty; } }
        private void SetIsDirty(bool newValue)
        {
            IsAllFoldersDirty = newValue;
            IsRecentFoldersDirty = newValue;
        }
        private bool IsAnyPaneOpen { get { return _isAllFolderPaneOpen || _isRecentFolderPaneOpen; } }

        private bool _isAllFoldersDirty = false;
        private bool IsAllFoldersDirty
        {
            get
            {
                return _isAllFoldersDirty;
            }
            set
            {
                if (_isAllFoldersDirty != value)
                {
                    _isAllFoldersDirty = value;
                    RaisePropertyChanged_UI();
                    // Task refr = RefreshAllFoldersAsync(_refreshIntervalMsec);
                    Task upd = UpdateFoldersAsync(_refreshIntervalMsec);
                }
            }
        }

        private bool _isRecentFoldersDirty = false;
        private bool IsRecentFoldersDirty
        {
            get
            {
                return _isRecentFoldersDirty;
            }
            set
            {
                if (_isRecentFoldersDirty != value)
                {
                    _isRecentFoldersDirty = value;
                    RaisePropertyChanged_UI();
                    // Task refr = RefreshRecentFoldersAsync(_refreshIntervalMsec);
                    Task upd = UpdateFoldersAsync(_refreshIntervalMsec);
                }
            }
        }

        private static SemaphoreSlimSafeRelease _refreshFoldersSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        public async Task UpdateFoldersAsync(int waitMsec)
        {
            _refreshIntervalMsec = REFRESH_INTERVAL_LONG_MSEC;

            if (IsAnyDirty && IsAnyPaneOpen)
            {
                try
                {
                    _animationStarter.StartAnimation();
                    await Task.Delay(waitMsec).ConfigureAwait(false);
                    Debug.WriteLine("Finished waiting " + waitMsec + " msec");

                    await _refreshFoldersSemaphore.WaitAsync().ConfigureAwait(false);
                    if (IsAnyDirty && IsAnyPaneOpen)
                    {
                        //_animationStarter.StartAnimation();
                        //await Task.Delay(waitMsec).ConfigureAwait(false);
                        //Debug.WriteLine("Finished waiting " + waitMsec + " msec");

                        if (_isAllFoldersDirty && _isAllFolderPaneOpen)
                        {
                            await Task.Run(delegate { return ReadAllFoldersAsync(); }).ConfigureAwait(false);
                            IsAllFoldersDirty = false;
                        }
                        if (_isRecentFoldersDirty && _isRecentFolderPaneOpen)
                        {
                            await Task.Run(delegate { return ReadRecentFoldersAsync(); }).ConfigureAwait(false);
                            IsRecentFoldersDirty = false;
                        }
                    }
                }
                finally
                {
                    _animationStarter.EndAnimation();
                    SemaphoreSlimSafeRelease.TryRelease(_refreshFoldersSemaphore);
                }
            }
        }

        private Task ReadAllFoldersAsync()
        {
            return _binder?.RunFunctionWhileOpenAsyncT(async delegate
            {
                if (!IsAllFoldersPaneOpen) return;

                var folders = await _binder.DbManager.GetFoldersAsync().ConfigureAwait(false);
                var wallets = await _binder.DbManager.GetWalletsAsync().ConfigureAwait(false);
                var documents = await _binder.DbManager.GetDocumentsAsync().ConfigureAwait(false);

                var folderPreviews = GetFolderPreviews(folders, wallets, documents);

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
                {
                    _allFolderPreviews.Clear();
                    _allFolderPreviews.AddRange(folderPreviews);
                }).AsTask().ConfigureAwait(false);
            });
        }

        private Task ReadRecentFoldersAsync()
        {
            return _binder?.RunFunctionWhileOpenAsyncT(async delegate
            {
                if (!IsRecentFoldersPaneOpen) return;

                var folders = (await _binder.DbManager.GetFoldersAsync().ConfigureAwait(false)).OrderByDescending(ff => ff.DateCreated).Take(HOW_MANY_IN_RECENT);
                var wallets = await _binder.DbManager.GetWalletsAsync().ConfigureAwait(false);
                var documents = await _binder.DbManager.GetDocumentsAsync().ConfigureAwait(false);

                var folderPreviews = GetFolderPreviews(folders, wallets, documents);

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
                {
                    _recentFolderPreviews.Clear();
                    _recentFolderPreviews.AddRange(folderPreviews);
                }).AsTask().ConfigureAwait(false);
            });
        }
        #endregion binder data


        #region binder data events
        private void RegisterFolderChanged()
        {
            if (_binder?.Folders != null)
            {
                _binder.Folders.CollectionChanged += OnFol_CollectionChanged;
                foreach (Folder fol in _binder.Folders)
                {
                    fol.PropertyChanged += OnFol_PropertyChanged;
                    fol.Wallets.CollectionChanged += OnFolWal_CollectionChanged;
                    foreach (Wallet wal in fol.Wallets)
                    {
                        wal.Documents.CollectionChanged += OnFolWalDoc_CollectionChanged;
                        foreach (Document doc in wal.Documents)
                        {
                            doc.PropertyChanged += OnFolWalDoc_PropertyChanged;
                        }
                    }
                }
            }
        }
        private void UnregisterFolderChanged()
        {
            SetIsDirty(false);

            if (_binder?.Folders != null)
            {
                _binder.Folders.CollectionChanged -= OnFol_CollectionChanged;
                foreach (Folder fol in _binder.Folders)
                {
                    fol.PropertyChanged -= OnFol_PropertyChanged;
                    fol.Wallets.CollectionChanged -= OnFolWal_CollectionChanged;
                    foreach (Wallet wal in fol.Wallets)
                    {
                        wal.Documents.CollectionChanged -= OnFolWalDoc_CollectionChanged;
                        foreach (Document doc in wal.Documents)
                        {
                            doc.PropertyChanged -= OnFolWalDoc_PropertyChanged;
                        }
                    }
                }
            }
        }
        private void OnFol_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null) foreach (Folder fol in e.OldItems)
                {
                    fol.PropertyChanged -= OnFol_PropertyChanged;
                    fol.Wallets.CollectionChanged -= OnFolWal_CollectionChanged;
                    SetIsDirty(true);
                }
            if (e.NewItems != null) foreach (Folder fol in e.NewItems)
                {
                    fol.PropertyChanged += OnFol_PropertyChanged;
                    fol.Wallets.CollectionChanged += OnFolWal_CollectionChanged;
                    SetIsDirty(true);
                }
        }

        private void OnFolWal_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null) foreach (Wallet wal in e.OldItems)
                {
                    wal.Documents.CollectionChanged -= OnFolWalDoc_CollectionChanged;
                    SetIsDirty(true);
                }
            if (e.NewItems != null) foreach (Wallet wal in e.NewItems)
                {
                    wal.Documents.CollectionChanged += OnFolWalDoc_CollectionChanged;
                    SetIsDirty(true);
                }
        }

        private void OnFol_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Folder.DateCreated) || e.PropertyName == nameof(Folder.Name))
            {
                SetIsDirty(true);
            }
        }
        private void OnFolWalDoc_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null) foreach (Document doc in e.OldItems)
                {
                    doc.PropertyChanged -= OnFolWalDoc_PropertyChanged;
                    SetIsDirty(true);
                }
            if (e.NewItems != null) foreach (Document doc in e.NewItems)
                {
                    doc.PropertyChanged += OnFolWalDoc_PropertyChanged;
                    SetIsDirty(true);
                }
        }

        private void OnFolWalDoc_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Document.Uri0))
            {
                SetIsDirty(true);
            }
        }
        #endregion binder data events


        #region actions
        public void CloseCover()
        {
            _binder?.SetIsCoverOpen(false);
        }

        public async Task SelectFolderAsync(string folderId)
        {
            if (!string.IsNullOrWhiteSpace(folderId))
            {
                await _binder?.SetCurrentFolderIdAsync(folderId);
                _binder?.SetIsCoverOpen(false);
            }
        }

        public async Task DeleteFolderAsync(FolderPreview fp)
        {
            _refreshIntervalMsec = REFRESH_INTERVAL_SHORT_MSEC;
            await _binder?.RemoveFolderAsync(fp.FolderId);
        }
        public async Task AddFolderAsync()
        {
            _refreshIntervalMsec = REFRESH_INTERVAL_SHORT_MSEC;
            await _binder?.AddFolderAsync(new Folder());
        }
        public async Task AddOpenFolderAsync()
        {
            _refreshIntervalMsec = REFRESH_INTERVAL_SHORT_MSEC;
            var newFolder = new Folder();
            if (await _binder?.AddFolderAsync(newFolder))
            {
                await SelectFolderAsync(newFolder.Id);
            }
        }
        #endregion actions


        #region utilz
        private List<FolderPreview> GetFolderPreviews(IEnumerable<Folder> folders, IEnumerable<Wallet> wallets, IEnumerable<Document> documents)
        {
            var folderPreviews = new List<FolderPreview>();

            foreach (var fol in folders)
            {
                var folderPreview = new FolderPreview() { FolderName = fol.Name, FolderId = fol.Id };
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
                            folderPreview.DocumentUri0 = doc.Uri0;
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

            return folderPreviews;
        }
        #endregion utilz
    }
}

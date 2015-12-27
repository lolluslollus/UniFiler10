using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Model;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;

namespace UniFiler10.ViewModels
{
	public sealed class BinderCoverVM : OpenableObservableData
	{
		#region fields
		private const int REFRESH_INTERVAL_LONG_MSEC = 5000;
		private const int REFRESH_INTERVAL_SHORT_MSEC = 25;
		private AnimationStarter _animationStarter = null;
		private CancellationTokenSource _cts = null;
		#endregion fields


		#region properties
		private Binder _binder = null;
		public Binder Binder { get { return _binder; } private set { _binder = value; RaisePropertyChanged_UI(); } }

		private MetaBriefcase _metaBriefcase = null;
		public MetaBriefcase MetaBriefcase { get { return _metaBriefcase; } private set { _metaBriefcase = value; RaisePropertyChanged_UI(); } }

		private SwitchableObservableCollection<Binder.FolderPreview> _folderPreviews = new SwitchableObservableCollection<Binder.FolderPreview>();
		public SwitchableObservableCollection<Binder.FolderPreview> FolderPreviews { get { return _folderPreviews; } private set { _folderPreviews = value; RaisePropertyChanged_UI(); } }


		private bool _isAllFolderPaneOpen = false;
		public bool IsAllFoldersPaneOpen
		{
			get { return _isAllFolderPaneOpen; }
			set
			{
				if (_isAllFolderPaneOpen != value)
				{
					_isAllFolderPaneOpen = value; RaisePropertyChanged_UI();
					if (_isAllFolderPaneOpen) { _binder?.SetFilter(Binder.Filters.All); CloseOtherPanes(); Task upd = UpdatePaneContentAsync(0); }
				}
			}
		}

		private bool _isRecentFolderPaneOpen = false;
		public bool IsRecentFoldersPaneOpen
		{
			get { return _isRecentFolderPaneOpen; }
			set
			{
				if (_isRecentFolderPaneOpen != value)
				{
					_isRecentFolderPaneOpen = value; RaisePropertyChanged_UI();
					if (_isRecentFolderPaneOpen) { _binder?.SetFilter(Binder.Filters.Recent); CloseOtherPanes(); Task upd = UpdatePaneContentAsync(0); }
				}
			}
		}

		private bool _isByCatFolderPaneOpen = false;
		public bool IsByCatFoldersPaneOpen
		{
			get { return _isByCatFolderPaneOpen; }
			set
			{
				if (_isByCatFolderPaneOpen != value)
				{
					_isByCatFolderPaneOpen = value; RaisePropertyChanged_UI();
					if (_isByCatFolderPaneOpen) { _binder?.SetFilter(Binder.Filters.Cat); CloseOtherPanes(); Task upd = UpdatePaneContentAsync(0); }
				}
			}
		}

		private bool _isByFldFolderPaneOpen = false;
		public bool IsByFldFoldersPaneOpen
		{
			get { return _isByFldFolderPaneOpen; }
			set
			{
				if (_isByFldFolderPaneOpen != value)
				{
					_isByFldFolderPaneOpen = value; RaisePropertyChanged_UI();
					if (_isByFldFolderPaneOpen) { _binder?.SetFilter(Binder.Filters.Field); CloseOtherPanes(); Task upd = UpdatePaneContentAsync(0); }
				}
			}
		}
		private void CloseOtherPanes([CallerMemberName] string currentPropertyName = "")
		{
			if (currentPropertyName != nameof(IsAllFoldersPaneOpen)) IsAllFoldersPaneOpen = false;
			if (currentPropertyName != nameof(IsRecentFoldersPaneOpen)) IsRecentFoldersPaneOpen = false;
			if (currentPropertyName != nameof(IsByCatFoldersPaneOpen)) IsByCatFoldersPaneOpen = false;
			if (currentPropertyName != nameof(IsByFldFoldersPaneOpen)) IsByFldFoldersPaneOpen = false;
		}

		private bool _isAllFoldersDirty = true;
		private bool IsAllFoldersDirty
		{
			get { return _isAllFoldersDirty; }
			set
			{
				if (_isAllFoldersDirty != value)
				{
					_isAllFoldersDirty = value; RaisePropertyChanged_UI();
				}
			}
		}
		private bool _isRecentFoldersDirty = true;
		private bool IsRecentFoldersDirty
		{
			get { return _isRecentFoldersDirty; }
			set
			{
				if (_isRecentFoldersDirty != value)
				{
					_isRecentFoldersDirty = value; RaisePropertyChanged_UI();
				}
			}
		}
		private void SetIsDirty(bool newValue, bool scheduleUpdate, int withinMsec)
		{
			IsAllFoldersDirty = newValue;
			IsRecentFoldersDirty = newValue;

			if (scheduleUpdate)
			{
				if (_isAllFoldersDirty || _isRecentFoldersDirty)
				{
					Task upd = UpdatePaneContentAsync(withinMsec);
				}
			}

			//IsByCatFoldersDirty = newValue; // we don't use these variables to avoid catching all sorts of data changes
			//IsByFldFoldersDirty = newValue; // we don't use these variables to avoid catching all sorts of data changes

			//if (newValue) { Task upd = UpdatePaneContentAsync(REFRESH_INTERVAL_SHORT_MSEC); }
		}

		private string _catNameForCatFilter = null;
		public string CatNameForCatFilter
		{
			get { return _catNameForCatFilter; }
			set { SetCatNameForCatFilter(value, true); } // we only use this setter for the binding
		}
		private void SetCatNameForCatFilter(string value, bool doUpdates)
		{
			string newValue = value ?? string.Empty;
			if (_catNameForCatFilter != newValue)
			{
				_catNameForCatFilter = newValue; RaisePropertyChanged_UI(nameof(CatNameForCatFilter));
				if (doUpdates)
				{
					UpdateIdsForCatFilterAsync().ContinueWith(delegate
					{
						if (_isByCatFolderPaneOpen) { Task upd = UpdatePaneContentAsync(0); }
					});
				}
			}
		}

		private string _catNameForFldFilter = null;
		public string CatNameForFldFilter
		{
			get { return _catNameForFldFilter; }
			set { SetCatNameForFldFilter(value, true); } // we only use this setter for the binding
		}
		private void SetCatNameForFldFilter(string value, bool doUpdates)
		{
			string newValue = value ?? string.Empty;
			if (_catNameForFldFilter != newValue)
			{
				_catNameForFldFilter = newValue; RaisePropertyChanged_UI(nameof(CatNameForFldFilter));
				if (doUpdates)
				{
					UpdateIdsForFldFilterAsync().ContinueWith(delegate
					{
						UpdateDataForFldFilter();
						if (_isByFldFolderPaneOpen) { Task upd = UpdatePaneContentAsync(0); }
					});
				}
			}
		}

		private string _fldDscCaptionForFldFilter = null;
		public string FldDscCaptionForFldFilter
		{
			get { return _fldDscCaptionForFldFilter; }
			set { SetFldDscCaptionForFldFilter(value, true); } // we only use this setter for the binding
		}
		private void SetFldDscCaptionForFldFilter(string value, bool doUpdates)
		{
			string newValue = value ?? string.Empty;
			if (_fldDscCaptionForFldFilter != newValue)
			{
				_fldDscCaptionForFldFilter = newValue; RaisePropertyChanged_UI(nameof(FldDscCaptionForFldFilter));
				if (doUpdates)
				{
					UpdateIdsForFldFilterAsync().ContinueWith(delegate
					{
						UpdateDataForFldFilter();
						if (_isByFldFolderPaneOpen) { Task upd = UpdatePaneContentAsync(0); }
					});
				}
			}
		}

		private string _fldValVaalueForFldFilter = null;
		public string FldValVaalueForFldFilter
		{
			get { return _fldValVaalueForFldFilter; }
			set { SetFldValVaalueForFldFilter(value, true); } // we only use this setter for the binding
		}
		private void SetFldValVaalueForFldFilter(string value, bool doUpdates)
		{
			string newValue = value ?? string.Empty;
			if (_fldValVaalueForFldFilter != newValue)
			{
				_fldValVaalueForFldFilter = newValue; RaisePropertyChanged_UI(nameof(FldValVaalueForFldFilter));
				if (doUpdates)
				{
					UpdateIdsForFldFilterAsync().ContinueWith(delegate
					{
						if (_isByFldFolderPaneOpen) { Task upd = UpdatePaneContentAsync(0); }
					});
					//UpdateDataForFldFilter(); // we don't need it here
					//UpdateIdsForFldFilter(); // we don't need it here
				}
			}
		}

		private SwitchableObservableCollection<FieldDescription> _fldDscsInCat = new SwitchableObservableCollection<FieldDescription>();
		public SwitchableObservableCollection<FieldDescription> FldDscsInCat { get { return _fldDscsInCat; } private set { _fldDscsInCat = value; RaisePropertyChanged_UI(); } }

		private SwitchableObservableCollection<FieldValue> _fldValsInFldDscs = new SwitchableObservableCollection<FieldValue>();
		public SwitchableObservableCollection<FieldValue> FldValsInFldDscs { get { return _fldValsInFldDscs; } private set { _fldValsInFldDscs = value; RaisePropertyChanged_UI(); } }
		#endregion properties


		#region update methods
		private void UpdateIsPaneOpen()
		{
			var binder = _binder; if (binder == null) return;

			switch (binder.WhichFilter)
			{
				case Binder.Filters.All:
					IsAllFoldersPaneOpen = true; break;
				case Binder.Filters.Cat:
					IsByCatFoldersPaneOpen = true; break;
				case Binder.Filters.Field:
					IsByFldFoldersPaneOpen = true; break;
				case Binder.Filters.Recent:
					IsRecentFoldersPaneOpen = true; break;
				default:
					IsRecentFoldersPaneOpen = true; break;
			}
		}

		private void UpdateDataForCatFilter()
		{
			var binder = _binder; if (binder == null) return;

			Category cat = null;
			cat = _metaBriefcase?.Categories?.FirstOrDefault(ca => ca.Id == binder.CatIdForCatFilter);
			SetCatNameForCatFilter(cat?.Name, false);
		}

		private async Task UpdateIdsForCatFilterAsync()
		{
			var binder = _binder; if (binder == null) return;
			await binder.SetIdsForCatFilterAsync(_metaBriefcase?.Categories?.FirstOrDefault(cat => cat.Name == _catNameForCatFilter)?.Id).ConfigureAwait(false);
		}

		private void UpdateDataForFldFilter()
		{
			var binder = _binder; if (binder == null) return;

			_fldDscsInCat.Clear();
			_fldValsInFldDscs.Clear();

			Category cat = null;
			FieldDescription fldDsc = null;

			cat = _metaBriefcase?.Categories?.FirstOrDefault(ca => ca.Id == binder.CatIdForFldFilter);
			if (cat != null)
			{
				if (cat.FieldDescriptions != null) _fldDscsInCat.AddRange(cat.FieldDescriptions);

				fldDsc = cat.FieldDescriptions.FirstOrDefault(fd => fd.Id == binder.FldDscIdForFldFilter);
				if (fldDsc != null)
				{
					if (fldDsc.PossibleValues != null) _fldValsInFldDscs.AddRange(fldDsc.PossibleValues);
				}
			}

			SetCatNameForFldFilter(cat?.Name, false);
			SetFldDscCaptionForFldFilter(fldDsc?.Caption, false);
			SetFldValVaalueForFldFilter(fldDsc?.PossibleValues?.FirstOrDefault(fv => fv.Id == binder.FldValIdForFldFilter)?.Vaalue, false);
		}

		private async Task UpdateIdsForFldFilterAsync()
		{
			var binder = _binder; if (binder == null) return;

			var catId = Binder.DEFAULT_ID;
			var fldDscId = Binder.DEFAULT_ID;
			var fldValId = Binder.DEFAULT_ID;

			var cat = _metaBriefcase.Categories.FirstOrDefault(ca => ca.Name == _catNameForFldFilter);
			if (cat != null)
			{
				catId = cat.Id;

				var fldDsc = cat.FieldDescriptions.FirstOrDefault(fd => fd.Caption == _fldDscCaptionForFldFilter);
				// var fldDsc = _metaBriefcase.FieldDescriptions.FirstOrDefault(fd => fd.Caption == _fldDscCaptionForFldFilter && cat.FieldDescriptionIds.Contains(fd.Id));
				if (fldDsc != null)
				{
					fldDscId = fldDsc.Id;

					var fldVal4F = fldDsc.PossibleValues.FirstOrDefault(fVal => fVal.Vaalue == _fldValVaalueForFldFilter);
					if (fldVal4F != null)
					{
						fldValId = fldVal4F.Id;
					}
				}
			}

			await binder.SetIdsForFldFilterAsync(catId, fldDscId, fldValId).ConfigureAwait(false);
		}

		private async Task UpdatePaneContentAsync(int waitMsec)
		{
			if (IsNeedRefresh && IsOpen)
			{
				try
				{
					_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);

					var cts = _cts;
					if (cts != null)
					{
						await Task.Delay(waitMsec, cts.Token).ConfigureAwait(false);
						Debug.WriteLine("Finished waiting " + waitMsec + " msec");

						await RunFunctionWhileOpenAsyncT(UpdatePaneContent2Async);
					}
				}
				catch (OperationCanceledException) { } // fired by the cancellation token
				catch (ObjectDisposedException) { } // fired by the cancellation token
				catch (Exception ex)
				{
					await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
				}
				finally
				{
					_animationStarter.EndAnimation(AnimationStarter.Animations.Updating);
				}
			}
		}
		private async Task UpdatePaneContent2Async()
		{
			if (IsNeedRefresh)
			{
				if (_isAllFoldersDirty && _isAllFolderPaneOpen)
				{
					await Task.Run(delegate { return ReadAllFoldersAsync(); }).ConfigureAwait(false);
					IsAllFoldersDirty = false;
					IsRecentFoldersDirty = true;
				}
				if (_isRecentFoldersDirty && _isRecentFolderPaneOpen)
				{
					await Task.Run(delegate { return ReadRecentFoldersAsync(); }).ConfigureAwait(false);
					IsAllFoldersDirty = true;
					IsRecentFoldersDirty = false;
				}
				if (/*_isByCatFoldersDirty &&*/ _isByCatFolderPaneOpen)
				{
					await Task.Run(delegate { return ReadByCatFoldersAsync(); }).ConfigureAwait(false);
					//IsByCatFoldersDirty = false;
					IsAllFoldersDirty = true;
					IsRecentFoldersDirty = true;
				}
				if (/*_isByFldFoldersDirty &&*/ _isByFldFolderPaneOpen)
				{
					await Task.Run(delegate { return ReadByFldFoldersAsync(); }).ConfigureAwait(false);
					//IsByFldFoldersDirty = false;
					IsAllFoldersDirty = true;
					IsRecentFoldersDirty = true;
				}
			}
		}
		private bool IsNeedRefresh
		{
			get
			{
				return (_isAllFoldersDirty && _isAllFolderPaneOpen) || (_isRecentFoldersDirty && _isRecentFolderPaneOpen) || _isByCatFolderPaneOpen || _isByFldFolderPaneOpen;
			}
		}
		#endregion update methods


		#region construct dispose open close
		public BinderCoverVM(Binder binder, AnimationStarter animationStarter)
		{
			if (binder == null) throw new ArgumentNullException("BinderCoverVM ctor: binder may not be null");
			if (animationStarter == null) throw new ArgumentNullException("BinderCoverVM ctor: animationStarter may not be null");

			Binder = binder;
			MetaBriefcase = MetaBriefcase.OpenInstance;
			if (_metaBriefcase == null) Debugger.Break(); // LOLLO this must never happen, check it
			_animationStarter = animationStarter;

			UpdateDataForCatFilter();
			UpdateDataForFldFilter();

			UpdateIsPaneOpen();
		}

		protected override async Task OpenMayOverrideAsync()
		{
			_cts = new CancellationTokenSource();
			RegisterFoldersChanged();
			await UpdatePaneContent2Async().ConfigureAwait(false);

			if (ImportFoldersEnded == null) ImportFoldersEnded += OnImportFoldersEnded;
			await ResumeImportFoldersFromBinderAsync().ConfigureAwait(false);
		}

		private void OnImportFoldersEnded(object sender, EventArgs e)
		{
			Task res = ResumeImportFoldersFromBinderAsync();
		}

		protected override Task CloseMayOverrideAsync()
		{
			ImportFoldersEnded -= OnImportFoldersEnded;
			UnregisterFoldersChanged();
			//_animationStarter.EndAllAnimations();

			try
			{
				_cts?.Cancel();
			}
			catch { }
			try
			{
				_cts?.Dispose();
			}
			catch { }
			_cts = null;

			return Task.CompletedTask;
		}

		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_folderPreviews?.Dispose();
			_folderPreviews = null;
		}
		#endregion construct dispose open close


		#region binder data methods
		private async Task ReadAllFoldersAsync()
		{
			var binder = _binder; if (binder == null) return;
			var fp = await binder.GetAllFolderPreviewsAsync().ConfigureAwait(false);
			await RefreshFolderPreviewsAsync(fp).ConfigureAwait(false);
		}

		private async Task ReadRecentFoldersAsync()
		{
			var binder = _binder; if (binder == null) return;
			var fp = await binder.GetRecentFolderPreviewsAsync().ConfigureAwait(false);
			await RefreshFolderPreviewsAsync(fp).ConfigureAwait(false);
		}

		private async Task ReadByCatFoldersAsync()
		{
			var binder = _binder; if (binder == null) return;
			var fp = await binder.GetByCatFolderPreviewsAsync().ConfigureAwait(false);
			await RefreshFolderPreviewsAsync(fp).ConfigureAwait(false);
		}

		private async Task ReadByFldFoldersAsync()
		{
			var binder = _binder; if (binder == null) return;
			var fp = await binder.GetByFldFolderPreviewsAsync().ConfigureAwait(false);
			await RefreshFolderPreviewsAsync(fp).ConfigureAwait(false);
		}

		private Task RefreshFolderPreviewsAsync(List<Binder.FolderPreview> folderPreviews)
		{
			return RunInUiThreadAsync(delegate
			{
				_folderPreviews?.Clear();
				_folderPreviews?.AddRange(folderPreviews);
				Debug.WriteLine("BinderCoverVM has refreshed the folder previews");
			});
		}
		#endregion binder data methods


		#region binder data events
		private void RegisterFoldersChanged()
		{
			if (_binder?.Folders != null)
			{
				_binder.Folders.CollectionChanged += OnFol_CollectionChanged;
				foreach (Folder fol in _binder.Folders)
				{
					RegisterFolderChanged(fol);
				}
			}
		}
		private void RegisterFolderChanged(Folder fol)
		{
			fol.PropertyChanged += OnFol_PropertyChanged;
			fol.Wallets.CollectionChanged += OnFolWal_CollectionChanged;
			foreach (Wallet wal in fol.Wallets)
			{
				RegisterWalletChanged(wal);
			}
		}
		private void RegisterWalletChanged(Wallet wal)
		{
			wal.Documents.CollectionChanged += OnFolWalDoc_CollectionChanged;
			foreach (Document doc in wal.Documents)
			{
				RegisterDocumentChanged(doc);
			}
		}
		private void RegisterDocumentChanged(Document doc)
		{
			doc.PropertyChanged += OnFolWalDoc_PropertyChanged;
		}
		private void UnregisterFoldersChanged()
		{
			SetIsDirty(false, false, 0);

			if (_binder?.Folders != null)
			{
				_binder.Folders.CollectionChanged -= OnFol_CollectionChanged;
				foreach (Folder fol in _binder.Folders)
				{
					UnregisterFolderChanged(fol);
				}
			}
		}
		private void UnregisterFolderChanged(Folder fol)
		{
			fol.PropertyChanged -= OnFol_PropertyChanged;
			fol.Wallets.CollectionChanged -= OnFolWal_CollectionChanged;
			foreach (Wallet wal in fol.Wallets)
			{
				UnregisterWalletChanged(wal);
			}
		}
		private void UnregisterWalletChanged(Wallet wal)
		{
			wal.Documents.CollectionChanged -= OnFolWalDoc_CollectionChanged;
			foreach (Document doc in wal.Documents)
			{
				UnregisterDocumentChanged(doc);
			}
		}
		private void UnregisterDocumentChanged(Document doc)
		{
			doc.PropertyChanged -= OnFolWalDoc_PropertyChanged;
		}

		private void OnFol_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			bool isDirty = false;
			if (e.OldItems != null)
				foreach (Folder fol in e.OldItems)
				{
					UnregisterFolderChanged(fol);
					isDirty = true;
				}
			if (e.NewItems != null)
				foreach (Folder fol in e.NewItems)
				{
					RegisterFolderChanged(fol);
					isDirty = true;
				}
			if (isDirty) SetIsDirty(true, true, REFRESH_INTERVAL_LONG_MSEC);
		}

		private void OnFolWal_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			bool isDirty = false;
			if (e.OldItems != null)
				foreach (Wallet wal in e.OldItems)
				{
					UnregisterWalletChanged(wal);
					isDirty = true;
				}
			if (e.NewItems != null)
				foreach (Wallet wal in e.NewItems)
				{
					RegisterWalletChanged(wal);
					isDirty = true;
				}
			if (isDirty) SetIsDirty(true, true, REFRESH_INTERVAL_LONG_MSEC);
		}

		private void OnFol_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Folder.DateCreated) || e.PropertyName == nameof(Folder.Name))
			{
				SetIsDirty(true, true, REFRESH_INTERVAL_LONG_MSEC);
			}
		}
		private void OnFolWalDoc_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			bool isDirty = false;
			if (e.OldItems != null)
				foreach (Document doc in e.OldItems)
				{
					UnregisterDocumentChanged(doc);
					isDirty = true;
				}
			if (e.NewItems != null)
				foreach (Document doc in e.NewItems)
				{
					RegisterDocumentChanged(doc);
					isDirty = true;
				}
			if (isDirty) SetIsDirty(true, true, REFRESH_INTERVAL_LONG_MSEC);
		}

		private void OnFolWalDoc_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Document.Uri0))
			{
				SetIsDirty(true, true, REFRESH_INTERVAL_LONG_MSEC);
			}
		}
		#endregion binder data events


		#region user actions
		public async Task<bool> SetCurrentFolderAsync(string folderId)
		{
			if (!string.IsNullOrWhiteSpace(folderId))
			{
				var binder = _binder;
				if (binder != null)
				{
					await binder.SetCurrentFolderIdAsync(folderId);
					return true;
				}
			}
			return false;
		}

		public async Task AddFolderAsync()
		{
			var binder = _binder;
			if (binder != null)
			{
				if (await binder.AddFolderAsync().ConfigureAwait(false) != null)
				{
					// if there is a filter in place, remove it to show the new folder 
					if (!_isAllFolderPaneOpen && !_isRecentFolderPaneOpen) IsAllFoldersPaneOpen = true;
					else SetIsDirty(true, true, 0);
				}
			}
			// LOLLO NOTE that instance?.Method() and Task ttt = instance?.Method() work, but await instance?.Method() throws a null reference exception if instance is null.
		}

		public async Task<bool> AddAndOpenFolderAsync()
		{
			var binder = _binder;
			if (binder != null)
			{
				var newFolder = await binder.AddFolderAsync();
				if (newFolder != null)
				{
					if (await SetCurrentFolderAsync(newFolder.Id).ConfigureAwait(false))
					{
						SetIsDirty(true, true, 0);
						return true;
					}
				}
			}
			return false;
		}

		public async Task DeleteFolderAsync(Binder.FolderPreview fp)
		{
			var binder = _binder;
			if (binder != null && fp != null)
			{
				if (await binder.RemoveFolderAsync(fp.FolderId).ConfigureAwait(false))
				{
					SetIsDirty(true, true, 0);
				}
			}
		}


		public void StartImportFoldersFromBinderAsync()
		{
			var binder = _binder;
			if (binder != null)
			{
				var pickDirectoryTask = Pickers.PickFolderAsync(new string[] { ConstantData.DB_EXTENSION, ConstantData.XML_EXTENSION });
				var afterPickDirectoryTask = pickDirectoryTask.ContinueWith(delegate
				{
					return AfterImportFoldersFromBinderAsync(pickDirectoryTask, binder);
				});
			}
			else
			{
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		private async Task AfterImportFoldersFromBinderAsync(Task<StorageFolder> fromDirTask, Binder binder)
		{
			bool isOk = false;
			var fromDir = await fromDirTask.ConfigureAwait(false);

			_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);

			if (fromDir != null && binder != null)
			{
				StorageFolder tempDir = null;
				if (fromDir.Path.Contains(ApplicationData.Current.TemporaryFolder.Path))
				{
					tempDir = fromDir;
				}
				else
				{
					tempDir = await ApplicationData.Current.TemporaryFolder
						.CreateFolderAsync(ConstantData.TEMP_DIR_4_IMPORT_FOLDERS, CreationCollisionOption.GenerateUniqueName)
						.AsTask().ConfigureAwait(false);
					await fromDir.CopyDirContentsReplacingAsync(tempDir).ConfigureAwait(false);
				}

				isOk = await binder.ImportFoldersAsync(tempDir).ConfigureAwait(false);
				if (isOk)
				{
					await tempDir.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
					RegistryAccess.SetValue(ConstantData.REG_IMPORT_FOLDERS_BINDER_NAME, string.Empty);
					RegistryAccess.SetValue(ConstantData.REG_IMPORT_FOLDERS_DIR_PATH, string.Empty);

				}
				else
				{
					RegistryAccess.SetValue(ConstantData.REG_IMPORT_FOLDERS_BINDER_NAME, binder.DBName);
					RegistryAccess.SetValue(ConstantData.REG_IMPORT_FOLDERS_DIR_PATH, tempDir.Path);
					ImportFoldersEnded?.Invoke(this, EventArgs.Empty);
				}
			}
			_animationStarter.EndAllAnimations();
			if (isOk) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			else _animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
		}

		private async Task ResumeImportFoldersFromBinderAsync()
		{
			string dbName = RegistryAccess.GetValue(ConstantData.REG_IMPORT_FOLDERS_BINDER_NAME);
			string dirPath = RegistryAccess.GetValue(ConstantData.REG_IMPORT_FOLDERS_DIR_PATH);
			var binder = _binder;
			if (binder != null && !string.IsNullOrWhiteSpace(dbName) && !string.IsNullOrWhiteSpace(dirPath) && binder.DBName == dbName)
			{
				var dirTask = StorageFolder.GetFolderFromPathAsync(dirPath).AsTask();
				await AfterImportFoldersFromBinderAsync(dirTask, binder).ConfigureAwait(false);
			}
		}

		private static event EventHandler ImportFoldersEnded;
		#endregion actions
	}
}
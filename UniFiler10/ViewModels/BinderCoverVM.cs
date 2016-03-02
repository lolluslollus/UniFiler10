using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Model;
using Utilz;
using Utilz.Data;
using Windows.Storage;
using Windows.System.Profile;

namespace UniFiler10.ViewModels
{
	public sealed class BinderCoverVM : OpenableObservableDisposableData
	{
		#region fields
		private const int REFRESH_INTERVAL_LONG_MSEC = 5000;
		//private const int REFRESH_INTERVAL_SHORT_MSEC = 25;
		private readonly AnimationStarter _animationStarter = null;
		#endregion fields


		#region properties
		private readonly Binder _binder = null;
		public Binder Binder { get { return _binder; } }

		private readonly MetaBriefcase _metaBriefcase = null;
		public MetaBriefcase MetaBriefcase { get { return _metaBriefcase; } }

		private SwitchableObservableDisposableCollection<Binder.FolderPreview> _folderPreviews = null;
		public SwitchableObservableDisposableCollection<Binder.FolderPreview> FolderPreviews { get { return _folderPreviews; } private set { _folderPreviews = value; RaisePropertyChanged_UI(); } }

		private volatile bool _isAllFolderPaneOpen = false;
		public bool IsAllFoldersPaneOpen
		{
			get { return _isAllFolderPaneOpen; }
			set
			{
				if (_isAllFolderPaneOpen != value)
				{
					_isAllFolderPaneOpen = value; RaisePropertyChanged_UI();
					if (_isAllFolderPaneOpen) { _binder.SetFilter(Binder.Filters.All); CloseOtherPanes(); Task upd = UpdatePaneContentAsync(0); }
				}
			}
		}

		private volatile bool _isRecentFolderPaneOpen = false;
		public bool IsRecentFoldersPaneOpen
		{
			get { return _isRecentFolderPaneOpen; }
			set
			{
				if (_isRecentFolderPaneOpen != value)
				{
					_isRecentFolderPaneOpen = value; RaisePropertyChanged_UI();
					if (_isRecentFolderPaneOpen) { _binder.SetFilter(Binder.Filters.Recent); CloseOtherPanes(); Task upd = UpdatePaneContentAsync(0); }
				}
			}
		}

		private volatile bool _isByCatFolderPaneOpen = false;
		public bool IsByCatFoldersPaneOpen
		{
			get { return _isByCatFolderPaneOpen; }
			set
			{
				if (_isByCatFolderPaneOpen != value)
				{
					_isByCatFolderPaneOpen = value; RaisePropertyChanged_UI();
					if (_isByCatFolderPaneOpen) { _binder.SetFilter(Binder.Filters.Cat); CloseOtherPanes(); Task upd = UpdatePaneContentAsync(0); }
				}
			}
		}

		private volatile bool _isByFldFolderPaneOpen = false;
		public bool IsByFldFoldersPaneOpen
		{
			get { return _isByFldFolderPaneOpen; }
			set
			{
				if (_isByFldFolderPaneOpen != value)
				{
					_isByFldFolderPaneOpen = value; RaisePropertyChanged_UI();
					if (_isByFldFolderPaneOpen) { _binder.SetFilter(Binder.Filters.Field); CloseOtherPanes(); Task upd = UpdatePaneContentAsync(0); }
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

		private volatile bool _isAllFoldersDirty = true;
		private bool IsAllFoldersDirty
		{
			get { return _isAllFoldersDirty; }
			set
			{
				if (_isAllFoldersDirty != value)
				{
					_isAllFoldersDirty = value;
				}
			}
		}

		private volatile bool _isRecentFoldersDirty = true;
		private bool IsRecentFoldersDirty
		{
			get { return _isRecentFoldersDirty; }
			set
			{
				if (_isRecentFoldersDirty != value)
				{
					_isRecentFoldersDirty = value;
				}
			}
		}
		private void SetIsDirty(bool newValue, bool scheduleUpdate, int withinMsec)
		{
			IsAllFoldersDirty = newValue;
			IsRecentFoldersDirty = newValue;

			if (scheduleUpdate && (_isAllFoldersDirty || _isRecentFoldersDirty))
			{
				Task upd = UpdatePaneContentAsync(withinMsec);
			}

			//IsByCatFoldersDirty = newValue; // we don't use these variables to avoid catching all sorts of data changes
			//IsByFldFoldersDirty = newValue; // we don't use these variables to avoid catching all sorts of data changes
		}

		private volatile string _catNameForCatFilter = string.Empty;
		public string CatNameForCatFilter
		{
			get { return _catNameForCatFilter; }
			set { SetCatNameForCatFilter(value, true); } // we only use this setter for the binding
		}
		private void SetCatNameForCatFilter(string value, bool doUpdates)
		{
			string newValue = value ?? string.Empty;
			if (_catNameForCatFilter == newValue) return;

			_catNameForCatFilter = newValue; RaisePropertyChanged_UI(nameof(CatNameForCatFilter));
			if (!doUpdates) return;

			UpdateCatFilterId_ToAConsistentState();
			if (_isByCatFolderPaneOpen) { Task upd = UpdatePaneContentAsync(0); }
		}

		private volatile string _catNameForFldFilter = string.Empty;
		public string CatNameForFldFilter
		{
			get { return _catNameForFldFilter; }
			set { SetCatNameForFldFilter(value, true); } // we only use this setter for the binding
		}
		private void SetCatNameForFldFilter(string value, bool doUpdates)
		{
			string newValue = value ?? string.Empty;
			if (_catNameForFldFilter == newValue) return;

			_catNameForFldFilter = newValue; RaisePropertyChanged_UI(nameof(CatNameForFldFilter));
			if (!doUpdates) return;

			UpdateFldFilterIds_ToAConsistentState();
			UpdateFldFilterFromIds(); // call this coz this property can affect other *FldFilter properties
			if (_isByFldFolderPaneOpen) { Task upd = UpdatePaneContentAsync(0); }
		}

		private volatile string _fldDscCaptionForFldFilter = string.Empty;
		public string FldDscCaptionForFldFilter
		{
			get { return _fldDscCaptionForFldFilter; }
			set { SetFldDscCaptionForFldFilter(value, true); } // we only use this setter for the binding
		}
		private void SetFldDscCaptionForFldFilter(string value, bool doUpdates)
		{
			string newValue = value ?? string.Empty;
			if (_fldDscCaptionForFldFilter == newValue) return;

			_fldDscCaptionForFldFilter = newValue; RaisePropertyChanged_UI(nameof(FldDscCaptionForFldFilter));
			if (!doUpdates) return;

			UpdateFldFilterIds_ToAConsistentState();
			UpdateFldFilterFromIds(); // call this coz this property can affect other *FldFilter properties
			if (_isByFldFolderPaneOpen) { Task upd = UpdatePaneContentAsync(0); }
		}

		private volatile string _fldValVaalueForFldFilter = string.Empty;
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
					UpdateFldFilterIds_ToAConsistentState();
					//UpdateDataForFldFilter(); // we don't need it here
					if (_isByFldFolderPaneOpen) { Task upd = UpdatePaneContentAsync(0); }
				}
			}
		}

		private SwitchableObservableDisposableCollection<FieldDescription> _fldDscsInCat = null;
		public SwitchableObservableDisposableCollection<FieldDescription> FldDscsInCat { get { return _fldDscsInCat; } private set { _fldDscsInCat = value; RaisePropertyChanged_UI(); } }

		private SwitchableObservableDisposableCollection<FieldValue> _fldValsInFldDscs = null;
		public SwitchableObservableDisposableCollection<FieldValue> FldValsInFldDscs { get { return _fldValsInFldDscs; } private set { _fldValsInFldDscs = value; RaisePropertyChanged_UI(); } }

		private static readonly object _isImportingLocker = new object();
		public bool IsImportingFolders
		{
			get
			{
				lock (_isImportingLocker)
				{
					string tf = RegistryAccess.GetValue(ConstantData.REG_IMPORT_FOLDERS_IS_IMPORTING);
					return tf == true.ToString();
				}
			}
			private set
			{
				lock (_isImportingLocker)
				{
					RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_FOLDERS_IS_IMPORTING, value.ToString());
					RaisePropertyChanged_UI();
				}
			}
		}
		private bool TrySetIsImportingFolders(bool newValue)
		{
			lock (_isImportingLocker)
			{
				if (IsImportingFolders != newValue)
				{
					IsImportingFolders = newValue;
					return true;
				}
				return false;
			}
		}
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

		private void UpdateCatFilterFromIds()
		{
			var binder = _binder; if (binder == null) return;

			Category category = null;
			category = _metaBriefcase?.Categories?.FirstOrDefault(cat => cat.Id == binder.CatIdForCatFilter);
			SetCatNameForCatFilter(category?.Name, false);
		}

		private void UpdateCatFilterId_ToAConsistentState()
		{
			_binder?.SetIdsForCatFilter(_metaBriefcase?.Categories?.FirstOrDefault(cat => cat.Name == _catNameForCatFilter)?.Id);
		}

		private void UpdateFldFilterFromIds()
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
				if (fldDsc?.PossibleValues != null) _fldValsInFldDscs.AddRange(fldDsc.PossibleValues);
			}

			SetCatNameForFldFilter(cat?.Name, false);
			SetFldDscCaptionForFldFilter(fldDsc?.Caption, false);
			SetFldValVaalueForFldFilter(fldDsc?.PossibleValues?.FirstOrDefault(fv => fv.Id == binder.FldValIdForFldFilter)?.Vaalue, false);
		}

		private void UpdateFldFilterIds_ToAConsistentState()
		{
			var binder = _binder; if (binder == null) return;

			var catId = DbBoundObservableData.DEFAULT_ID;
			var fldDscId = DbBoundObservableData.DEFAULT_ID;
			var fldValId = DbBoundObservableData.DEFAULT_ID;

			var cat = _metaBriefcase.Categories.FirstOrDefault(ca => ca.Name == _catNameForFldFilter);
			if (cat != null)
			{
				catId = cat.Id;

				var fldDsc = cat.FieldDescriptions.FirstOrDefault(fd => fd.Caption == _fldDscCaptionForFldFilter);
				// var fldDsc = _metaBriefcase.FieldDescriptions.FirstOrDefault(fd => fd.Caption == _fldDscCaptionForFldFilter && cat.FieldDescriptionIds.Contains(fd.Id));
				if (fldDsc != null)
				{
					fldDscId = fldDsc.Id;

					var fldVal = fldDsc.PossibleValues.FirstOrDefault(fVal => fVal.Vaalue == _fldValVaalueForFldFilter);
					if (fldVal != null)
					{
						fldValId = fldVal.Id;
					}
				}
			}

			binder.SetIdsForFldFilter(catId, fldDscId, fldValId);
		}

		private async Task UpdatePaneContentAsync(int waitMsec)
		{
			if (IsNeedRefresh && IsOpen)
			{
				try
				{
					_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);

					await Task.Delay(waitMsec, CancToken).ConfigureAwait(false);
					Debug.WriteLine("Finished waiting " + waitMsec + " msec");

					await RunFunctionIfOpenAsyncT(UpdatePaneContent2Async);
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
					await Task.Run(ReadAllFoldersAsync, CancToken).ConfigureAwait(false);
					IsAllFoldersDirty = false;
					IsRecentFoldersDirty = true;
				}
				if (_isRecentFoldersDirty && _isRecentFolderPaneOpen)
				{
					await Task.Run(ReadRecentFoldersAsync, CancToken).ConfigureAwait(false);
					IsAllFoldersDirty = true;
					IsRecentFoldersDirty = false;
				}
				if (/*_isByCatFoldersDirty &&*/ _isByCatFolderPaneOpen)
				{
					await Task.Run(ReadByCatFoldersAsync, CancToken).ConfigureAwait(false);
					//IsByCatFoldersDirty = false;
					IsAllFoldersDirty = true;
					IsRecentFoldersDirty = true;
				}
				if (/*_isByFldFoldersDirty &&*/ _isByFldFolderPaneOpen)
				{
					await Task.Run(ReadByFldFoldersAsync, CancToken).ConfigureAwait(false);
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

			_binder = binder;
			RaisePropertyChanged_UI(nameof(Binder));
			_metaBriefcase = MetaBriefcase.OpenInstance;
			if (_metaBriefcase == null) throw new ArgumentNullException("BinderCoverVM ctor: MetaBriefcase may not be null");
			RaisePropertyChanged_UI(nameof(MetaBriefcase));
			_animationStarter = animationStarter;
		}

		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			await RunInUiThreadAsync(delegate
			{
				FolderPreviews = new SwitchableObservableDisposableCollection<Binder.FolderPreview>();
				FldDscsInCat = new SwitchableObservableDisposableCollection<FieldDescription>();
				FldValsInFldDscs = new SwitchableObservableDisposableCollection<FieldValue>();

				UpdateCatFilterFromIds();
				UpdateFldFilterFromIds();
				UpdateIsPaneOpen();
			}).ConfigureAwait(false);

			RegisterFoldersChanged();
			await UpdatePaneContent2Async().ConfigureAwait(false);

			Logger.Add_TPL("BinderCoverVM is opening", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

			if (IsImportingFolders)
			{
				Logger.Add_TPL("alphaWhenOpening", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
				var dir = await Pickers.GetLastPickedFolderAsync().ConfigureAwait(false);
				Logger.Add_TPL("dir == null = " + (dir == null).ToString(), Logger.AppEventsLogFilename, Logger.Severity.Info, false);

				await ContinueAfterPickAsync(dir, _binder);
			}
		}

		protected override Task CloseMayOverrideAsync()
		{
			_folderPreviews?.Dispose();
			_fldDscsInCat?.Dispose();
			_fldValsInFldDscs?.Dispose();

			UnregisterFoldersChanged();

			return Task.CompletedTask;
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

		private Task RefreshFolderPreviewsAsync(IEnumerable<Binder.FolderPreview> folderPreviews)
		{
			return RunInUiThreadAsync(delegate
			{
				_folderPreviews?.ReplaceAll(folderPreviews);
				Debug.WriteLine("BinderCoverVM has refreshed the folder previews");
			});
		}
		#endregion binder data methods


		#region binder data events
		private void RegisterFoldersChanged()
		{
			var folders = _binder?.Folders;
			if (folders != null)
			{
				folders.CollectionChanged += OnFol_CollectionChanged;
				foreach (Folder fol in folders)
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
		private void RegisterDocumentChanged(INotifyPropertyChanged doc)
		{
			doc.PropertyChanged += OnFolWalDoc_PropertyChanged;
		}
		private void UnregisterFoldersChanged()
		{
			SetIsDirty(false, false, 0);

			var folders = _binder?.Folders;
			if (folders == null) return;

			folders.CollectionChanged -= OnFol_CollectionChanged;
			foreach (Folder fol in folders)
			{
				UnregisterFolderChanged(fol);
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
		private void UnregisterDocumentChanged(INotifyPropertyChanged doc)
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

		private void OnFol_PropertyChanged(object sender, PropertyChangedEventArgs e)
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

		private void OnFolWalDoc_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Document.Uri0))
			{
				SetIsDirty(true, true, REFRESH_INTERVAL_LONG_MSEC);
			}
		}
		#endregion binder data events


		#region user actions
		public Task SetCurrentFolderAsync(string folderId)
		{
			return RunFunctionIfOpenAsyncT(() => SetCurrentFolder2Async(folderId));
		}

		private Task SetCurrentFolder2Async(string folderId)
		{
			var binder = _binder;
			if (binder == null) return Task.CompletedTask;

			return binder.SetCurrentFolderIdAsync(folderId);
		}

		public Task<bool> AddFolderAsync()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				var binder = _binder;
				if (binder == null) return false;

				if (await binder.AddFolderAsync().ConfigureAwait(false) == null) return false;

				// if there is a filter in place, remove it to show the new folder 
				if (!_isAllFolderPaneOpen && !_isRecentFolderPaneOpen) IsAllFoldersPaneOpen = true;
				else SetIsDirty(true, true, 0);
				return true;
			});
			// LOLLO NOTE that instance?.Method() and Task ttt = instance?.Method() work, but await instance?.Method() throws a null reference exception if instance is null.
		}

		public Task<bool> AddAndOpenFolderAsync()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				var binder = _binder;
				if (binder == null) return false;

				var newFolder = await binder.AddFolderAsync().ConfigureAwait(false);
				if (newFolder == null) return false;

				await SetCurrentFolder2Async(newFolder.Id).ConfigureAwait(false);
				SetIsDirty(true, true, 0);
				return true;
			});
		}

		public Task DeleteFolderAsync(Binder.FolderPreview fp)
		{
			return RunFunctionIfOpenAsyncT(async delegate
			{
				var binder = _binder;
				if (binder != null && fp != null)
				{
					if (await binder.RemoveFolderAsync(fp.FolderId).ConfigureAwait(false))
					{
						SetIsDirty(true, true, 0);
					}
				}
			});
		}

		public async void StartImportFoldersFromBinder()
		{
			if (!IsOpen) return;
			await Logger.AddAsync("StartImportFoldersFromBinder() starting", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			var binder = _binder;
			if (binder != null && TrySetIsImportingFolders(true))
			{
				var userChoice = await UserConfirmationPopup.GetInstance().GetUserChoiceBeforeImportingBinderAsync(binder.DBName, CancToken);
				if (CancToken.IsCancellationRequested) { IsImportingFolders = false; return; }
				if (userChoice.Item1 == BriefcaseVM.ImportBinderOperations.Cancel) { IsImportingFolders = false; return; }

				StorageFolder dir = null;
				if (string.IsNullOrWhiteSpace(userChoice.Item2))
				{
					dir = await Pickers.PickDirectoryAsync(new[] { ConstantData.DB_EXTENSION, ConstantData.XML_EXTENSION }).ConfigureAwait(false);
				}
				else
				{
					dir = await Briefcase.BindersDirectory.TryGetItemAsync(userChoice.Item2).AsTask().ConfigureAwait(false) as StorageFolder;
					Pickers.SetLastPickedFolder(dir);
				}

				// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
				// To avoid surprises, we try the following here under _isOpenSemaphore. If it does not run through, IsImportingFolders will stay true.
				// In OpenMayOverrideAsync, we check IsImportingFolders and, if true, we try again.
				// ContinueAfterPickAsync sets IsImportingFolders to false, so there won't be redundant attempts.
				Logger.Add_TPL("StartImportFoldersFromBinder(): directory picked", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
				var isThrough = await RunFunctionIfOpenThreeStateAsyncT(delegate
				{
					Logger.Add_TPL("StartImportFoldersFromBinder(): _isOpen == true, about to call ContinueAfterPickAsync()", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
					return ContinueAfterPickAsync(dir, binder);
				}).ConfigureAwait(false);
				Logger.Add_TPL("StartImportFoldersFromBinder(): isThrough = " + isThrough, Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		private static void ReadAnalyticsInfo()
		{
			// LOLLO TODO this can be interesting...
			var form = AnalyticsInfo.DeviceForm;
			var avi = AnalyticsInfo.VersionInfo;
			var family = avi.DeviceFamily;
			var familyVersion = avi.DeviceFamilyVersion;
		}

		private async Task ContinueAfterPickAsync(StorageFolder fromDir, Binder binder)
		{
			Logger.Add_TPL("ContinueAfterPickAsync() starting", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			bool isImported = false;
			try
			{
				if (binder != null && fromDir != null)
				{
					_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
					isImported = await binder.ImportFoldersAsync(fromDir).ConfigureAwait(false);
					Logger.Add_TPL("ContinueAfterPickAsync(): isImported = " + isImported, Logger.AppEventsLogFilename, Logger.Severity.Info, false);
					Logger.Add_TPL("ContinueAfterPickAsync(): binder is open = " + binder.IsOpen, Logger.AppEventsLogFilename, Logger.Severity.Info, false);
					Logger.Add_TPL("ContinueAfterPickAsync(): binder is disposed = " + binder.IsDisposed, Logger.AppEventsLogFilename, Logger.Severity.Info, false);
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.AppEventsLogFilename).ConfigureAwait(false);
			}

			_animationStarter.EndAllAnimations();
			_animationStarter.StartAnimation(isImported
				? AnimationStarter.Animations.Success
				: AnimationStarter.Animations.Failure);

			IsImportingFolders = false;
		}
		#endregion actions
	}
}
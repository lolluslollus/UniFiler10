﻿using System;
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
using Windows.Storage;

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
					RegistryAccess.SetValue(ConstantData.REG_IMPORT_FOLDERS_IS_IMPORTING, value.ToString());
					RaisePropertyChanged_UI();
					Logger.Add_TPL("IsImportingFolders = " + value.ToString(), Logger.AppEventsLogFilename, Logger.Severity.Info, false);
				}
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

						await RunFunctionIfOpenAsyncT(UpdatePaneContent2Async);
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

			Logger.Add_TPL("BinderCoverVM ctor ended", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		}
		//private static List<Func<Task>> _runAsSoonAsOpensStatic = new List<Func<Task>>();
		protected override async Task OpenMayOverrideAsync()
		{
			_cts = new CancellationTokenSource();
			RegisterFoldersChanged();
			await UpdatePaneContent2Async().ConfigureAwait(false);

			Logger.Add_TPL("BinderCoverVM is opening", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

			if (IsImportingFolders)
			{
				Logger.Add_TPL("alphaWhenOpening", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
				var dir = await Pickers.GetLastPickedFolderJustOnceAsync().ConfigureAwait(false);
				Logger.Add_TPL("dir == null = " + (dir == null).ToString(), Logger.AppEventsLogFilename, Logger.Severity.Info, false);

				await ContinueAfterPickAsync(dir, _binder);
			}
			//if (ImportFoldersEnded == null) ImportFoldersEnded += OnImportFoldersEnded;
			//await ResumeImportFoldersFromBinderAsync().ConfigureAwait(false);
		}

		//private void OnImportFoldersEnded(object sender, EventArgs e)
		//{
		//	Task res = ResumeImportFoldersFromBinderAsync();
		//}

		protected override Task CloseMayOverrideAsync()
		{
			//ImportFoldersEnded -= OnImportFoldersEnded;
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

			//Logger.Add_TPL("BinderCoverVM is closing, _runAsSoonAsOpens.Count = " + _runAsSoonAsOpens.Count, Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			//_runAsSoonAsOpensStatic.AddRange(_runAsSoonAsOpens);

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

		//public void StartImportFoldersFromBinder()
		//{
		//	// LOLLO TODO use http://stackoverflow.com/questions/23866325/how-to-avoid-storagefile-copyasync-throw-exception-when-copying-big-file
		//	// to copy files, across the app. Alternatively, warn when a file is too large.
		//	var binder = _binder;
		//	if (binder != null && !_isImportingFolders)
		//	{
		//		IsImportingFolders = true;
		//		//RegistryAccess.SetValue(ConstantData.REG_IMPORT_FOLDERS_IS_IMPORTING, true.ToString());
		//		var pickTask = Pickers.PickDirectoryAsync(new string[] { ConstantData.DB_EXTENSION, ConstantData.XML_EXTENSION });
		//		var afterPickTask = pickTask.ContinueWith(delegate
		//		{
		//			return ContinueAfterPickAsync(pickTask, binder);
		//		});
		//	}
		//	else
		//	{
		//		_animationStarter.EndAllAnimations();
		//		_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
		//	}
		//}

		public async void StartImportFoldersFromBinder()
		{
			await Logger.AddAsync("StartImportFoldersFromBinder() starting", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			// LOLLO TODO use http://stackoverflow.com/questions/23866325/how-to-avoid-storagefile-copyasync-throw-exception-when-copying-big-file
			// to copy files, across the app. Alternatively, warn when a file is too large.
			var binder = _binder;
			if (binder != null && !IsImportingFolders)
			{
				IsImportingFolders = true;
				var dir = await Pickers.PickDirectoryAsync(new string[] { ConstantData.DB_EXTENSION, ConstantData.XML_EXTENSION });
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

		// LOLLO TODO check https://social.msdn.microsoft.com/Forums/sqlserver/en-US/13002ba6-6e59-47b8-a746-c05525953c5a/uwpfileopenpicker-bugs-in-win-10-mobile-when-not-debugging?forum=wpdevelop
		// and AnalyticsVersionInfo.DeviceFamily
		// for picker details

		//private async Task ContinueAfterPickAsync(Task<StorageFolder> fromDirTask, Binder binder)
		//{
		//	bool isImported = false;
		//	bool isNeedsContinuing = false;

		//	try
		//	{
		//		if (binder != null)
		//		{
		//			var fromDir = await fromDirTask.ConfigureAwait(false);
		//			if (fromDir != null)
		//			{
		//				_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
		//				RegistryAccess.SetValue(ConstantData.REG_IMPORT_FOLDERS_BINDER_NAME, binder.DBName);
		//				isImported = await binder.ImportFoldersAsync(fromDir).ConfigureAwait(false);
		//				if (!isImported && !binder.IsOpen) isNeedsContinuing = true; // LOLLO if isOk is false because there was an error and not because the app was suspended, I must unlock importing.
		//			}
		//		}
		//	}
		//	catch (Exception ex)
		//	{
		//		await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
		//	}

		//	_animationStarter.EndAllAnimations();
		//	if (isImported)
		//	{
		//		_animationStarter.StartAnimation(AnimationStarter.Animations.Success);
		//	}
		//	if (isNeedsContinuing)
		//	{
		//		RegistryAccess.SetValue(ConstantData.REG_IMPORT_FOLDERS_CONTINUE_IMPORTING, true.ToString());
		//	}
		//	else
		//	{
		//		RegistryAccess.SetValue(ConstantData.REG_IMPORT_FOLDERS_CONTINUE_IMPORTING, false.ToString());
		//		if (!isImported) _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
		//		//RegistryAccess.SetValue(ConstantData.REG_IMPORT_FOLDERS_IS_IMPORTING, false.ToString());
		//		IsImportingFolders = false;
		//	}

		//	//ImportFoldersEnded?.Invoke(this, EventArgs.Empty);
		//}

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
			if (isImported) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			else _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);

			IsImportingFolders = false;
			//ImportFoldersEnded?.Invoke(this, EventArgs.Empty);
		}
		//private async Task ResumeImportFoldersFromBinderAsync()
		//{
		//	string continueImporting = RegistryAccess.GetValue(ConstantData.REG_IMPORT_FOLDERS_CONTINUE_IMPORTING);
		//	if (continueImporting == true.ToString())
		//	{
		//		string dbName = RegistryAccess.GetValue(ConstantData.REG_IMPORT_FOLDERS_BINDER_NAME);
		//		if (dbName == _binder?.DBName)
		//		{
		//			IsImportingFolders = true;
		//			_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);

		//			await ContinueAfterPickAsync(Pickers.GetLastPickedFolderJustOnceAsync(), _binder).ConfigureAwait(false);
		//		}
		//	}
		//	//}
		//	else
		//	{
		//		IsImportingFolders = false;
		//	}
		//}

		//private static event EventHandler ImportFoldersEnded;
		#endregion actions
	}
}
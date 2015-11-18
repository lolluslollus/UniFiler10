﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Metadata;
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
		private const int REFRESH_INTERVAL_LONG_MSEC = 5000;
		private const int REFRESH_INTERVAL_SHORT_MSEC = 25;

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

		private bool _isAllFoldersDirty = false;
		private bool IsAllFoldersDirty
		{
			get { return _isAllFoldersDirty; }
			set { if (_isAllFoldersDirty != value) { _isAllFoldersDirty = value; RaisePropertyChanged_UI(); Task upd = UpdatePaneContentAsync(_refreshIntervalMsec); } }
		}

		private bool _isRecentFoldersDirty = false;
		private bool IsRecentFoldersDirty
		{
			get { return _isRecentFoldersDirty; }
			set { if (_isRecentFoldersDirty != value) { _isRecentFoldersDirty = value; RaisePropertyChanged_UI(); Task upd = UpdatePaneContentAsync(_refreshIntervalMsec); } }
		}
		private void SetIsDirty(bool newValue)
		{
			IsAllFoldersDirty = newValue;
			IsRecentFoldersDirty = newValue;
			//IsByCatFoldersDirty = newValue; // we don't use these variables to avoid catching all sorts of data changes
			//IsByFldFoldersDirty = newValue; // we don't use these variables to avoid catching all sorts of data changes
		}

		//// LOLLO TODO test begin
		//private Category _cat4CatFilter = null;
		//public Category Cat4CatFilter
		//{
		//	get { return _cat4CatFilter; }
		//	private set
		//	{
		//		if (_cat4CatFilter == null ^ value != null || value?.Id != _cat4CatFilter?.Id)
		//		{
		//			_cat4CatFilter = value; RaisePropertyChanged_UI();
		//			UpdateIdsForCatFilterAsync().ContinueWith(delegate
		//			{
		//				if (_isByCatFolderPaneOpen) { Task upd = UpdatePaneContentAsync(0); }
		//			});
		//		}
		//	}
		//}
		//// LOLLO TODO test end
		private string _catNameForCatFilter = null;
		public string CatNameForCatFilter
		{
			get { return _catNameForCatFilter; }
			set
			{
				string newValue = value ?? string.Empty;
				if (_catNameForCatFilter != newValue)
				{
					_catNameForCatFilter = newValue; RaisePropertyChanged_UI();
					UpdateIdsForCatFilterAsync().ContinueWith(delegate
					{
						if (_isByCatFolderPaneOpen) { Task upd = UpdatePaneContentAsync(0); }
					});
				}
			}
		}
		private async Task UpdateIdsForCatFilterAsync()
		{
			var binder = _binder; if (binder == null) return;
			await binder.SetIdsForCatFilterAsync(_metaBriefcase?.Categories?.FirstOrDefault(cat => cat.Name == _catNameForCatFilter)?.Id).ConfigureAwait(false);
			//// LOLLO TODO test begin
			//await binder.SetIdsForCatFilterAsync(_cat4CatFilter?.Id).ConfigureAwait(false);
			//// LOLLO TODO test end
		}
		private void UpdateDataForCatFilter()
		{
			var binder = _binder; if (binder == null) return;

			CatNameForCatFilter = _metaBriefcase?.Categories?.FirstOrDefault(cat => cat.Id == binder.CatIdForCatFilter)?.Name;
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
			SetFldDscCaptionForFldFilter(fldDsc?.Caption, false); Debug.WriteLine("called SetFldDscCaptionForFldFilter with value = " + fldDsc?.Caption);
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

		IAnimationStarter _animationStarter = null;
		#endregion properties


		#region construct dispose open close
		public BinderCoverVM(Binder binder, IAnimationStarter animationStarter)
		{
			if (binder == null) throw new ArgumentNullException("BinderCoverVM ctor: binder may not be null");
			if (animationStarter == null) throw new ArgumentNullException("BinderCoverVM ctor: animationStarter may not be null");

			Binder = binder;
			MetaBriefcase = MetaBriefcase.OpenInstance;
			if (_metaBriefcase == null) Debugger.Break(); // LOLLO this must never happen, check it
			_animationStarter = animationStarter;

			UpdateOpenClose();
		}

		protected override Task OpenMayOverrideAsync()
		{
			_binder.PropertyChanged += OnBinder_PropertyChanged; // throws if _binder is null
			RegisterFolderChanged();

			UpdateDataForCatFilter();
			UpdateDataForFldFilter();

			//_refreshIntervalMsec = REFRESH_INTERVAL_SHORT_MSEC;
			SetIsDirty(true);
			UpdateIsPaneOpen();

			return Task.CompletedTask;
		}

		protected override Task CloseMayOverrideAsync()
		{
			var binder = _binder;
			if (binder != null) binder.PropertyChanged -= OnBinder_PropertyChanged;
			UnregisterFolderChanged();
			return Task.CompletedTask;
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_folderPreviews?.Dispose();
			_folderPreviews = null;
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
			if (_binder?.IsOpen == true)
			{
				Task open = OpenAsync().ContinueWith(delegate { Task upd = UpdatePaneContentAsync(0); });
			}
			else
			{
				Task close = CloseAsync();
			}
		}
		#endregion construct dispose open close


		#region binder data
		private int _refreshIntervalMsec = REFRESH_INTERVAL_LONG_MSEC;

		private bool IsNeedRefresh { get { return (_isAllFoldersDirty && _isAllFolderPaneOpen) || (_isRecentFoldersDirty && _isRecentFolderPaneOpen) || _isByCatFolderPaneOpen || _isByFldFolderPaneOpen; } }

		private async Task UpdatePaneContentAsync(int waitMsec)
		{
			_refreshIntervalMsec = REFRESH_INTERVAL_LONG_MSEC;

			if (IsNeedRefresh && IsOpen)
			{
				try
				{
					_animationStarter.StartAnimation();
					await Task.Delay(waitMsec).ConfigureAwait(false);
					Debug.WriteLine("Finished waiting " + waitMsec + " msec");

					await RunFunctionWhileOpenAsyncT(async delegate
					{
						if (IsNeedRefresh)
						{
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
							if (/*_isByCatFoldersDirty &&*/ _isByCatFolderPaneOpen)
							{
								await Task.Run(delegate { return ReadByCatFoldersAsync(); }).ConfigureAwait(false);
								//IsByCatFoldersDirty = false;
							}
							if (/*_isByFldFoldersDirty &&*/ _isByFldFolderPaneOpen)
							{
								await Task.Run(delegate { return ReadByFldFoldersAsync(); }).ConfigureAwait(false);
								//IsByFldFoldersDirty = false;
							}
						}
					});
				}
				finally
				{
					_animationStarter.EndAnimation();
				}
			}
		}

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
			return CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
			{
				_folderPreviews?.Clear();
				_folderPreviews?.AddRange(folderPreviews);
			}).AsTask();
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
			if (e.OldItems != null)
				foreach (Folder fol in e.OldItems)
				{
					fol.PropertyChanged -= OnFol_PropertyChanged;
					fol.Wallets.CollectionChanged -= OnFolWal_CollectionChanged;
					SetIsDirty(true);
				}
			if (e.NewItems != null)
				foreach (Folder fol in e.NewItems)
				{
					fol.PropertyChanged += OnFol_PropertyChanged;
					fol.Wallets.CollectionChanged += OnFolWal_CollectionChanged;
					SetIsDirty(true);
				}
		}

		private void OnFolWal_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (e.OldItems != null)
				foreach (Wallet wal in e.OldItems)
				{
					wal.Documents.CollectionChanged -= OnFolWalDoc_CollectionChanged;
					SetIsDirty(true);
				}
			if (e.NewItems != null)
				foreach (Wallet wal in e.NewItems)
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
			if (e.OldItems != null)
				foreach (Document doc in e.OldItems)
				{
					doc.PropertyChanged -= OnFolWalDoc_PropertyChanged;
					SetIsDirty(true);
				}
			if (e.NewItems != null)
				foreach (Document doc in e.NewItems)
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


		#region user actions
		public async Task SelectFolderAsync(string folderId)
		{
			if (!string.IsNullOrWhiteSpace(folderId))
			{
				await _binder?.SetCurrentFolderIdAsync(folderId);
				_binder?.SetIsCoverOpen(false);
			}
		}

		public async Task DeleteFolderAsync(Binder.FolderPreview fp)
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
		public void CloseCover()
		{
			_binder?.SetIsCoverOpen(false);
		}
		public void GoBack()
		{
			var opener = _binder?.ParentPaneOpener;
			if (opener != null)
			{
				// _binder.SetIsCoverOpen(false);
				opener.IsShowingCover = true;
			}

		}
		public void ShowSettings()
		{
			var opener = _binder?.ParentPaneOpener;
			if (opener != null)
			{
				_binder.SetIsCoverOpen(false);
				opener.IsShowingSettings = true;
			}
		}
		#endregion actions
	}
}
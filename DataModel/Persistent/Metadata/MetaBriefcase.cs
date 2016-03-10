using Microsoft.OneDrive.Sdk;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.Runtime;
using Utilz;
using Utilz.Data;
using Windows.Storage;
using UniFiler10.Data.Constants;
using System.Net.Http.Headers;
using System.Threading;
using UniFiler10.Data.Model;
using System.Globalization;
using System.Collections.Generic;

namespace UniFiler10.Data.Metadata
{
	[DataContract]
	public sealed class MetaBriefcase : OpenableObservableData
	{
		#region data properties
		private static readonly string DEFAULT_ID = string.Empty;
		private static MetaBriefcase _instance = null;
		[IgnoreDataMember]
		public static MetaBriefcase OpenInstance
		{
			get
			{
				lock (_instanceLocker)
				{
					if (_instance?._isOpen == true) return _instance;
					else return null;
				}
			}
		}

		private string _currentCategoryId = DEFAULT_ID;
		[DataMember]
		public string CurrentCategoryId
		{
			get { return _currentCategoryId; }
			private set
			{
				var newValue = value ?? DEFAULT_ID;
				if (_currentCategoryId != newValue)
				{
					_currentCategoryId = newValue;
					UpdateCurrentCategory2();
				}
				else if (_currentCategory == null)
				{
					UpdateCurrentCategory2();
				}
			}
		}

		private Category _currentCategory = null;
		[IgnoreDataMember]
		public Category CurrentCategory { get { return _currentCategory; } private set { if (_currentCategory != value) { _currentCategory = value; RaisePropertyChanged_UI(); } } }
		private void UpdateCurrentCategory2()
		{
			if (_categories != null && _currentCategoryId != null)
			{
				CurrentCategory = _categories.FirstOrDefault(cat => cat.Id == _currentCategoryId);
			}
			else
			{
				CurrentCategory = null;
			}
		}

		private string _currentFieldDescriptionId = DEFAULT_ID;
		[DataMember]
		public string CurrentFieldDescriptionId
		{
			get { return _currentFieldDescriptionId; }
			private set
			{
				var newValue = value ?? DEFAULT_ID;
				if (_currentFieldDescriptionId != newValue)
				{
					_currentFieldDescriptionId = newValue;
					UpdateCurrentFieldDescription2();
				}
				else if (_currentFieldDescription == null)
				{
					UpdateCurrentFieldDescription2();
				}
			}
		}

		private FieldDescription _currentFieldDescription = null;
		[IgnoreDataMember]
		public FieldDescription CurrentFieldDescription { get { return _currentFieldDescription; } private set { if (_currentFieldDescription != value) { _currentFieldDescription = value; RaisePropertyChanged_UI(); } } }
		private void UpdateCurrentFieldDescription2()
		{
			if (_fieldDescriptions != null && _currentFieldDescriptionId != null)
			{
				CurrentFieldDescription = _fieldDescriptions.FirstOrDefault(fd => fd.Id == _currentFieldDescriptionId);
				//CurrentFieldDescription = _currentCategory.FieldDescriptions.FirstOrDefault(fd => fd.Id == _currentFieldDescriptionId);
			}
			else
			{
				CurrentFieldDescription = null;
			}
		}

		// we cannot make this readonly because it is serialised. we only use the setter for serialising.
		private SwitchableObservableCollection<Category> _categories = new SwitchableObservableCollection<Category>();
		[DataMember]
		public SwitchableObservableCollection<Category> Categories { get { return _categories; } private set { _categories = value; RaisePropertyChanged_UI(); } }

		// we cannot make this readonly because it is serialised. we only use the setter for serialising.
		private SwitchableObservableCollection<FieldDescription> _fieldDescriptions = new SwitchableObservableCollection<FieldDescription>();
		[DataMember]
		public SwitchableObservableCollection<FieldDescription> FieldDescriptions { get { return _fieldDescriptions; } private set { _fieldDescriptions = value; RaisePropertyChanged_UI(); } }

		private readonly object _propLocker = new object();
		private bool _isElevated = false; // this must not be serialised because it does not belong in the metadata xml, so it has its own place in the registry.
		[IgnoreDataMember]
		public bool IsElevated
		{
			get
			{
				return GetPropertyLocking(ref _isElevated, _propLocker);
			}
			set
			{
				SetPropertyLocking(ref _isElevated, value, _propLocker);
			}
		}

		private readonly RuntimeData _runtimeData = null;
		private readonly Briefcase _briefcase = null;
		#endregion properties


		#region load properties
		private readonly MetaBriefcaseOneDriveReaderWriter _oneDriveReaderWriter = null;

		private bool _isPropsLoaded = false;
		[IgnoreDataMember]
		private bool IsPropsLoaded
		{
			get { lock (_propLocker) { return _isPropsLoaded; } }
			set { lock (_propLocker) { _isPropsLoaded = value; } }
		}

		internal static bool GetIsLocalSyncedOnceSinceLastOpen()
		{
			try
			{
				_localDataSemaphore.WaitOne();
				string regVal = RegistryAccess.GetValue(ConstantData.REG_MBC_ODU_LOCAL_SYNCED_SINCE_OPEN).ToLower();
				if (string.IsNullOrWhiteSpace(regVal)) return false;
				return regVal.Equals(true.ToString().ToLower());
			}
			finally
			{
				SemaphoreExtensions.TryRelease(_localDataSemaphore);
			}
		}
		private static void SetIsLocalSyncedOnceSinceLastOpen(bool value)
		{
			bool newValue = value.ToString().ToLower().Equals(true.ToString().ToLower());
			RegistryAccess.TrySetValue(ConstantData.REG_MBC_ODU_LOCAL_SYNCED_SINCE_OPEN, newValue.ToString().ToLower());
		}

		public class OpenParameters
		{
			public StorageFile SourceFile { get; }
			public bool IsReloadProps { get; }
			public bool IsLoadFromOneDriveThisOneTime { get; }
			public bool IsLocalSyncedOnceSinceLastOpen { get; internal set; }
			public OpenParameters(StorageFile sourceFile, bool isReloadProps, bool isLoadFromOneDriveThisOneTime)
			{
				SourceFile = sourceFile;
				IsReloadProps = isReloadProps;
				IsLoadFromOneDriveThisOneTime = isLoadFromOneDriveThisOneTime;
				IsLocalSyncedOnceSinceLastOpen = false;
			}
		}
		#endregion load properties


		#region rubbish bin properties
		private readonly MetaBriefcaseRubbishBin _rubbishBin = null;
		#endregion rubbish bin properties


		#region lifecycle
		private static readonly object _instanceLocker = new object();
		internal static MetaBriefcase GetInstance(RuntimeData runtimeData, Briefcase briefcase)
		{
			lock (_instanceLocker)
			{
				if (_instance == null)
				{
					_instance = new MetaBriefcase(runtimeData, briefcase);
				}
				return _instance;
			}
		}

		private MetaBriefcase(RuntimeData runtimeData, Briefcase briefcase)
		{
			_briefcase = briefcase;
			_runtimeData = runtimeData;

			_oneDriveReaderWriter = new MetaBriefcaseOneDriveReaderWriter(_briefcase, _runtimeData);
			_rubbishBin = new MetaBriefcaseRubbishBin(this);
		}

		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			var openParams = args as OpenParameters ?? new OpenParameters(null, false, _briefcase.IsWantToUseOneDrive);
			bool wantToUseOneDriveNowOrLater = _briefcase.IsWantToUseOneDrive;

			await _oneDriveReaderWriter.LogonAsync(wantToUseOneDriveNowOrLater).ConfigureAwait(false);
			try
			{
				_localDataSemaphore.WaitOne();
				await LoadAsync(wantToUseOneDriveNowOrLater, openParams).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
			finally
			{
				SemaphoreExtensions.TryRelease(_localDataSemaphore);
			}

			await _oneDriveReaderWriter.OpenAsync().ConfigureAwait(false);
			await _rubbishBin.OpenAsync().ConfigureAwait(false);
		}

		protected override async Task CloseMayOverrideAsync()
		{
			await _rubbishBin.CloseAsync().ConfigureAwait(false);
			await _oneDriveReaderWriter.CloseAsync().ConfigureAwait(false);
			//await Save2Async().ConfigureAwait(false); // LOLLO TODO see if I need this, I am trying to do fewer saves
			// RaiseUpdateOneDriveMetaBriefcaseRequested();
		}
		#endregion lifecycle


		#region loading methods
		internal const string FILENAME = "LolloSessionDataMetaBriefcase.xml";

		private async Task LoadAsync(bool wantToUseOneDriveNowOrLater, OpenParameters openParams)
		{
			if (IsPropsLoaded && !openParams.IsReloadProps /*&& IsLocalSyncedOnceSinceLastOpen*/) return;

			// LOLLO NOTE on the onedrive sdk
			// http://blogs.u2u.net/diederik/post/2015/04/06/Using-the-OneDrive-SDK-in-universal-apps.aspx
			// https://msdn.microsoft.com/en-us/magazine/mt632271.aspx
			// https://onedrive.live.com/?authkey=%21ADtqHIG1cV7g5EI&cid=40CFFDE85F1AB56A&id=40CFFDE85F1AB56A%212187&parId=40CFFDE85F1AB56A%212186&action=locate

			StorageFile localFile = null;
			bool mustSaveLocal = false;
			bool mustSyncOneDrive = false;
			MetaBriefcase newMetaBriefcase = null;

			try
			{
				localFile = openParams.SourceFile ?? await GetDirectory()
					.CreateFileAsync(FILENAME, CreationCollisionOption.OpenIfExists)
					.AsTask().ConfigureAwait(false);
			}
			catch (Exception ex) { Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename); }

			if (CancToken.IsCancellationRequested) return;

			var serializer = new DataContractSerializer(typeof(MetaBriefcase));
			if (wantToUseOneDriveNowOrLater && openParams.IsLoadFromOneDriveThisOneTime)
			{
				if (wantToUseOneDriveNowOrLater && _runtimeData.IsConnectionAvailable)
				{
					await _oneDriveReaderWriter.RunUnderSemaphore(async () =>
					{
						if (MetaBriefcaseOneDriveReaderWriter.GetIsOneDriveUpdateOverdue2())
						{// LOLLO TODO load form one drive and merge remote with local: check it
							var localMetaBriefcase = await LoadFromFile(localFile, serializer).ConfigureAwait(false);
							var remoteMetaBriefcase = (await _oneDriveReaderWriter.TryReadMetaBriefcase2Async().ConfigureAwait(false)).Item1;

							newMetaBriefcase = Merge(localMetaBriefcase, remoteMetaBriefcase);
							mustSaveLocal = true;
							mustSyncOneDrive = true;
							openParams.IsLocalSyncedOnceSinceLastOpen = true;
						}
						else
						{
							var mbcAndException = await _oneDriveReaderWriter.TryReadMetaBriefcase2Async().ConfigureAwait(false);
							if (mbcAndException.Item1 != null)
							{
								newMetaBriefcase = mbcAndException.Item1;
								mustSaveLocal = true;
								openParams.IsLocalSyncedOnceSinceLastOpen = true;
							}
							else if (mbcAndException.Item2 is SerializationException)
							{
								newMetaBriefcase = await LoadFromFile(localFile, serializer).ConfigureAwait(false);
								mustSyncOneDrive = true;
								openParams.IsLocalSyncedOnceSinceLastOpen = true;
							}
							else
							{
								newMetaBriefcase = await LoadFromFile(localFile, serializer).ConfigureAwait(false);
								openParams.IsLocalSyncedOnceSinceLastOpen = false;
							}
						}
					}).ConfigureAwait(false);
				}
				else // do not want or cannot use OneDive
				{
					newMetaBriefcase = await LoadFromFile(localFile, serializer).ConfigureAwait(false);
					openParams.IsLocalSyncedOnceSinceLastOpen = false;
				}
			}
			else // push the data from here into OneDrive
			{
				newMetaBriefcase = await LoadFromFile(localFile, serializer).ConfigureAwait(false);
				mustSyncOneDrive = true;
				openParams.IsLocalSyncedOnceSinceLastOpen = true;
			}
			SetIsLocalSyncedOnceSinceLastOpen(openParams.IsLocalSyncedOnceSinceLastOpen);

			if (newMetaBriefcase != null) // if I could pick up some data, use it and sync whatever needs syncing
			{
				IsPropsLoaded = true;
				CopyXMLPropertiesFrom(newMetaBriefcase);
				if (mustSaveLocal)
				{
					Task syncLocal = Task.Run(() => Save2Async(), CancToken);
				}
				if (mustSyncOneDrive)
				{
					_oneDriveReaderWriter.RaiseUpdateOneDriveMetaBriefcaseRequested();
					// I don't use the following, but it is interesting and it works:
					//Task saveToOneDrive = Task.Run(() => SaveToOneDrive(localFileContent, _oneDriveAccountSession.AccessToken), CancToken).ContinueWith(state => localFileContent?.Dispose());
				}
			}

			// load non-xml properties
			bool isElevated = false;
			bool.TryParse(RegistryAccess.GetValue(ConstantData.REG_MBC_IS_ELEVATED), out isElevated);
			IsElevated = isElevated;

			Debug.WriteLine("ended method MetaBriefcase.LoadAsync()");
		}

		private async Task<MetaBriefcase> LoadFromFile(StorageFile file, DataContractSerializer serializer)
		{
			MetaBriefcase newMetaBriefcase = null;
			try
			{
				using (var localFileContent = await file.OpenStreamForReadAsync().ConfigureAwait(false))
				{
					newMetaBriefcase = (MetaBriefcase)(serializer.ReadObject(localFileContent));
				}
			}
			catch (Exception ex1) { Logger.Add_TPL(ex1.ToString(), Logger.FileErrorLogFilename); }
			return newMetaBriefcase;
		}

		internal async Task<string> GetLocalFileContentString()
		{
			string localFileContentString = string.Empty;
			try
			{
				_localDataSemaphore.WaitOne();
				var localFile = await GetDirectory().TryGetItemAsync(FILENAME).AsTask(CancToken).ConfigureAwait(false) as StorageFile;
				if (localFile == null) return null;

				using (var localFileContent = await localFile.OpenStreamForReadAsync().ConfigureAwait(false))
				{
					using (var streamReader = new StreamReader(localFileContent))
					{
						localFileContentString = streamReader.ReadToEnd();
					}
				}
			}
			finally { SemaphoreExtensions.TryRelease(_localDataSemaphore); }
			return localFileContentString;
		}

		private async Task<bool> Save2Async(StorageFile file = null)
		{
			//for (int i = 0; i < 100000000; i++) //wait a few seconds, for testing
			//{
			//    String aaa = i.ToString();
			//}
			await Logger.AddAsync("MetaBriefcase about to save", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			bool result = false;

			// save xml properties
			try
			{
				_localDataSemaphore.WaitOne();

				if (file == null)
				{
					file = await GetDirectory()
						.TryGetItemAsync(FILENAME).AsTask().ConfigureAwait(false) as StorageFile;
				}

				string savedData = string.Empty;
				if (file != null)
				{
					using (var localFileContent = await file.OpenStreamForReadAsync().ConfigureAwait(false))
					{
						using (StreamReader streamReader = new StreamReader(localFileContent))
						{
							savedData = streamReader.ReadToEnd();
						}
					}
				}

				using (var memoryStream = new MemoryStream())
				{
					var serializer = new DataContractSerializer(typeof(MetaBriefcase));
					serializer.WriteObject(memoryStream, this);

					string currentMetaBriefcase = string.Empty;
					memoryStream.Seek(0, SeekOrigin.Begin);
					using (StreamReader streamReader = new StreamReader(memoryStream))
					{
						currentMetaBriefcase = streamReader.ReadToEnd();

						if (!currentMetaBriefcase.Trim().Equals(savedData.Trim()))
						{
							if (file == null)
							{
								file = await GetDirectory()
									.CreateFileAsync(FILENAME, CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
							}

							using (var fileStream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
							{
								fileStream.SetLength(0); // avoid leaving crap at the end if overwriting a file that was longer
								memoryStream.Seek(0, SeekOrigin.Begin);
								await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
								await memoryStream.FlushAsync().ConfigureAwait(false);
								await fileStream.FlushAsync().ConfigureAwait(false);
							}
						}
					}
				}
				Debug.WriteLine("ended method MetaBriefcase.SaveAsync()");
				result = true;
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
			finally
			{
				// save non-xml properties
				result = result & RegistryAccess.TrySetValue(ConstantData.REG_MBC_IS_ELEVATED, IsElevated.ToString());
				SemaphoreExtensions.TryRelease(_localDataSemaphore);
			}

			return result;
		}

		/// <summary>
		/// This semaphore protects the local file, operating system-wide
		/// </summary>
		private static readonly Semaphore _localDataSemaphore = new Semaphore(1, 1, "Unifiler10_MetaBriefcase_LocalDataSemaphore");

		public Task SaveIntoOneDriveAsync(CancellationToken cancToken, Guid taskInstanceId)
		{
			return _oneDriveReaderWriter.SaveIntoOneDriveAsync(cancToken, taskInstanceId);
		}

		private bool CopyXMLPropertiesFrom(MetaBriefcase source)
		{
			if (source == null) return false;

			//IsElevated = source._isElevated; // NO!
			FieldDescription.Copy(source._fieldDescriptions, ref _fieldDescriptions);
			RaisePropertyChanged_UI(nameof(FieldDescriptions));
			Category.Copy(source._categories, ref _categories, _fieldDescriptions);
			RaisePropertyChanged_UI(nameof(Categories));

			CurrentCategoryId = source._currentCategoryId; // must come after setting the categories
			UpdateCurrentCategory2();

			CurrentFieldDescriptionId = source._currentFieldDescriptionId; // must come after setting the current category
			UpdateCurrentFieldDescription2();

			return true;
		}
		public static StorageFolder GetDirectory()
		{
			return ApplicationData.Current.LocalFolder;
		}
		#endregion loading methods


		#region data methods
		public Task SetCurrentCategoryAsync(Category cat)
		{
			return RunFunctionIfOpenAsyncA(delegate
			{
				if (cat != null)
				{
					CurrentCategoryId = cat.Id;
				}
			});
		}

		public Task SetCurrentFieldDescriptionAsync(FieldDescription fldDsc)
		{
			return RunFunctionIfOpenAsyncA(delegate
			{
				if (fldDsc != null)
				{
					CurrentFieldDescriptionId = fldDsc.Id;
				}
			});
		}

		public Task<bool> AddNewCategoryAsync()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				string name = RuntimeData.GetText("NewCategory");
				var newCat = new Category(name, true, true);

				if (Category.Check(newCat) && !Categories.Any(cat => cat.Name == newCat.Name || cat.Id == newCat.Id))
				{
					await RunInUiThreadAsync(() => _categories.Add(newCat)).ConfigureAwait(false);
					return true;
				}
				return false;
			});
		}
		public Task<bool> RemoveCategoryAsync(Category cat)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (cat != null && (cat.IsJustAdded || IsElevated))
				{
					bool isRemoved = false;
					await RunInUiThreadAsync(() => isRemoved = _categories.Remove(cat)).ConfigureAwait(false);
					await _rubbishBin.AddCategoryAsync(cat);
					if (CurrentCategoryId == cat.Id && _categories.Any()) CurrentCategoryId = _categories[0]?.Id;
					return isRemoved;
				}
				else
				{
					return false;
				}
			});
		}

		public Task<bool> IsCategoryAvailableAsync(string catId)
		{
			return RunFunctionIfOpenAsyncB(delegate
			{
				return !string.IsNullOrWhiteSpace(catId) && _categories.FirstOrDefault(cat => cat.Id == catId) != null;
			});
		}

		public Task<bool> AddNewFieldDescriptionAsync()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				string name = RuntimeData.GetText("NewFieldDescription");
				var fldDsc = new FieldDescription(name, true, true);

				if (FieldDescription.Check(fldDsc) && !_fieldDescriptions.Any(fd => fd.Caption == fldDsc.Caption || fd.Id == fldDsc.Id))
				{
					await RunInUiThreadAsync(() => _fieldDescriptions.Add(fldDsc)).ConfigureAwait(false);
					return true;
				}
				return false;
			});
		}

		public Task<bool> RemoveFieldDescriptionAsync(FieldDescription fldDsc)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (fldDsc != null && (fldDsc.IsJustAdded || IsElevated))
				{
					bool isRemoved = false;
					var catsWithFldDsc = new List<string>();
					await RunInUiThreadAsync(delegate
					{
						foreach (var cat in _categories)
						{
							if (cat.RemoveFieldDescription(fldDsc)) catsWithFldDsc.Add(cat.Id);
						}
						isRemoved = _fieldDescriptions.Remove(fldDsc);
					}).ConfigureAwait(false);

					await _rubbishBin.AddFieldDescriptionAsync(catsWithFldDsc, fldDsc);
					if (CurrentFieldDescriptionId == fldDsc.Id)
					{
						if (_fieldDescriptions.Any()) CurrentFieldDescriptionId = _fieldDescriptions[0]?.Id;
						else CurrentFieldDescriptionId = null;
					}
					return isRemoved;
				}
				return false;
			});
		}

		public Task<bool> AddNewPossibleValueToCurrentFieldDescriptionAsync()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (_currentFieldDescription == null) return false;

				string name = RuntimeData.GetText("NewFieldValue");
				var newFldVal = new FieldValue(name, true, true);

				bool isAdded = false;
				await RunInUiThreadAsync(() => isAdded = _currentFieldDescription.AddPossibleValue(newFldVal)).ConfigureAwait(false);
				return isAdded;
			});
		}

		/// <summary>
		/// Save metaBriefcase, in case there is a crash before the next Suspend.
		/// This is the only method that is not called by the VM, which saves when closing.
		/// </summary>
		/// <param name="fldDsc"></param>
		/// <param name="newFldVal"></param>
		/// <param name="save"></param>
		/// <returns></returns>
		public Task<bool> AddPossibleValueToFieldDescriptionAsync(FieldDescription fldDsc, FieldValue newFldVal, bool save)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (fldDsc == null || newFldVal == null) return false;

				bool isAdded = false;

				// LOLLO TODO check if I need or want this
				var recycledFldVal = await _rubbishBin.GetPossibleValueAsync(fldDsc, newFldVal.Vaalue); //.ConfigureAwait(false);
				if (recycledFldVal?.Item2 != null) newFldVal = recycledFldVal.Item2;

				await RunInUiThreadAsync(() => isAdded = fldDsc.AddPossibleValue(newFldVal)).ConfigureAwait(false);
				if (isAdded && save)
				{
					isAdded = await Save2Async().ConfigureAwait(false);
					_oneDriveReaderWriter.RaiseUpdateOneDriveMetaBriefcaseRequested();
				}
				return isAdded;
			});
		}

		public Task<bool> RemovePossibleValueFromCurrentFieldDescriptionAsync(FieldValue fldVal)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				var currFldDsc = _currentFieldDescription;
				if (fldVal == null || currFldDsc == null || (!fldVal.IsJustAdded && !IsElevated)) return false;

				bool isRemoved = false;
				await RunInUiThreadAsync(() => isRemoved = currFldDsc.RemovePossibleValue(fldVal)).ConfigureAwait(false);
				await _rubbishBin.AddPossibleValueAsync(currFldDsc, fldVal).ConfigureAwait(false);
				return isRemoved;
			});
		}

		public Task<bool> AssignFieldDescriptionToCurrentCategoryAsync(FieldDescription fldDsc)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (fldDsc == null || _currentCategory == null) return false;

				bool isAdded = false;
				await RunInUiThreadAsync(() => isAdded = _currentCategory.AddFieldDescription(fldDsc)).ConfigureAwait(false);
				return isAdded;
			});
		}

		public Task<bool> UnassignFieldDescriptionFromCurrentCategoryAsync(FieldDescription fldDsc)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (fldDsc == null || _currentCategory == null || (!fldDsc.JustAssignedToCats.Contains(_currentCategoryId) && !IsElevated)) return false;

				bool isRemoved = false;
				await RunInUiThreadAsync(() => isRemoved = _currentCategory.RemoveFieldDescription(fldDsc)).ConfigureAwait(false);
				return isRemoved;
			});
		}

		public async Task<bool> SaveACopyAsync(StorageFile file)
		{
			if (file != null) return await RunFunctionIfOpenAsyncTB(() => Save2Async(file)).ConfigureAwait(false);
			else return false;
		}

		public async Task<bool> SaveAsync()
		{
			bool result = false;
			await RunFunctionIfOpenAsyncT(async () =>
			{
				result = await Save2Async().ConfigureAwait(false);
				if (result) _oneDriveReaderWriter.RaiseUpdateOneDriveMetaBriefcaseRequested();
			});
			return result;
		}

		internal static MetaBriefcase Merge(MetaBriefcase one, MetaBriefcase two)
		{
			if (one?.Categories == null || one?.FieldDescriptions == null) return two;
			if (two?.Categories == null || two?.FieldDescriptions == null) return one;

			foreach (var fdOne in one.FieldDescriptions)
			{
				var fdTwo = two.FieldDescriptions.FirstOrDefault(fd => fd.Id == fdOne.Id);
				if (fdTwo != null)
				{
					foreach (var pvOne in fdOne.PossibleValues.Where(pv1 => fdTwo.PossibleValues.All(pv2 => pv2.Id != pv1.Id)))
					{
						fdTwo.PossibleValues.Add(pvOne);
					}
				}
			}
			foreach (var fdTwo in two.FieldDescriptions)
			{
				var fdOne = one.FieldDescriptions.FirstOrDefault(fd => fd.Id == fdTwo.Id);
				if (fdOne != null)
				{
					foreach (var pvTwo in fdTwo.PossibleValues.Where(pv2 => fdOne.PossibleValues.All(pv1 => pv1.Id != pv2.Id)))
					{
						fdOne.PossibleValues.Add(pvTwo);
					}
				}
			}

			foreach (var catOne in one.Categories)
			{
				var catTwo = two.Categories.FirstOrDefault(cat => cat.Id == catOne.Id);
				if (catTwo != null)
				{
					foreach (var fdOne in catOne.FieldDescriptionIds.Where(fd1 => catTwo.FieldDescriptionIds.All(fd2 => fd2 != fd1)))
					{
						catTwo.FieldDescriptionIds.Add(fdOne);
					}
				}
			}

			foreach (var catTwo in two.Categories)
			{
				var catOne = one.Categories.FirstOrDefault(cat => cat.Id == catTwo.Id);
				if (catOne != null)
				{
					foreach (var fdTwo in catTwo.FieldDescriptionIds.Where(fd2 => catOne.FieldDescriptionIds.All(fd1 => fd1 != fd2)))
					{
						catOne.FieldDescriptionIds.Add(fdTwo);
					}
				}
			}

			foreach (var fdInOne in one.FieldDescriptions.Where(fd1 => two.FieldDescriptions.All(fd2 => fd2.Id != fd1.Id && fd2.Caption != fd1.Caption)))
			{
				two.FieldDescriptions.Add(fdInOne);
			}
			foreach (var fdInTwo in two.FieldDescriptions.Where(fd2 => one.FieldDescriptions.All(fd1 => fd1.Id != fd2.Id && fd1.Caption != fd2.Caption)))
			{
				one.FieldDescriptions.Add(fdInTwo);
			}
			foreach (var fdInOne in one.FieldDescriptions)
			{
				var fdInTwo = two.FieldDescriptions.FirstOrDefault(fd => fd.Id == fdInOne.Id);
				if (fdInTwo != null)
				{
					bool isAnyValueAllowedTolerant = fdInOne.IsAnyValueAllowed | fdInTwo.IsAnyValueAllowed;
					fdInOne.IsAnyValueAllowed = fdInTwo.IsAnyValueAllowed = isAnyValueAllowedTolerant;
				}
			}

			foreach (var catInOne in one.Categories.Where(cat1 => two.Categories.All(cat2 => cat2.Id != cat1.Id && cat2.Name != cat1.Name)))
			{
				two.Categories.Add(catInOne);
			}
			foreach (var catInTwo in two.Categories.Where(cat2 => one.Categories.All(cat1 => cat1.Id != cat2.Id && cat1.Name != cat2.Name)))
			{
				one.Categories.Add(catInTwo);
			}

			return one;
		}
		#endregion data methods
	}

	public class MetaBriefcaseOneDriveReaderWriter : OpenableObservableData
	{
		#region events
		public static event EventHandler UpdateOneDriveMetaBriefcaseRequested;
		internal void RaiseUpdateOneDriveMetaBriefcaseRequested()
		{
			if (!_briefcase?.IsWantToUseOneDrive == true) return;
			RunUnderSemaphore(() => RaiseUpdateOneDriveMetaBriefcaseRequested2());
		}
		private void RaiseUpdateOneDriveMetaBriefcaseRequested2()
		{
			if (_briefcase?.IsWantToUseOneDrive == true)
			{
				Task raise = Task.Run(() =>
				{
					SetOneDriveUpdateCalledNow2();
					UpdateOneDriveMetaBriefcaseRequested?.Invoke(this, EventArgs.Empty);
				});
			}
		}
		#endregion events


		#region properties
		private static readonly string[] _oneDriveScopes = { "onedrive.readwrite", "onedrive.appfolder", "wl.signin", "wl.offline_access", "wl.skydrive", "wl.skydrive_update" };
		//private const string _oneDriveAppRootUri = "https://api.onedrive.com/v1.0/drive/special/approot/";
		private const string _oneDriveAppRootUri4Path = "https://api.onedrive.com/v1.0/drive/special/approot:/"; // this is useful if you don't know the file ids but you know the paths

		/// <summary>
		/// This semaphore protects the one drive data, operating system-wide
		/// </summary>
		private static readonly Semaphore _oneDriveSemaphore = new Semaphore(1, 1, "Unifiler10_MetaBriefcase_OneDriveSemaphore");

		/// <summary>
		/// This semaphore protects the one drive background task cancellation, operating system-wide
		/// </summary>
		private static readonly Semaphore _oneDriveCancelBkgTaskSemaphore = new Semaphore(1, 1, "Unifiler10_MetaBriefcase_OneDriveCancelBkgTaskSemaphore");
		private static bool GetIsCurrentBackgroundTaskInstanceAllowed(Guid id)
		{
			if (id == null) return false;
			try
			{
				_oneDriveCancelBkgTaskSemaphore.WaitOne();
				string regVal = RegistryAccess.GetValue(ConstantData.ODU_BACKGROUND_TASK_ALLOWED_INSTANCE_ID);
				return regVal.Equals(id.ToString());
			}
			finally
			{
				SemaphoreExtensions.TryRelease(_oneDriveCancelBkgTaskSemaphore);
			}
		}
		private static void SetAllowedBackgroundTaskInstance(Guid id)
		{
			try
			{
				_oneDriveCancelBkgTaskSemaphore.WaitOne();
				RegistryAccess.TrySetValue(ConstantData.ODU_BACKGROUND_TASK_ALLOWED_INSTANCE_ID, id.ToString());
			}
			finally
			{
				SemaphoreExtensions.TryRelease(_oneDriveCancelBkgTaskSemaphore);
			}
		}

		private static string OneDriveAccessToken2
		{
			get
			{
				return RegistryAccess.GetValue(ConstantData.REG_MBC_ODU_TKN);
			}
			set
			{
				RegistryAccess.TrySetValue(ConstantData.REG_MBC_ODU_TKN, value);
			}
		}

		internal static bool GetIsOneDriveUpdateOverdue2()
		{
			DateTime lastTimeUpdateOneDriveCalled = default(DateTime);
			DateTime.TryParse(RegistryAccess.GetValue(ConstantData.REG_MBC_LAST_TIME_UPDATE_ONEDRIVE_CALLED), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out lastTimeUpdateOneDriveCalled);

			DateTime lastTimeUpdateOneDriveRan = default(DateTime);
			DateTime.TryParse(RegistryAccess.GetValue(ConstantData.REG_MBC_LAST_TIME_UPDATE_ONEDRIVE_RAN), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out lastTimeUpdateOneDriveRan);

			return lastTimeUpdateOneDriveCalled >= lastTimeUpdateOneDriveRan && lastTimeUpdateOneDriveCalled > default(DateTime);
		}

		private static void SetIsOneDriveUpdateOverdue2()
		{
			string newValue = default(DateTime).ToUniversalTime().ToString(CultureInfo.InvariantCulture);
			RegistryAccess.TrySetValue(ConstantData.REG_MBC_LAST_TIME_UPDATE_ONEDRIVE_RAN, newValue);
		}

		private static void SetOneDriveUpdateCalledNow2()
		{
			string newValue = DateTime.Now.ToUniversalTime().ToString(CultureInfo.InvariantCulture);
			RegistryAccess.TrySetValue(ConstantData.REG_MBC_LAST_TIME_UPDATE_ONEDRIVE_CALLED, newValue);
		}

		private static void SetOneDriveUpdateRanNow2()
		{
			string now = DateTime.Now.AddMilliseconds(1.0).ToUniversalTime().ToString(CultureInfo.InvariantCulture);
			RegistryAccess.TrySetValue(ConstantData.REG_MBC_LAST_TIME_UPDATE_ONEDRIVE_RAN, now);
		}

		private static void SetOneDrivePullRanNow2()
		{
			string now = DateTime.Now.AddMilliseconds(1.0).ToUniversalTime().ToString(CultureInfo.InvariantCulture);
			RegistryAccess.TrySetValue(ConstantData.REG_MBC_LAST_TIME_PULL_ONEDRIVE_RAN, now);
		}

		private static DateTime GetWhenOneDrivePullRan2()
		{
			DateTime when = default(DateTime);
			DateTime.TryParse(RegistryAccess.GetValue(ConstantData.REG_MBC_LAST_TIME_PULL_ONEDRIVE_RAN), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out when);
			return when;
		}

		private readonly Briefcase _briefcase = null;
		private readonly RuntimeData _runtimeData = null;
		#endregion properties


		#region lifecycle
		internal MetaBriefcaseOneDriveReaderWriter(Briefcase briefcase, RuntimeData runtimeData)
		{
			_briefcase = briefcase;
			_runtimeData = runtimeData;
		}
		protected override Task OpenMayOverrideAsync(object args = null)
		{
			_runtimeData.PropertyChanged += OnRuntimeData_PropertyChanged;
			return Task.CompletedTask;
		}
		protected override Task CloseMayOverrideAsync()
		{
			_runtimeData.PropertyChanged -= OnRuntimeData_PropertyChanged;
			return Task.CompletedTask;
		}
		#endregion lifecycle


		#region event handlers
		private void OnRuntimeData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(RuntimeData.IsConnectionAvailable) && _runtimeData.IsConnectionAvailable)
			{
				Task upd = Task.Run(() =>
				{
					RunUnderSemaphore(() =>
					{
						if (GetIsOneDriveUpdateOverdue2()) RaiseUpdateOneDriveMetaBriefcaseRequested2();
					});
				});
			}
		}
		#endregion event handlers


		#region services
		internal async Task RunUnderSemaphore(Func<Task> func)
		{
			try
			{
				_oneDriveSemaphore.WaitOne();
				await func().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
			finally
			{
				SemaphoreExtensions.TryRelease(_oneDriveSemaphore);
			}
		}
		private void RunUnderSemaphore(Action action)
		{
			try
			{
				_oneDriveSemaphore.WaitOne();
				action();
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
			finally
			{
				SemaphoreExtensions.TryRelease(_oneDriveSemaphore);
			}
		}
		internal Task LogonAsync(bool wantToUseOneDrive)
		{
			return RunUnderSemaphore(async () =>
			{
				// LOLLO NOTE in the dashboard, set settings - API settings - Mobile or desktop client app = true
				OneDriveAccessToken2 = string.Empty;
				if (wantToUseOneDrive && _runtimeData.IsConnectionAvailable)
				{
					var oneDriveClient = OneDriveClientExtensions.GetUniversalClient(_oneDriveScopes);
					AccountSession oneDriveAccountSession = null;
					Task<AccountSession> authenticateT = null;
					await RunInUiThreadAsync(() =>
					{
						authenticateT = oneDriveClient.AuthenticateAsync(); // this needs the UI thread
					}).ConfigureAwait(false);

					// LOLLO no timeout like in the following: if the user must sign in and it takes time, this timeout will be too short and the one drive data won't be read.
					//Func<Task> waitMax = async () => await Task.Delay(3000);
					//Func<Task> authenticateF = async () => oneDriveAccountSession = await authenticateT;
					// if the connection is very slow, we time out. The old token may still work, if present; otherwise, we stick to the local file.
					//await Task.WhenAny(waitMax(), authenticateF()).ConfigureAwait(false);
					oneDriveAccountSession = await authenticateT.ConfigureAwait(false);

					OneDriveAccessToken2 = oneDriveAccountSession?.AccessToken ?? string.Empty;
					// var appRoot = await oneDriveClient.Drive.Special.AppRoot.Request().GetAsync().ConfigureAwait(false);
					//}
				}
			});
		}
		internal async Task<Tuple<MetaBriefcase, Exception>> TryReadMetaBriefcase2Async()
		{
			MetaBriefcase mbc = null;
			var serializer = new DataContractSerializer(typeof(MetaBriefcase));

			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OneDriveAccessToken2);

				try
				{
					using (var odFileContent = await client.GetStreamAsync(new Uri(_oneDriveAppRootUri4Path + MetaBriefcase.FILENAME + ":/content")).ConfigureAwait(false))
					{
						mbc = (MetaBriefcase)serializer.ReadObject(odFileContent);
					}
					SetOneDrivePullRanNow2();
				}
				catch (SerializationException ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
					SetIsOneDriveUpdateOverdue2();
					return Tuple.Create<MetaBriefcase, Exception>(null, ex);
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
					return Tuple.Create<MetaBriefcase, Exception>(null, ex);
				}
			}

			return Tuple.Create<MetaBriefcase, Exception>(mbc, null);
		}

		internal Task SaveIntoOneDriveAsync(CancellationToken cancToken, Guid taskInstanceId)
		{
			if (!_runtimeData.IsConnectionAvailable || cancToken.IsCancellationRequested) return Task.CompletedTask;

			SetAllowedBackgroundTaskInstance(taskInstanceId);
			Debug.WriteLine("last taskInstanceId = " + taskInstanceId.ToString());

			return RunUnderSemaphore(async () =>
			{
				var myInstanceId = taskInstanceId;
				if (!GetIsCurrentBackgroundTaskInstanceAllowed(myInstanceId)) return;
				await SaveIntoOneDrive2Async(cancToken, myInstanceId).ConfigureAwait(false);
			});
		}

		private async Task SaveIntoOneDrive2Async(CancellationToken cancToken, Guid taskInstanceId)
		{// LOLLO TODO test this with the merging (it's new)
			string localFileContentString = await _briefcase.MetaBriefcase.GetLocalFileContentString().ConfigureAwait(false);

			//await Task.Delay(15000).ConfigureAwait(false);
			if (cancToken.IsCancellationRequested || !GetIsCurrentBackgroundTaskInstanceAllowed(taskInstanceId)) return;

			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OneDriveAccessToken2);

				DateTime oneDriveFileLastModifiedWhen = default(DateTime);
				string remoteFilecontentString = string.Empty;
				try
				{
					var odFileProps = JObject.Parse(await client.GetStringAsync(new Uri(_oneDriveAppRootUri4Path + MetaBriefcase.FILENAME)).ConfigureAwait(false));
					oneDriveFileLastModifiedWhen = odFileProps.GetValue("lastModifiedDateTime").ToObject<DateTime>().ToUniversalTime();
					remoteFilecontentString = await client.GetStringAsync(new Uri(_oneDriveAppRootUri4Path + MetaBriefcase.FILENAME + ":/content")).ConfigureAwait(false);
				}
				catch { }

				if (localFileContentString.Trim().Equals(remoteFilecontentString.Trim(), StringComparison.Ordinal))
				{
					SetOneDriveUpdateRanNow2();
					return;
				}
				if (cancToken.IsCancellationRequested || !GetIsCurrentBackgroundTaskInstanceAllowed(taskInstanceId)) return;

				HttpResponseMessage response = null;
				if (!MetaBriefcase.GetIsLocalSyncedOnceSinceLastOpen() || GetWhenOneDrivePullRan2() < oneDriveFileLastModifiedWhen)
					response = await MergeIntoOneDriveAsync(cancToken, taskInstanceId, client, localFileContentString, remoteFilecontentString).ConfigureAwait(false);
				else
					response = await OverwriteOneDriveAsync(cancToken, taskInstanceId, client, localFileContentString).ConfigureAwait(false);

				if (response?.IsSuccessStatusCode == true) SetOneDriveUpdateRanNow2();
			}
		}

		private static async Task<HttpResponseMessage> OverwriteOneDriveAsync(CancellationToken cancToken, Guid taskInstanceId, HttpClient client, /*Stream localFileContent*/ string localFileContent)
		{
			HttpResponseMessage response = null;
			using (var content = new StringContent(localFileContent))
			{
				response = await client.PutAsync(new Uri(_oneDriveAppRootUri4Path + MetaBriefcase.FILENAME + ":/content"), content, cancToken).ConfigureAwait(false);
			}
			return response;
		}

		private static async Task<HttpResponseMessage> MergeIntoOneDriveAsync(CancellationToken cancToken, Guid taskInstanceId, HttpClient client, string localFileContent, string remoteFilecontent)
		{
			MetaBriefcase remoteMbc = null;
			MetaBriefcase localMbc = null;
			var serializer = new DataContractSerializer(typeof(MetaBriefcase));
			using (var ms = new MemoryStream())
			{
				using (var sw = new StreamWriter(ms))
				{
					sw.Write(remoteFilecontent);
					await sw.FlushAsync().ConfigureAwait(false);
					ms.Position = 0;
					remoteMbc = (MetaBriefcase)serializer.ReadObject(ms);
				}
			}
			if (cancToken.IsCancellationRequested || !GetIsCurrentBackgroundTaskInstanceAllowed(taskInstanceId)) return null;
			using (var ms = new MemoryStream())
			{
				using (var sw = new StreamWriter(ms))
				{
					sw.Write(localFileContent);
					await sw.FlushAsync().ConfigureAwait(false);
					ms.Position = 0;
					localMbc = (MetaBriefcase)serializer.ReadObject(ms);
				}
			}
			if (cancToken.IsCancellationRequested || !GetIsCurrentBackgroundTaskInstanceAllowed(taskInstanceId)) return null;

			var mergedMbc = MetaBriefcase.Merge(localMbc, remoteMbc);
			if (cancToken.IsCancellationRequested || !GetIsCurrentBackgroundTaskInstanceAllowed(taskInstanceId)) return null;

			HttpResponseMessage response = null;
			using (var ms = new MemoryStream())
			{
				serializer.WriteObject(ms, mergedMbc);
				ms.Position = 0;
				using (var content = new StreamContent(ms))
				{
					response = await client.PutAsync(new Uri(_oneDriveAppRootUri4Path + MetaBriefcase.FILENAME + ":/content"), content, cancToken).ConfigureAwait(false);
				}
			}
			return response;
		}
		#endregion services
	}

	[DataContract]
	public class MetaBriefcaseRubbishBin : OpenableObservableData // LOLLO TODO test this
	{
		#region events
		public static event EventHandler DataChanged;
		#endregion events


		#region properties
		internal const string FILENAME = "LolloSessionDataMetaBriefcaseRubbishBin.xml";
		private readonly MetaBriefcase _mbc = null;

		private List<Category> _deletedCategories = new List<Category>();
		[DataMember]
		private List<Category> DeletedCategories { get { return _deletedCategories; } set { _deletedCategories = value; } } // the setter is only for the serialiser

		private List<Tuple<List<string>, FieldDescription>> _deletedFieldDescriptions = new List<Tuple<List<string>, FieldDescription>>();
		[DataMember]
		private List<Tuple<List<string>, FieldDescription>> DeletedFieldDescriptions { get { return _deletedFieldDescriptions; } set { _deletedFieldDescriptions = value; } } // the setter is only for the serialiser

		private List<Tuple<FieldDescription, FieldValue>> _deletedFieldValues = new List<Tuple<FieldDescription, FieldValue>>();
		[DataMember]
		private List<Tuple<FieldDescription, FieldValue>> DeletedFieldValues { get { return _deletedFieldValues; } set { _deletedFieldValues = value; } } // the setter is only for the serialiser

		internal Task AddCategoryAsync(Category category)
		{
			return RunFunctionIfOpenAsyncA(() =>
			{
				var availableCat = DeletedCategories.FirstOrDefault(cat => cat.Name == category.Name);
				if (availableCat != null) DeletedCategories.Remove(availableCat);
				DeletedCategories.Add(category);
			});
		}
		internal async Task<Category> GetCategoryAsync(string name)
		{
			Category result = null;
			await RunFunctionIfOpenAsyncA(() =>
			{
				result = DeletedCategories.FirstOrDefault(cat => cat.Name == name);
			}).ConfigureAwait(false);
			return result;
		}
		internal Task AddFieldDescriptionAsync(List<string> catsWithFldDsc, FieldDescription fieldDescription)
		{
			return RunFunctionIfOpenAsyncA(() =>
			{
				var availableFldDsc = DeletedFieldDescriptions.FirstOrDefault(fd => fd.Item2.Caption == fieldDescription.Caption);
				if (availableFldDsc != null) DeletedFieldDescriptions.Remove(availableFldDsc);
				DeletedFieldDescriptions.Add(Tuple.Create(catsWithFldDsc, fieldDescription));
			});
		}
		internal async Task<Tuple<List<string>, FieldDescription>> GetFieldDescriptionAsync(string caption)
		{
			Tuple<List<string>, FieldDescription> result = null;
			await RunFunctionIfOpenAsyncA(() =>
			{
				result = DeletedFieldDescriptions.FirstOrDefault(fd => fd.Item2.Caption == caption);
			}).ConfigureAwait(false);
			return result;
		}
		internal Task AddPossibleValueAsync(FieldDescription fieldDescription, FieldValue fieldValue)
		{
			return RunFunctionIfOpenAsyncA(() =>
			{
				var availablePv = DeletedFieldValues.FirstOrDefault(pv => pv.Item1.Id == fieldDescription.Id && pv.Item2.Vaalue == fieldValue.Vaalue);
				if (availablePv != null) DeletedFieldValues.Remove(availablePv);
				DeletedFieldValues.Add(Tuple.Create(fieldDescription, fieldValue));
			});
		}
		internal async Task<Tuple<FieldDescription, FieldValue>> GetPossibleValueAsync(FieldDescription fieldDescription, string vaalue)
		{
			Tuple<FieldDescription, FieldValue> result = null;
			await RunFunctionIfOpenAsyncA(() =>
			{
				result = DeletedFieldValues.FirstOrDefault(pv => pv.Item1.Id == fieldDescription.Id && pv.Item2.Vaalue == vaalue);
			}).ConfigureAwait(false);
			return result;
		}
		internal Task ClearAsync() // LOLLO TODO call this at some point, but when?
		{
			return RunFunctionIfOpenAsyncA(() =>
			{
				_deletedCategories.Clear();
				_deletedFieldDescriptions.Clear();
				_deletedFieldValues.Clear();
			});
		}
		#endregion properties


		#region lifecycle
		internal MetaBriefcaseRubbishBin(MetaBriefcase metaBriefcase)
		{
			_mbc = metaBriefcase;
		}

		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			await LoadAsync().ConfigureAwait(false);
			Category.NameChanged += OnCategory_NameChanged;
			FieldDescription.CaptionChanged += OnFldDsc_CaptionChanged;
			FieldValue.VaalueChanged += OnFieldValue_VaalueChanged;
		}

		protected override async Task CloseMayOverrideAsync()
		{
			Category.NameChanged -= OnCategory_NameChanged;
			FieldDescription.CaptionChanged -= OnFldDsc_CaptionChanged;
			FieldValue.VaalueChanged -= OnFieldValue_VaalueChanged;

			await SaveAsync().ConfigureAwait(false);
		}
		private async Task LoadAsync()
		{
			var file = await MetaBriefcase.GetDirectory()
				.CreateFileAsync(FILENAME, CreationCollisionOption.OpenIfExists)
				.AsTask().ConfigureAwait(false);

			MetaBriefcaseRubbishBin newInstance = null;
			var serializer = new DataContractSerializer(typeof(MetaBriefcaseRubbishBin));
			try
			{
				using (var localFileContent = await file.OpenStreamForReadAsync().ConfigureAwait(false))
				{
					newInstance = (MetaBriefcaseRubbishBin)(serializer.ReadObject(localFileContent));
				}
			}
			catch (Exception ex1) { Logger.Add_TPL(ex1.ToString(), Logger.FileErrorLogFilename); }
			if (newInstance != null) CopyXMLPropertiesFrom(newInstance);
		}
		private bool CopyXMLPropertiesFrom(MetaBriefcaseRubbishBin source)
		{// LOLLO TODO check if this is good enough or I need deep copies
			if (source == null) return false;

			if (source._deletedCategories != null) _deletedCategories = source._deletedCategories;
			if (source._deletedFieldDescriptions != null) _deletedFieldDescriptions = source._deletedFieldDescriptions;
			if (source._deletedFieldValues != null) _deletedFieldValues = source._deletedFieldValues;

			return true;
		}
		private async Task<bool> SaveAsync()
		{
			try
			{
				using (var memoryStream = new MemoryStream())
				{
					var serializer = new DataContractSerializer(typeof(MetaBriefcaseRubbishBin));
					serializer.WriteObject(memoryStream, this);

					string currentInstance = string.Empty;
					memoryStream.Seek(0, SeekOrigin.Begin);
					using (StreamReader streamReader = new StreamReader(memoryStream))
					{
						currentInstance = streamReader.ReadToEnd();

						var file = await MetaBriefcase.GetDirectory()
							.CreateFileAsync(FILENAME, CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);

						using (var fileStream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
						{
							fileStream.SetLength(0); // avoid leaving crap at the end if overwriting a file that was longer
							memoryStream.Seek(0, SeekOrigin.Begin);
							await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
							await memoryStream.FlushAsync().ConfigureAwait(false);
							await fileStream.FlushAsync().ConfigureAwait(false);
						}
					}
				}
				return true;
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}

			return false;
		}
		#endregion lifecycle


		#region event handlers
		private void OnCategory_NameChanged(object sender, EventArgs e)
		{
			Task tryRecycle = RunFunctionIfOpenAsyncT(async () =>
			{
				var cat = sender as Category;
				if (cat == null) return;

				var recycledCat = await GetCategoryAsync(cat.Name); //.ConfigureAwait(false);
				if (recycledCat != null)
				{
					Task awaitThis = Task.CompletedTask;
					await RunInUiThreadAsync(() =>
					{
						bool setCurrent = _mbc.CurrentCategoryId == cat.Id;
						_deletedCategories.Remove(cat);
						cat = recycledCat;
						_deletedCategories.Add(cat);

						foreach (var missingFldDsc in cat.FieldDescriptions.Where(fd0 => _mbc.FieldDescriptions.All(fd1 => fd1.Id != fd0.Id)))
						{
							cat.RemoveFieldDescription(missingFldDsc);
						}
						if (setCurrent) awaitThis = _mbc.SetCurrentCategoryAsync(cat);
					}).ConfigureAwait(false);
					await awaitThis;

					DataChanged?.Invoke(this, EventArgs.Empty);
				}
			});
		}

		private void OnFldDsc_CaptionChanged(object sender, EventArgs e)
		{
			Task tryRecycle = RunFunctionIfOpenAsyncT(async () =>
			{
				var fldDsc = sender as FieldDescription; var cats = _deletedCategories;
				if (fldDsc == null || cats == null) return;

				var recycledFldDsc = await GetFieldDescriptionAsync(fldDsc.Caption); //.ConfigureAwait(false);
				if (recycledFldDsc != null)
				{
					Task awaitThis = Task.CompletedTask;
					await RunInUiThreadAsync(() =>
					{
						bool setCurrent = _mbc.CurrentFieldDescriptionId == fldDsc.Id;
						foreach (var cat in _deletedCategories)
						{
							cat.RemoveFieldDescription(fldDsc);
						}

						_mbc.FieldDescriptions.Remove(fldDsc);
						fldDsc = recycledFldDsc.Item2;
						_mbc.FieldDescriptions.Add(fldDsc);

						foreach (var catId in recycledFldDsc.Item1)
						{
							var registeredCat = _deletedCategories.FirstOrDefault(cat => cat.Id == catId);
							registeredCat?.AddFieldDescription(fldDsc);
						}
						if (setCurrent) awaitThis = _mbc.SetCurrentFieldDescriptionAsync(fldDsc);
					}).ConfigureAwait(false);
					await awaitThis;

					DataChanged?.Invoke(this, EventArgs.Empty);
				}
			});
		}

		private void OnFieldValue_VaalueChanged(object sender, EventArgs e)
		{
			Task tryRecycle = RunFunctionIfOpenAsyncT(async () =>
			{
				var fldVal = sender as FieldValue; var cfd = _mbc.CurrentFieldDescription;
				if (fldVal == null || cfd == null) return;

				var recycledFldVal = await GetPossibleValueAsync(cfd, fldVal.Vaalue); //.ConfigureAwait(false);
				if (recycledFldVal != null)
				{
					await RunInUiThreadAsync(() =>
					{
						cfd.RemovePossibleValue(fldVal);
						fldVal = recycledFldVal.Item2;
						cfd.AddPossibleValue(fldVal);
					}).ConfigureAwait(false);

					DataChanged?.Invoke(this, EventArgs.Empty);
				}
			});
		}
		#endregion event handlers
	}
}
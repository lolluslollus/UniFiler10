﻿using Microsoft.OneDrive.Sdk;
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
		#region events
		//public static event EventHandler UpdateOneDriveMetaBriefcaseRequested;
		//private void RaiseUpdateOneDriveMetaBriefcaseRequested()
		//{
		//	if (_briefcase?.IsWantToUseOneDrive == true)
		//	{
		//		Task raise = Task.Run(() =>
		//		{
		//			try
		//			{
		//				_oneDriveSemaphore.WaitOne();
		//				SetOneDriveUpdateCalledNow();
		//			}
		//			catch (Exception ex)
		//			{
		//				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
		//			}
		//			finally
		//			{
		//				SemaphoreExtensions.TryRelease(_oneDriveSemaphore);
		//				UpdateOneDriveMetaBriefcaseRequested?.Invoke(this, EventArgs.Empty);
		//			}
		//		});
		//	}
		//}
		public event EventHandler DataChanged;
		#endregion events


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
		private readonly OneDriveReaderWriter _oneDriveReaderWriter = null;

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

		//[IgnoreDataMember]
		//private static bool IsLoadFromOneDrive
		//{
		//	get
		//	{
		//		string regVal = RegistryAccess.GetValue(ConstantData.REG_MBC_IS_LOAD_FROM_ONE_DRIVE).ToLower();
		//		if (string.IsNullOrWhiteSpace(regVal)) return Briefcase.DefaultIsWantToUseOneDrive;
		//		return regVal.Equals(true.ToString().ToLower());
		//	}
		//	set
		//	{
		//		bool newValue = value.ToString().ToLower().Equals(true.ToString().ToLower());
		//		RegistryAccess.TrySetValue(ConstantData.REG_MBC_IS_LOAD_FROM_ONE_DRIVE, newValue.ToString().ToLower());
		//	}
		//}		

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
		private readonly RubbishBin _rubbishBin = null;
		[DataContract]
		private class RubbishBin : OpenableObservableData // LOLLO TODO test this
		{
			public const string FILENAME = "LolloSessionDataMetaBriefcaseRubbishBin.xml";

			private List<Category> _categories = new List<Category>();
			[DataMember]
			private List<Category> Categories { get { return _categories; } set { _categories = value; } } // the setter is only for the serialiser

			private List<Tuple<List<string>, FieldDescription>> _fieldDescriptions = new List<Tuple<List<string>, FieldDescription>>();
			[DataMember]
			private List<Tuple<List<string>, FieldDescription>> FieldDescriptions { get { return _fieldDescriptions; } set { _fieldDescriptions = value; } } // the setter is only for the serialiser

			private List<Tuple<FieldDescription, FieldValue>> _fieldValues = new List<Tuple<FieldDescription, FieldValue>>();
			[DataMember]
			private List<Tuple<FieldDescription, FieldValue>> FieldValues { get { return _fieldValues; } set { _fieldValues = value; } } // the setter is only for the serialiser

			public Task AddCategoryAsync(Category category)
			{
				return RunFunctionIfOpenAsyncA(() =>
				{
					var availableCat = Categories.FirstOrDefault(cat => cat.Name == category.Name);
					if (availableCat != null) Categories.Remove(availableCat);
					Categories.Add(category);
				});
			}
			public async Task<Category> GetCategoryAsync(string name)
			{
				Category result = null;
				await RunFunctionIfOpenAsyncA(() =>
				{
					result = Categories.FirstOrDefault(cat => cat.Name == name);
				}).ConfigureAwait(false);
				return result;
			}
			public Task AddFieldDescriptionAsync(List<string> catsWithFldDsc, FieldDescription fieldDescription)
			{
				return RunFunctionIfOpenAsyncA(() =>
				{
					var availableFldDsc = FieldDescriptions.FirstOrDefault(fd => fd.Item2.Caption == fieldDescription.Caption);
					if (availableFldDsc != null) FieldDescriptions.Remove(availableFldDsc);
					FieldDescriptions.Add(Tuple.Create(catsWithFldDsc, fieldDescription));
				});
			}
			public async Task<Tuple<List<string>, FieldDescription>> GetFieldDescriptionAsync(string caption)
			{
				Tuple<List<string>, FieldDescription> result = null;
				await RunFunctionIfOpenAsyncA(() =>
				{
					result = FieldDescriptions.FirstOrDefault(fd => fd.Item2.Caption == caption);
				}).ConfigureAwait(false);
				return result;
			}
			public Task AddPossibleValueAsync(FieldDescription fieldDescription, FieldValue fieldValue)
			{
				return RunFunctionIfOpenAsyncA(() =>
				{
					var availablePv = FieldValues.FirstOrDefault(pv => pv.Item1.Id == fieldDescription.Id && pv.Item2.Vaalue == fieldValue.Vaalue);
					if (availablePv != null) FieldValues.Remove(availablePv);
					FieldValues.Add(Tuple.Create(fieldDescription, fieldValue));
				});
			}
			public async Task<Tuple<FieldDescription, FieldValue>> GetPossibleValueAsync(FieldDescription fieldDescription, string vaalue)
			{
				Tuple<FieldDescription, FieldValue> result = null;
				await RunFunctionIfOpenAsyncA(() =>
				{
					result = FieldValues.FirstOrDefault(pv => pv.Item1.Id == fieldDescription.Id && pv.Item2.Vaalue == vaalue);
				}).ConfigureAwait(false);
				return result;
			}
			public Task ClearAsync() // LOLLO TODO call this at some point, but when?
			{
				return RunFunctionIfOpenAsyncA(() =>
				{
					_categories.Clear();
					_fieldDescriptions.Clear();
					_fieldValues.Clear();
				});
			}

			#region lifecycle
			protected override Task OpenMayOverrideAsync(object args = null)
			{
				return LoadAsync();
			}

			protected override Task CloseMayOverrideAsync()
			{
				return SaveAsync();
			}
			private async Task LoadAsync()
			{
				var file = await GetDirectory()
					.CreateFileAsync(FILENAME, CreationCollisionOption.OpenIfExists)
					.AsTask().ConfigureAwait(false);

				RubbishBin newInstance = null;
				var serializer = new DataContractSerializer(typeof(RubbishBin));
				try
				{
					using (var localFileContent = await file.OpenStreamForReadAsync().ConfigureAwait(false))
					{
						newInstance = (RubbishBin)(serializer.ReadObject(localFileContent));
					}
				}
				catch (Exception ex1) { Logger.Add_TPL(ex1.ToString(), Logger.FileErrorLogFilename); }
				if (newInstance != null) CopyXMLPropertiesFrom(newInstance);
			}
			private bool CopyXMLPropertiesFrom(RubbishBin source)
			{// LOLLO TODO check if this is good enough or I need deep copies
				if (source == null) return false;

				if (source._categories != null) _categories = source._categories;
				if (source._fieldDescriptions != null) _fieldDescriptions = source._fieldDescriptions;
				if (source._fieldValues != null) _fieldValues = source._fieldValues;

				return true;
			}
			private async Task<bool> SaveAsync()
			{
				try
				{
					using (var memoryStream = new MemoryStream())
					{
						var serializer = new DataContractSerializer(typeof(RubbishBin));
						serializer.WriteObject(memoryStream, this);

						string currentInstance = string.Empty;
						memoryStream.Seek(0, SeekOrigin.Begin);
						using (StreamReader streamReader = new StreamReader(memoryStream))
						{
							currentInstance = streamReader.ReadToEnd();

							var file = await GetDirectory()
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
		}
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

			_oneDriveReaderWriter = new OneDriveReaderWriter(_briefcase, _runtimeData);
			_rubbishBin = new RubbishBin();
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
			Category.NameChanged += OnCategory_NameChanged;
			FieldDescription.CaptionChanged += OnFldDsc_CaptionChanged;
			FieldValue.VaalueChanged += OnFieldValue_VaalueChanged;

			await _rubbishBin.OpenAsync().ConfigureAwait(false);
		}

		protected override async Task CloseMayOverrideAsync()
		{
			Category.NameChanged -= OnCategory_NameChanged;
			FieldDescription.CaptionChanged -= OnFldDsc_CaptionChanged;
			FieldValue.VaalueChanged -= OnFieldValue_VaalueChanged;

			await _rubbishBin.CloseAsync().ConfigureAwait(false);
			await _oneDriveReaderWriter.CloseAsync().ConfigureAwait(false);
			//await Save2Async().ConfigureAwait(false); // LOLLO TODO see if I need this, I am trying to do fewer saves
			// RaiseUpdateOneDriveMetaBriefcaseRequested();
		}
		#endregion lifecycle


		#region loading methods
		public const string FILENAME = "LolloSessionDataMetaBriefcase.xml";

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
					if (OneDriveReaderWriter.GetIsOneDriveUpdateOverdue())
					{// LOLLO TODO load form one drive and merge remote with local: check it
						var localMetaBriefcase = await LoadFromFile(localFile, serializer).ConfigureAwait(false);
						var remoteMetaBriefcase = (await _oneDriveReaderWriter.TryReadMetaBriefcase().ConfigureAwait(false)).Item1;

						newMetaBriefcase = OneDriveReaderWriter.Merge(localMetaBriefcase, remoteMetaBriefcase);
						mustSaveLocal = true;
						mustSyncOneDrive = true;
						openParams.IsLocalSyncedOnceSinceLastOpen = true;
					}
					else
					{
						var mbcAndException = await _oneDriveReaderWriter.TryReadMetaBriefcase().ConfigureAwait(false);
						if (mbcAndException.Item1 != null)
						{
							mustSaveLocal = true;
							openParams.IsLocalSyncedOnceSinceLastOpen = true;
						}
						else if (mbcAndException.Item2 is SerializationException)
						{
							mustSyncOneDrive = true;
							openParams.IsLocalSyncedOnceSinceLastOpen = true;
						}
						else
						{
							newMetaBriefcase = await LoadFromFile(localFile, serializer).ConfigureAwait(false);
							openParams.IsLocalSyncedOnceSinceLastOpen = false;
						}
					}
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
		/// <summary>
		/// This semaphore protects the one drive data, operating system-wide
		/// </summary>
		private static readonly Semaphore _oneDriveSemaphore = new Semaphore(1, 1, "Unifiler10_MetaBriefcase_OneDriveSemaphore");

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

		//public Task SetIsElevatedAsync(bool newValue)
		//{
		//	return RunFunctionIfOpenAsyncA(delegate { IsElevated = newValue; });
		//}

		public Task<bool> AddNewCategoryAsync()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				string name = RuntimeData.GetText("NewCategory");
				var newCat = new Category(name, true, true);

				if (Category.Check(newCat) && !Categories.Any(cat => cat.Name == newCat.Name || cat.Id == newCat.Id))
				{
					await RunInUiThreadAsync(() => _categories.Add(newCat)).ConfigureAwait(false);

					//newCat.NameChanged -= OnCategory_NameChanged;
					//newCat.NameChanged += OnCategory_NameChanged;
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
					//cat.NameChanged -= OnCategory_NameChanged;
					await RunInUiThreadAsync(() => isRemoved = _categories.Remove(cat)).ConfigureAwait(false);
					//if (isRemoved) cat?.Dispose();
					await _rubbishBin.AddCategoryAsync(cat);
					if (CurrentCategoryId == cat.Id && _categories.Any()) { CurrentCategoryId = _categories[0]?.Id; }
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

					//fldDsc.CaptionChanged -= OnFldDsc_CaptionChanged;
					//fldDsc.CaptionChanged += OnFldDsc_CaptionChanged;
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

					//fldDsc.CaptionChanged -= OnFldDsc_CaptionChanged;
					//foreach (var pv in fldDsc.PossibleValues)
					//{
					//	pv.VaalueChanged -= OnFieldValue_VaalueChanged;
					//}

					var catsWithFldDsc = new List<string>();
					await RunInUiThreadAsync(delegate
					{
						foreach (var cat in _categories)
						{
							if (cat.RemoveFieldDescription(fldDsc)) catsWithFldDsc.Add(cat.Id);
						}
						isRemoved = _fieldDescriptions.Remove(fldDsc);
					}).ConfigureAwait(false);
					//if (isRemoved) fldDesc?.Dispose();
					await _rubbishBin.AddFieldDescriptionAsync(catsWithFldDsc, fldDsc);
					if (CurrentFieldDescriptionId == fldDsc.Id) if (_fieldDescriptions.Any()) { CurrentFieldDescriptionId = _fieldDescriptions[0]?.Id; } else CurrentFieldDescriptionId = null;
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
				//if (isAdded)
				//{
				//	newFldVal.VaalueChanged -= OnFieldValue_VaalueChanged;
				//	newFldVal.VaalueChanged += OnFieldValue_VaalueChanged;
				//}
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
				//if (isAdded)
				//{
				//	newFldVal.VaalueChanged -= OnFieldValue_VaalueChanged;
				//	newFldVal.VaalueChanged += OnFieldValue_VaalueChanged;
				//}
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

				//fldVal.VaalueChanged -= OnFieldValue_VaalueChanged;

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
		#endregion data methods


		#region one drive writer methods
		public class OneDriveReaderWriter : OpenableObservableData
		{
			#region events
			public static event EventHandler UpdateOneDriveMetaBriefcaseRequested;
			internal void RaiseUpdateOneDriveMetaBriefcaseRequested()
			{
				if (_briefcase?.IsWantToUseOneDrive == true)
				{
					Task raise = Task.Run(() =>
					{
						try
						{
							_oneDriveSemaphore.WaitOne();
							SetOneDriveUpdateCalledNow();
						}
						catch (Exception ex)
						{
							Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
						}
						finally
						{
							SemaphoreExtensions.TryRelease(_oneDriveSemaphore);
							UpdateOneDriveMetaBriefcaseRequested?.Invoke(this, EventArgs.Empty);
						}
					});
				}
			}
			#endregion events


			#region properties
			private static readonly string[] _oneDriveScopes = { "onedrive.readwrite", "onedrive.appfolder", "wl.signin", "wl.offline_access", "wl.skydrive", "wl.skydrive_update" };
			//private const string _oneDriveAppRootUri = "https://api.onedrive.com/v1.0/drive/special/approot/";
			private const string _oneDriveAppRootUri4Path = "https://api.onedrive.com/v1.0/drive/special/approot:/"; // this is useful if you don't know the file ids but you know the paths

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

			private static string OneDriveAccessToken
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

			internal static bool GetIsOneDriveUpdateOverdue()
			{
				DateTime lastTimeUpdateOneDriveCalled = default(DateTime);
				DateTime.TryParse(RegistryAccess.GetValue(ConstantData.REG_MBC_LAST_TIME_UPDATE_ONEDRIVE_CALLED), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out lastTimeUpdateOneDriveCalled);

				DateTime lastTimeUpdateOneDriveRan = default(DateTime);
				DateTime.TryParse(RegistryAccess.GetValue(ConstantData.REG_MBC_LAST_TIME_UPDATE_ONEDRIVE_RAN), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out lastTimeUpdateOneDriveRan);

				return lastTimeUpdateOneDriveCalled >= lastTimeUpdateOneDriveRan && lastTimeUpdateOneDriveCalled > default(DateTime);
			}

			private static void SetIsOneDriveUpdateOverdue()
			{
				string newValue = default(DateTime).ToUniversalTime().ToString(CultureInfo.InvariantCulture);
				RegistryAccess.TrySetValue(ConstantData.REG_MBC_LAST_TIME_UPDATE_ONEDRIVE_RAN, newValue);
			}

			private static void SetOneDriveUpdateCalledNow()
			{
				string newValue = DateTime.Now.ToUniversalTime().ToString(CultureInfo.InvariantCulture);
				RegistryAccess.TrySetValue(ConstantData.REG_MBC_LAST_TIME_UPDATE_ONEDRIVE_CALLED, newValue);
			}

			private static void SetOneDriveUpdateRanNow()
			{
				string now = DateTime.Now.AddMilliseconds(1.0).ToUniversalTime().ToString(CultureInfo.InvariantCulture);
				RegistryAccess.TrySetValue(ConstantData.REG_MBC_LAST_TIME_UPDATE_ONEDRIVE_RAN, now);

				//IsLoadFromOneDrive = true;
			}

			private static void SetOneDrivePullRanNow()
			{
				string now = DateTime.Now.AddMilliseconds(1.0).ToUniversalTime().ToString(CultureInfo.InvariantCulture);
				RegistryAccess.TrySetValue(ConstantData.REG_MBC_LAST_TIME_PULL_ONEDRIVE_RAN, now);
			}

			private static DateTime GetWhenOneDrivePullRan()
			{
				DateTime when = default(DateTime);
				DateTime.TryParse(RegistryAccess.GetValue(ConstantData.REG_MBC_LAST_TIME_PULL_ONEDRIVE_RAN), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out when);
				return when;
			}

			private readonly Briefcase _briefcase = null;
			private readonly RuntimeData _runtimeData = null;
			#endregion properties


			#region lifecycle
			internal OneDriveReaderWriter(Briefcase briefcase, RuntimeData runtimeData)
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
						try
						{
							_oneDriveSemaphore.WaitOne();
							if (GetIsOneDriveUpdateOverdue()) RaiseUpdateOneDriveMetaBriefcaseRequested();
						}
						catch (Exception ex)
						{
							Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
						}
						finally
						{
							SemaphoreExtensions.TryRelease(_oneDriveSemaphore);
						}
					});
				}
			}
			#endregion event handlers


			#region services
			internal async Task LogonAsync(bool wantToUseOneDrive)
			{
				try
				{
					_oneDriveSemaphore.WaitOne();
					// LOLLO NOTE in the dashboard, set settings - API settings - Mobile or desktop client app = true
					OneDriveAccessToken = string.Empty;
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

						OneDriveAccessToken = oneDriveAccountSession?.AccessToken ?? string.Empty;
						// var appRoot = await oneDriveClient.Drive.Special.AppRoot.Request().GetAsync().ConfigureAwait(false);
						//}
					}
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
			internal async Task<Tuple<MetaBriefcase, Exception>> TryReadMetaBriefcase()
			{
				MetaBriefcase mbc = null;
				var serializer = new DataContractSerializer(typeof(MetaBriefcase));
				using (var client = new HttpClient())
				{
					client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OneDriveAccessToken);

					try
					{
						using (var odFileContent = await client.GetStreamAsync(new Uri(_oneDriveAppRootUri4Path + FILENAME + ":/content")).ConfigureAwait(false))
						{
							mbc = (MetaBriefcase)serializer.ReadObject(odFileContent);
						}
						SetOneDrivePullRanNow();
					}
					catch (SerializationException ex)
					{
						Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
						SetIsOneDriveUpdateOverdue();
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
			internal async Task SaveIntoOneDriveAsync(CancellationToken cancToken, Guid taskInstanceId)
			{
				try
				{
					if (!_runtimeData.IsConnectionAvailable || cancToken.IsCancellationRequested) return;

					SetAllowedBackgroundTaskInstance(taskInstanceId);
					Debug.WriteLine("last taskInstanceId = " + taskInstanceId.ToString());
					_oneDriveSemaphore.WaitOne();
					var myInstanceId = taskInstanceId;
					if (!GetIsCurrentBackgroundTaskInstanceAllowed(myInstanceId)) return;
					await SaveIntoOneDrive2Async(cancToken, myInstanceId).ConfigureAwait(false);
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
			private static async Task SaveIntoOneDrive2Async(CancellationToken cancToken, Guid taskInstanceId)
			{// LOLLO TODO test this with the merging (it's new)
				string localFileContentString = string.Empty;
				try
				{
					_localDataSemaphore.WaitOne();
					var localFile = await GetDirectory().TryGetItemAsync(FILENAME).AsTask(cancToken).ConfigureAwait(false) as StorageFile;
					if (localFile == null) return;

					using (var localFileContent = await localFile.OpenStreamForReadAsync().ConfigureAwait(false))
					{
						using (var streamReader = new StreamReader(localFileContent))
						{
							localFileContentString = streamReader.ReadToEnd();
						}
					}
				}
				finally { SemaphoreExtensions.TryRelease(_localDataSemaphore); }

				//await Task.Delay(15000).ConfigureAwait(false);
				if (cancToken.IsCancellationRequested || !GetIsCurrentBackgroundTaskInstanceAllowed(taskInstanceId)) return;

				using (var client = new HttpClient())
				{
					client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OneDriveAccessToken);

					DateTime oneDriveFileLastModifiedWhen = default(DateTime);
					string remoteFilecontentString = string.Empty;
					try
					{
						var odFileProps = JObject.Parse(await client.GetStringAsync(new Uri(_oneDriveAppRootUri4Path + FILENAME)).ConfigureAwait(false));
						oneDriveFileLastModifiedWhen = odFileProps.GetValue("lastModifiedDateTime").ToObject<DateTime>().ToUniversalTime();
						remoteFilecontentString = await client.GetStringAsync(new Uri(_oneDriveAppRootUri4Path + FILENAME + ":/content")).ConfigureAwait(false);
					}
					catch { }

					if (localFileContentString.Trim().Equals(remoteFilecontentString.Trim(), StringComparison.Ordinal))
					{
						SetOneDriveUpdateRanNow();
						return;
					}
					if (cancToken.IsCancellationRequested || !GetIsCurrentBackgroundTaskInstanceAllowed(taskInstanceId)) return;

					if (!GetIsLocalSyncedOnceSinceLastOpen() || GetWhenOneDrivePullRan() < oneDriveFileLastModifiedWhen) await MergeIntoOneDriveAsync(cancToken, taskInstanceId, client, localFileContentString, remoteFilecontentString).ConfigureAwait(false);
					else await OverwriteOneDriveAsync(cancToken, taskInstanceId, client, localFileContentString).ConfigureAwait(false);
					//else await OverwriteOneDriveAsync(cancToken, taskInstanceId, client, localFileContent).ConfigureAwait(false);

					SetOneDriveUpdateRanNow();
				}
			}

			private static async Task OverwriteOneDriveAsync(CancellationToken cancToken, Guid taskInstanceId, HttpClient client, /*Stream localFileContent*/ string localFileContent)
			{
				//localFileContent.Position = 0;
				//using (var content = new StreamContent(localFileContent))
				using (var content = new StringContent(localFileContent))
				{
					await client.PutAsync(new Uri(_oneDriveAppRootUri4Path + FILENAME + ":/content"), content, cancToken).ConfigureAwait(false);
				}
			}

			private static async Task MergeIntoOneDriveAsync(CancellationToken cancToken, Guid taskInstanceId, HttpClient client, string localFileContent, string remoteFilecontent)
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
				if (cancToken.IsCancellationRequested || !GetIsCurrentBackgroundTaskInstanceAllowed(taskInstanceId)) return;
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
				if (cancToken.IsCancellationRequested || !GetIsCurrentBackgroundTaskInstanceAllowed(taskInstanceId)) return;

				var mergedMbc = Merge(localMbc, remoteMbc);
				if (cancToken.IsCancellationRequested || !GetIsCurrentBackgroundTaskInstanceAllowed(taskInstanceId)) return;

				using (var ms = new MemoryStream())
				{
					serializer.WriteObject(ms, mergedMbc);
					ms.Position = 0;
					using (var content = new StreamContent(ms))
					{
						await client.PutAsync(new Uri(_oneDriveAppRootUri4Path + FILENAME + ":/content"), content, cancToken).ConfigureAwait(false);
					}
				}
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
			#endregion services
		}
		#endregion one drive writer methods


		#region rubbish bin event handlers
		private void OnCategory_NameChanged(object sender, EventArgs e)
		{
			Task tryRecycle = RunFunctionIfOpenAsyncT(async () =>
			{
				var cat = sender as Category;
				if (cat == null) return;

				var recycledCat = await _rubbishBin.GetCategoryAsync(cat.Name); //.ConfigureAwait(false);
				if (recycledCat != null)
				{
					//cat.NameChanged -= OnCategory_NameChanged;

					await RunInUiThreadAsync(() =>
					{
						bool setCurrent = CurrentCategoryId == cat.Id;
						_categories.Remove(cat);
						cat = recycledCat;
						_categories.Add(cat);

						foreach (var missingFldDsc in cat.FieldDescriptions.Where(fd0 => _fieldDescriptions.All(fd1 => fd1.Id != fd0.Id)))
						{
							cat.RemoveFieldDescription(missingFldDsc);
						}
						if (setCurrent) CurrentCategoryId = cat.Id;
					}).ConfigureAwait(false);

					//cat.NameChanged -= OnCategory_NameChanged;
					//cat.NameChanged += OnCategory_NameChanged;

					DataChanged?.Invoke(this, EventArgs.Empty);
				}
			});
		}

		private void OnFldDsc_CaptionChanged(object sender, EventArgs e)
		{
			Task tryRecycle = RunFunctionIfOpenAsyncT(async () =>
			{
				var fldDsc = sender as FieldDescription; var cats = _categories;
				if (fldDsc == null || cats == null) return;

				var recycledFldDsc = await _rubbishBin.GetFieldDescriptionAsync(fldDsc.Caption); //.ConfigureAwait(false);
				if (recycledFldDsc != null)
				{
					//fldDsc.CaptionChanged -= OnFldDsc_CaptionChanged;
					//foreach (var pv in fldDsc.PossibleValues)
					//{
					//	pv.VaalueChanged -= OnFieldValue_VaalueChanged;
					//}

					await RunInUiThreadAsync(() =>
					{
						bool setCurrent = CurrentFieldDescriptionId == fldDsc.Id;
						foreach (var cat in _categories)
						{
							cat.RemoveFieldDescription(fldDsc);
						}

						_fieldDescriptions.Remove(fldDsc);
						fldDsc = recycledFldDsc.Item2;
						_fieldDescriptions.Add(fldDsc);

						foreach (var catId in recycledFldDsc.Item1)
						{
							var registeredCat = _categories.FirstOrDefault(cat => cat.Id == catId);
							registeredCat?.AddFieldDescription(fldDsc);
						}
						if (setCurrent) CurrentFieldDescriptionId = fldDsc.Id;
					}).ConfigureAwait(false);

					//foreach (var pv in fldDsc.PossibleValues)
					//{
					//	pv.VaalueChanged -= OnFieldValue_VaalueChanged;
					//	pv.VaalueChanged += OnFieldValue_VaalueChanged;
					//}
					//fldDsc.CaptionChanged -= OnFldDsc_CaptionChanged;
					//fldDsc.CaptionChanged += OnFldDsc_CaptionChanged;

					DataChanged?.Invoke(this, EventArgs.Empty);
				}
			});
		}

		private void OnFieldValue_VaalueChanged(object sender, EventArgs e)
		{
			Task tryRecycle = RunFunctionIfOpenAsyncT(async () =>
			{
				var fldVal = sender as FieldValue; var cfd = _currentFieldDescription;
				if (fldVal == null || cfd == null) return;

				var recycledFldVal = await _rubbishBin.GetPossibleValueAsync(cfd, fldVal.Vaalue); //.ConfigureAwait(false);
				if (recycledFldVal != null)
				{
					//fldVal.VaalueChanged -= OnFieldValue_VaalueChanged;

					await RunInUiThreadAsync(() =>
					{
						cfd.RemovePossibleValue(fldVal);
						fldVal = recycledFldVal.Item2;
						cfd.AddPossibleValue(fldVal);
					}).ConfigureAwait(false);

					//fldVal.VaalueChanged -= OnFieldValue_VaalueChanged;
					//fldVal.VaalueChanged += OnFieldValue_VaalueChanged;

					DataChanged?.Invoke(this, EventArgs.Empty);
				}
			});
		}
		#endregion rubbish bin event handlers
	}
}
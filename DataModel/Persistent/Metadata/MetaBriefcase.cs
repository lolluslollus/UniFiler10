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

// LOLLO TODO Metabriefcase newly saves when adding a possible  value. Make sure I save not too often and not too rarely. And safely, too.

namespace UniFiler10.Data.Metadata
{
	[DataContract]
	public sealed class MetaBriefcase : OpenableObservableData
	{
		#region events
		public static event EventHandler UpdateOneDriveMetaBriefcaseRequested;
		#endregion events


		#region properties
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

		private string _currentCategoryId = null;
		[DataMember]
		public string CurrentCategoryId
		{
			get { return _currentCategoryId; }
			private set
			{
				if (_currentCategoryId != value)
				{
					_currentCategoryId = value;
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

		private string _currentFieldDescriptionId = null;
		[DataMember]
		public string CurrentFieldDescriptionId
		{
			get { return _currentFieldDescriptionId; }
			private set
			{
				if (_currentFieldDescriptionId != value)
				{
					_currentFieldDescriptionId = value;
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

		private readonly object _isElevatedLocker = new object();
		private bool _isElevated = false; // this must not be serialised because it does not belong in the metadata xml, so it has its own place in the registry.
		[IgnoreDataMember]
		public bool IsElevated
		{
			get
			{
				lock (_isElevatedLocker)
				{
					return _isElevated;
				}
			}
			set
			{
				lock (_isElevatedLocker)
				{
					if (_isElevated != value)
					{
						_isElevated = value;
						RaisePropertyChanged_UI();
					}
				}
			}
		}

		private volatile bool _isSyncedOnceSinceLastOpen = false;
		[IgnoreDataMember]
		public bool IsSyncedOnceSinceLastOpen { get { { return _isSyncedOnceSinceLastOpen; } } private set { { _isSyncedOnceSinceLastOpen = value; } } }

		private static string OneDriveAccessToken
		{
			get { return RegistryAccess.GetValue(ConstantData.REG_MBC_ODU_TKN); }
			set { RegistryAccess.TrySetValue(ConstantData.REG_MBC_ODU_TKN, value); }
		}
		private static readonly string[] _oneDriveScopes = { "onedrive.readwrite", "onedrive.appfolder", "wl.signin", "wl.offline_access", "wl.skydrive", "wl.skydrive_update" };
		//private const string _oneDriveAppRootUri = "https://api.onedrive.com/v1.0/drive/special/approot/";
		private const string _oneDriveAppRootUri4Path = "https://api.onedrive.com/v1.0/drive/special/approot:/"; // this is useful if you don't know the file ids but you know the paths
		private readonly RuntimeData _runtimeData = null;
		private readonly Briefcase _briefcase = null;
		#endregion properties


		#region lifecycle
		private static readonly object _instanceLocker = new object();
		internal static MetaBriefcase GetInstance(RuntimeData runtimeData, Briefcase briefcase)
		{
			lock (_instanceLocker)
			{
				if (_instance == null/* || _instance._isDisposed*/)
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
		}

		//protected override void Dispose(bool isDisposing)
		//{
		//	_fieldDescriptions?.Dispose();
		//	_fieldDescriptions = null;
		//	_categories?.Dispose();
		//	_categories = null;

		//	base.Dispose(isDisposing);
		//}

		protected override async Task OpenMayOverrideAsync()
		{
			bool wantToUseOneDrive = _briefcase.IsWantToUseOneDrive;
			try
			{
				_oneDriveMetaBriefcaseSemaphore.WaitOne();

				wantToUseOneDrive = _briefcase.IsWantToUseOneDrive;
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

					// if (!string.IsNullOrEmpty(oneDriveAccountSession?.AccessToken)) OneDriveAccessToken = oneDriveAccountSession.AccessToken;
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
				await LoadAsync(wantToUseOneDrive).ConfigureAwait(false);
				SemaphoreExtensions.TryRelease(_oneDriveMetaBriefcaseSemaphore);
			}
		}

		protected override async Task CloseMayOverrideAsync()
		{
			await Save2Async().ConfigureAwait(false);
			if (_briefcase.IsWantAndCanUseOneDrive) UpdateOneDriveMetaBriefcaseRequested?.Invoke(this, EventArgs.Empty);

			//var fldDscs = _fieldDescriptions;
			//if (fldDscs != null)
			//{
			//	foreach (var fldDsc in fldDscs)
			//	{
			//		fldDsc.Dispose();
			//	}
			//}

			//var cats = _categories;
			//if (cats != null)
			//{
			//	foreach (var cat in cats)
			//	{
			//		cat.Dispose();
			//	}
			//}
		}
		#endregion lifecycle


		#region loading methods
		public const string FILENAME = "LolloSessionDataMetaBriefcase.xml";
		private readonly object _reloadLocker = new object();
		private StorageFile _sourceFile = null;
		private StorageFile SourceFile
		{
			get { lock (_reloadLocker) { return _sourceFile; } }
			set { lock (_reloadLocker) { _sourceFile = value; } }
		}
		private bool _isPropsLoaded = false;
		private bool IsPropsLoaded
		{
			get { lock (_reloadLocker) { return _isPropsLoaded; } }
			set { lock (_reloadLocker) { _isPropsLoaded = value; } }
		}

		public void SetSourceFileJustOnce(StorageFile sourceFile)
		{
			SourceFile = sourceFile;
			IsPropsLoaded = false;
		}
		public void SetReloadJustOnce()
		{
			IsPropsLoaded = false;
		}

		//private async Task LoadAsync()
		//{
		//	if (_isPropsLoaded) return;

		//	// LOLLO NOTE on the onedrive sdk
		//	// http://blogs.u2u.net/diederik/post/2015/04/06/Using-the-OneDrive-SDK-in-universal-apps.aspx
		//	// https://msdn.microsoft.com/en-us/magazine/mt632271.aspx
		//	// https://onedrive.live.com/?authkey=%21ADtqHIG1cV7g5EI&cid=40CFFDE85F1AB56A&id=40CFFDE85F1AB56A%212187&parId=40CFFDE85F1AB56A%212186&action=locate

		//	MetaBriefcase newMetaBriefcase = null;
		//	bool mustSyncLocal = false;
		//	bool mustSyncOneDrive = false;

		//	StorageFile localFile = null;
		//	var localFileLastModifiedWhen = default(DateTime);
		//	var oneDriveFileLastModifiedWhen = default(DateTime);

		//	// set localFile and localFileLastModifiedWhen
		//	try
		//	{
		//		if (_sourceFile != null)
		//		{
		//			localFile = _sourceFile;
		//			localFileLastModifiedWhen = new DateTime(9999, 12, 31);
		//		}
		//		else
		//		{
		//			if (CancToken.IsCancellationRequested) return;
		//			localFile = (await GetDirectory().TryGetItemAsync(FILENAME).AsTask().ConfigureAwait(false) as StorageFile);
		//			if (CancToken.IsCancellationRequested) return;

		//			if (localFile != null)
		//			{
		//				var localFileProps = await localFile.GetBasicPropertiesAsync().AsTask().ConfigureAwait(false);
		//				localFileLastModifiedWhen = localFileProps.DateModified.DateTime.ToUniversalTime();
		//			}
		//			else
		//			{
		//				localFile = await GetDirectory()
		//					.CreateFileAsync(FILENAME, CreationCollisionOption.OpenIfExists)
		//					.AsTask().ConfigureAwait(false);
		//			}
		//		}
		//	}
		//	catch (Exception ex) { Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename); }
		//	finally
		//	{
		//		_sourceFile = null;
		//	}

		//	if (CancToken.IsCancellationRequested) return;

		//	using (var client = new HttpClient())
		//	{
		//		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OneDriveAccessToken);

		//		// set oneDriveFileLastModifiedWhen
		//		try
		//		{
		//			var odFileProps = JObject.Parse(await client.GetStringAsync(new Uri(_oneDriveAppRootUri4Path + FILENAME)).ConfigureAwait(false));
		//			oneDriveFileLastModifiedWhen = odFileProps.GetValue("lastModifiedDateTime").ToObject<DateTime>().ToUniversalTime();
		//		}
		//		catch (Exception ex) { Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename); }

		//		var serializer = new DataContractSerializer(typeof(MetaBriefcase));
		//		if (oneDriveFileLastModifiedWhen > localFileLastModifiedWhen) // pick up data from one drive
		//		{
		//			mustSyncLocal = true;
		//			try
		//			{
		//				using (var odFileContent = await client.GetStreamAsync(new Uri(_oneDriveAppRootUri4Path + FILENAME + ":/content")).ConfigureAwait(false))
		//				{
		//					newMetaBriefcase = (MetaBriefcase)serializer.ReadObject(odFileContent);
		//				}
		//			}
		//			catch (SerializationException) // one drive has invalid data: pick up the local data. This must never happen!
		//			{
		//				await Logger.AddAsync("SerializationException reading from OneDrive", Logger.FileErrorLogFilename).ConfigureAwait(false);
		//				mustSyncOneDrive = true;
		//				try
		//				{
		//					using (var localFileContent = await localFile.OpenStreamForReadAsync().ConfigureAwait(false))
		//					{
		//						newMetaBriefcase = (MetaBriefcase)(serializer.ReadObject(localFileContent));
		//					}
		//				}
		//				catch (Exception ex) { Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename); }
		//			}
		//		}
		//		else // pick up date from local file
		//		{
		//			mustSyncOneDrive = true;
		//			try
		//			{
		//				using (var localFileContent = await localFile.OpenStreamForReadAsync().ConfigureAwait(false))
		//				{
		//					newMetaBriefcase = (MetaBriefcase)(serializer.ReadObject(localFileContent));
		//				}
		//			}
		//			catch (Exception ex) { Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename); }
		//		}
		//	}

		//	if (newMetaBriefcase != null) // if I could pick up some data, use it and sync whatever needs syncing
		//	{
		//		_isPropsLoaded = true;
		//		CopyXMLPropertiesFrom(newMetaBriefcase);
		//		if (mustSyncLocal)
		//		{
		//			Task syncLocal = Task.Run(() => Save2Async(), CancToken);
		//		}
		//		if (mustSyncOneDrive)
		//		{
		//			UpdateOneDriveMetaBriefcaseRequested?.Invoke(this, EventArgs.Empty);
		//			// I don't use the following, but it is interesting and it works.
		//			//Task saveToOneDrive = Task.Run(() => SaveToOneDrive(localFileContent, _oneDriveAccountSession.AccessToken), CancToken).ContinueWith(state => localFileContent?.Dispose());
		//		}
		//	}

		//	// load non-xml properties
		//	bool isElevated = false;
		//	bool.TryParse(RegistryAccess.GetValue(ConstantData.REG_MBC_IS_ELEVATED), out isElevated);
		//	IsElevated = isElevated;

		//	Debug.WriteLine("ended method MetaBriefcase.LoadAsync()");
		//}

		private async Task LoadAsync(bool wantToUseOneDrive)
		{
			// LOLLO TODO if, for any reason, the bkg task failed, I will load the old metadata here:
			// no good.
			if (IsPropsLoaded && IsSyncedOnceSinceLastOpen) return;

			// LOLLO NOTE on the onedrive sdk
			// http://blogs.u2u.net/diederik/post/2015/04/06/Using-the-OneDrive-SDK-in-universal-apps.aspx
			// https://msdn.microsoft.com/en-us/magazine/mt632271.aspx
			// https://onedrive.live.com/?authkey=%21ADtqHIG1cV7g5EI&cid=40CFFDE85F1AB56A&id=40CFFDE85F1AB56A%212187&parId=40CFFDE85F1AB56A%212186&action=locate

			MetaBriefcase newMetaBriefcase = null;
			bool mustSyncLocal = false;
			bool mustSyncOneDrive = false;

			StorageFile localFile = null;

			// set localFile and localFileLastModifiedWhen
			try
			{
				var sourceFile = SourceFile;
				if (sourceFile != null)
				{
					localFile = sourceFile;
				}
				else
				{
					localFile = await GetDirectory()
						.CreateFileAsync(FILENAME, CreationCollisionOption.OpenIfExists)
						.AsTask().ConfigureAwait(false);
				}
			}
			catch (Exception ex) { Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename); }
			finally
			{
				SourceFile = null;
			}

			if (CancToken.IsCancellationRequested) return;

			var serializer = new DataContractSerializer(typeof(MetaBriefcase));
			if (wantToUseOneDrive && _runtimeData.IsConnectionAvailable)
			{
				using (var client = new HttpClient())
				{
					client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OneDriveAccessToken);

					try
					{
						using (var odFileContent = await client.GetStreamAsync(new Uri(_oneDriveAppRootUri4Path + FILENAME + ":/content")).ConfigureAwait(false))
						{
							newMetaBriefcase = (MetaBriefcase)serializer.ReadObject(odFileContent);
						}
						mustSyncLocal = true;
						IsSyncedOnceSinceLastOpen = true;
					}
					catch (SerializationException) // one drive has invalid data: pick up the local data. This must never happen!
					{
						await Logger.AddAsync("SerializationException reading from OneDrive", Logger.FileErrorLogFilename).ConfigureAwait(false);
						try
						{
							using (var localFileContent = await localFile.OpenStreamForReadAsync().ConfigureAwait(false))
							{
								newMetaBriefcase = (MetaBriefcase)(serializer.ReadObject(localFileContent));
							}
						}
						catch (Exception ex1) { Logger.Add_TPL(ex1.ToString(), Logger.FileErrorLogFilename); }
						mustSyncOneDrive = true;
						IsSyncedOnceSinceLastOpen = true;
					}
					catch (Exception ex0) // one drive could not connect to one drive: pick up the local data.
					{
						try
						{
							using (var localFileContent = await localFile.OpenStreamForReadAsync().ConfigureAwait(false))
							{
								newMetaBriefcase = (MetaBriefcase)(serializer.ReadObject(localFileContent));
							}
						}
						catch (Exception ex1) { Logger.Add_TPL(ex1.ToString(), Logger.FileErrorLogFilename); }
						IsSyncedOnceSinceLastOpen = false;
					}
				}
			}
			else
			{
				try
				{
					using (var localFileContent = await localFile.OpenStreamForReadAsync().ConfigureAwait(false))
					{
						newMetaBriefcase = (MetaBriefcase)(serializer.ReadObject(localFileContent));
					}
				}
				catch (Exception ex1) { Logger.Add_TPL(ex1.ToString(), Logger.FileErrorLogFilename); }
				IsSyncedOnceSinceLastOpen = false;
			}

			if (newMetaBriefcase != null) // if I could pick up some data, use it and sync whatever needs syncing
			{
				IsPropsLoaded = true;
				CopyXMLPropertiesFrom(newMetaBriefcase);
				if (mustSyncLocal)
				{
					Task syncLocal = Task.Run(() => Save2Async(), CancToken);
				}
				if (mustSyncOneDrive)
				{
					UpdateOneDriveMetaBriefcaseRequested?.Invoke(this, EventArgs.Empty);
					// I don't use the following, but it is interesting and it works.
					//Task saveToOneDrive = Task.Run(() => SaveToOneDrive(localFileContent, _oneDriveAccountSession.AccessToken), CancToken).ContinueWith(state => localFileContent?.Dispose());
				}
			}

			// load non-xml properties
			bool isElevated = false;
			bool.TryParse(RegistryAccess.GetValue(ConstantData.REG_MBC_IS_ELEVATED), out isElevated);
			IsElevated = isElevated;

			Debug.WriteLine("ended method MetaBriefcase.LoadAsync()");
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
				_oneDriveMetaBriefcaseSemaphore.WaitOne();

				if (file == null)
				{
					file = await GetDirectory()
						.TryGetItemAsync(FILENAME).AsTask().ConfigureAwait(false) as StorageFile;
				}

				string savedMetaBriefcase = string.Empty;
				if (file != null)
				{
					using (var localFileContent = await file.OpenStreamForReadAsync().ConfigureAwait(false))
					{
						using (StreamReader streamReader = new StreamReader(localFileContent))
						{
							savedMetaBriefcase = streamReader.ReadToEnd();
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

						if (!currentMetaBriefcase.Trim().Equals(savedMetaBriefcase.Trim()))
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
				SemaphoreExtensions.TryRelease(_oneDriveMetaBriefcaseSemaphore);
			}

			return result;
		}

		/// <summary>
		/// This semaphore protects the token and the file, operating system-wide
		/// </summary>
		private static readonly Semaphore _oneDriveMetaBriefcaseSemaphore = new Semaphore(1, 1, "Unifiler10_OneDriveMetaBriefcaseSemaphore");
		public async Task SaveLocalFileToOneDriveAsync(CancellationToken cancToken)
		{
			try
			{
				if (!_runtimeData.IsConnectionAvailable) return;

				_oneDriveMetaBriefcaseSemaphore.WaitOne();

				var localFile = await GetDirectory().TryGetItemAsync(FILENAME).AsTask(cancToken).ConfigureAwait(false) as StorageFile;
				if (localFile == null) return;

				using (var localFileContent = await localFile.OpenStreamForReadAsync().ConfigureAwait(false))
				{
					var localFileContentString = string.Empty;
					using (var streamReader = new StreamReader(localFileContent))
					{
						localFileContentString = streamReader.ReadToEnd();

						if (cancToken.IsCancellationRequested) return;

						using (var client = new HttpClient())
						{
							client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OneDriveAccessToken);

							DateTime odLastModifiedWhen = default(DateTime);
							string remoteFilecontentString = string.Empty;
							try
							{
								var odFileProps = JObject.Parse(await client.GetStringAsync(new Uri(_oneDriveAppRootUri4Path + FILENAME)).ConfigureAwait(false));
								odLastModifiedWhen = odFileProps.GetValue("lastModifiedDateTime").ToObject<DateTime>().ToUniversalTime();
								remoteFilecontentString = await client.GetStringAsync(new Uri(_oneDriveAppRootUri4Path + FILENAME + ":/content")).ConfigureAwait(false);
							}
							catch { }

							if (localFileContentString.Trim().Equals(remoteFilecontentString.Trim(), StringComparison.Ordinal)) return;
							var localFileProps = await localFile.GetBasicPropertiesAsync().AsTask().ConfigureAwait(false);
							var lfLastModifiedWhen = localFileProps.DateModified.DateTime.ToUniversalTime();
							if (lfLastModifiedWhen <= odLastModifiedWhen) return;

							if (cancToken.IsCancellationRequested) return;

							localFileContent.Position = 0;
							using (var content = new StreamContent(localFileContent))
							{
								await client.PutAsync(new Uri(_oneDriveAppRootUri4Path + FILENAME + ":/content"), content, cancToken).ConfigureAwait(false);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
			finally
			{
				SemaphoreExtensions.TryRelease(_oneDriveMetaBriefcaseSemaphore);
			}
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


		#region while open methods
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

		public Task<bool> AddCategoryAsync()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				string name = RuntimeData.GetText("NewCategory");
				var newCat = new Category() { Name = name, IsCustom = true, IsJustAdded = true };

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
					//if (isRemoved) cat?.Dispose();
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

		public Task<bool> AddFieldDescriptionAsync()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				string name = RuntimeData.GetText("NewFieldDescription");
				var newFieldDesc = new FieldDescription(name, true, true);

				if (FieldDescription.Check(newFieldDesc) && !_fieldDescriptions.Any(fd => fd.Caption == newFieldDesc.Caption || fd.Id == newFieldDesc.Id))
				{
					await RunInUiThreadAsync(() => _fieldDescriptions.Add(newFieldDesc)).ConfigureAwait(false);
					return true;
				}
				return false;
			});
		}

		public Task<bool> RemoveFieldDescriptionAsync(FieldDescription fldDesc)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (fldDesc != null && (fldDesc.IsJustAdded || IsElevated))
				{
					bool isRemoved = false;
					await RunInUiThreadAsync(delegate
					{
						foreach (var cat in _categories)
						{
							cat.RemoveFieldDescription(fldDesc);
						}
						isRemoved = _fieldDescriptions.Remove(fldDesc);
					}).ConfigureAwait(false);
					//if (isRemoved) fldDesc?.Dispose();
					return isRemoved;
				}
				return false;
			});
		}

		public Task<bool> AddPossibleValueToCurrentFieldDescriptionAsync()
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
				await RunInUiThreadAsync(() => isAdded = fldDsc.AddPossibleValue(newFldVal)).ConfigureAwait(false);
				if (isAdded && save)
				{
					isAdded = await Save2Async().ConfigureAwait(false);
					if (_briefcase.IsWantAndCanUseOneDrive) UpdateOneDriveMetaBriefcaseRequested?.Invoke(this, EventArgs.Empty);
				}
				return isAdded;
			});
		}

		public Task<bool> RemovePossibleValueFromCurrentFieldDescriptionAsync(FieldValue fldVal)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (fldVal == null || _currentFieldDescription == null || (!fldVal.IsJustAdded && !IsElevated)) return false;

				bool isRemoved = false;
				await RunInUiThreadAsync(() => isRemoved = _currentFieldDescription.RemovePossibleValue(fldVal)).ConfigureAwait(false);
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
			var result = await RunFunctionIfOpenAsyncTB(() => Save2Async());
			if (_briefcase.IsWantAndCanUseOneDrive) UpdateOneDriveMetaBriefcaseRequested?.Invoke(this, EventArgs.Empty);
			return result;
		}
		#endregion while open methods
	}
}

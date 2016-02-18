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

// LOLLO TODO Metabriefcase newly saves when adding a possible  value. Make sure I save not too often and not too rarely. And safely, too.

namespace UniFiler10.Data.Metadata
{
	[DataContract]
	public sealed class MetaBriefcase : OpenableObservableDisposableData
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
					//UpdateCurrentFieldDescription2();
					RaisePropertyChanged_UI();
				}
				else if (_currentCategory == null)
				{
					UpdateCurrentCategory2();
					//UpdateCurrentFieldDescription2();
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
					RaisePropertyChanged_UI();
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
		private SwitchableObservableDisposableCollection<Category> _categories = new SwitchableObservableDisposableCollection<Category>();
		[DataMember]
		public SwitchableObservableDisposableCollection<Category> Categories { get { return _categories; } private set { _categories = value; RaisePropertyChanged_UI(); } }

		// we cannot make this readonly because it is serialised. we only use the setter for serialising.
		private SwitchableObservableDisposableCollection<FieldDescription> _fieldDescriptions = new SwitchableObservableDisposableCollection<FieldDescription>();
		[DataMember]
		public SwitchableObservableDisposableCollection<FieldDescription> FieldDescriptions { get { return _fieldDescriptions; } private set { _fieldDescriptions = value; RaisePropertyChanged_UI(); } }

		private volatile bool _isElevated = false; // LOLLO TODO this does not belong here. move it to the briefcase.
		[DataMember]
		public bool IsElevated { get { return _isElevated; } set { _isElevated = value; RaisePropertyChanged_UI(); } }

		//private static readonly SemaphoreSlimSafeRelease _loadSaveSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		//private IOneDriveClient _oneDriveClient = null;
		//private static AccountSession _oneDriveAccountSession = null;

		public static string OneDriveAccessToken
		{
			get { return RegistryAccess.GetValue(ConstantData.REG_MBC_ODU_TKN); }
			private set { RegistryAccess.TrySetValue(ConstantData.REG_MBC_ODU_TKN, value); }
		}
		private static readonly string[] _oneDriveScopes = { "onedrive.readwrite", "onedrive.appfolder", "wl.signin", "wl.offline_access", "wl.skydrive", "wl.skydrive_update" };
		//private const string _oneDriveAppRootUri = "https://api.onedrive.com/v1.0/drive/special/approot/";
		private const string _oneDriveAppRootUri4Path = "https://api.onedrive.com/v1.0/drive/special/approot:/"; // this is useful if you don't know the file ids but you know the paths
		private readonly RuntimeData _runtimeData = null;
		#endregion properties


		#region lifecycle
		private static readonly object _instanceLocker = new object();
		internal static MetaBriefcase GetInstance(RuntimeData runtimeData)
		{
			lock (_instanceLocker)
			{
				if (_instance == null || _instance._isDisposed)
				{
					_instance = new MetaBriefcase(runtimeData);
				}
				return _instance;
			}
		}

		private MetaBriefcase(RuntimeData runtimeData)
		{
			_runtimeData = runtimeData;
		}

		protected override void Dispose(bool isDisposing)
		{
			_fieldDescriptions?.Dispose();
			_fieldDescriptions = null;
			_categories?.Dispose();
			_categories = null;

			base.Dispose(isDisposing);
		}
		protected override async Task OpenMayOverrideAsync()
		{
			if (_runtimeData.IsConnectionAvailable)
			{
				try
				{
					// LOLLO TODO what if the connection is very slow?

					var oneDriveClient = OneDriveClientExtensions.GetUniversalClient(_oneDriveScopes);
					var oneDriveAccountSession = await oneDriveClient.AuthenticateAsync();

					// LOLLO NOTE in the dashboard, set settings - API settings - Mobile or desktop client app = true
					// and here, use the authentication broker with appId = dashboard - settings - app settings - client id
					// this allows working outside the UI thread, for whatever reason.
					// However, you still need to be in the UI thread here.

					OneDriveAccessToken = oneDriveAccountSession.AccessToken;
					// var appRoot = await oneDriveClient.Drive.Special.AppRoot.Request().GetAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
				}
			}
			await LoadAsync().ConfigureAwait(false);
		}

		protected override async Task CloseMayOverrideAsync()
		{
			await Save2Async().ConfigureAwait(false);
			UpdateOneDriveMetaBriefcaseRequested?.Invoke(this, EventArgs.Empty);

			var fldDscs = _fieldDescriptions;
			if (fldDscs != null)
			{
				foreach (var fldDsc in fldDscs)
				{
					fldDsc.Dispose();
				}
			}

			var cats = _categories;
			if (cats != null)
			{
				foreach (var cat in cats)
				{
					cat.Dispose();
				}
			}
		}
		#endregion lifecycle


		#region loading methods
		public const string FILENAME = "LolloSessionDataMetaBriefcase.xml";
		private StorageFile _sourceFile = null;
		public void SetSourceFileJustOnce(StorageFile sourceFile)
		{
			_sourceFile = sourceFile;
		}

		private async Task LoadAsync()
		{
			// LOLLO NOTE on the onedrive sdk
			// http://blogs.u2u.net/diederik/post/2015/04/06/Using-the-OneDrive-SDK-in-universal-apps.aspx
			// https://msdn.microsoft.com/en-us/magazine/mt632271.aspx
			// https://onedrive.live.com/?authkey=%21ADtqHIG1cV7g5EI&cid=40CFFDE85F1AB56A&id=40CFFDE85F1AB56A%212187&parId=40CFFDE85F1AB56A%212186&action=locate

			string errorMessage = string.Empty;
			MetaBriefcase newMetaBriefcase = null;

			try
			{
				_oneDriveMetaBriefcaseSemaphore.WaitOne();
				// await _loadSaveSemaphore.WaitAsync(CancToken).ConfigureAwait(false);

				StorageFile localFile = null;
				var odLastModifiedWhen = default(DateTime);
				var lfLastModifiedWhen = default(DateTime);

				try
				{
					if (_sourceFile != null)
					{
						localFile = _sourceFile;
						lfLastModifiedWhen = new DateTime(9999, 12, 31);
					}
					else
					{
						localFile = (await GetDirectory().TryGetItemAsync(FILENAME).AsTask().ConfigureAwait(false) as StorageFile);

						if (CancToken.IsCancellationRequested) return;

						if (localFile != null)
						{
							var localFileProps = await localFile.GetBasicPropertiesAsync().AsTask().ConfigureAwait(false);
							lfLastModifiedWhen = localFileProps.DateModified.DateTime;
						}
						else
						{
							localFile = await GetDirectory()
								.CreateFileAsync(FILENAME, CreationCollisionOption.OpenIfExists)
								.AsTask().ConfigureAwait(false);
						}
					}
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
				}
				finally
				{
					_sourceFile = null; // LOLLO TODO see if you can make this less crappy!
				}
				//String ssss = null; //this is useful when you debug and want to see the file as a string
				//using (IInputStream inStream = await file.OpenSequentialReadAsync())
				//{
				//    using (StreamReader streamReader = new StreamReader(inStream.AsStreamForRead()))
				//    {
				//      ssss = streamReader.ReadToEnd();
				//    }
				//}

				if (CancToken.IsCancellationRequested) return;

				using (var client = new HttpClient())
				{
					client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OneDriveAccessToken);

					try
					{
						var odFileProps = JObject.Parse(await client.GetStringAsync(new Uri(_oneDriveAppRootUri4Path + FILENAME)));
						odLastModifiedWhen = odFileProps.GetValue("lastModifiedDateTime").ToObject<DateTime>();
					}
					catch { }

					var serializer = new DataContractSerializer(typeof(MetaBriefcase));
					if (odLastModifiedWhen > lfLastModifiedWhen)
					{
						using (var odFileContent = await client.GetStreamAsync(new Uri(_oneDriveAppRootUri4Path + FILENAME + ":/content")))
						{
							newMetaBriefcase = (MetaBriefcase)serializer.ReadObject(odFileContent);
						}
						// sync local from OneDrive
						Task syncLocal = Task.Run(() => newMetaBriefcase?.Save2Async(), CancToken);
					}
					else
					{
						var localFileContent = await localFile.OpenStreamForReadAsync().ConfigureAwait(false);
						newMetaBriefcase = (MetaBriefcase)(serializer.ReadObject(localFileContent));
						// sync OneDrive from local
						UpdateOneDriveMetaBriefcaseRequested?.Invoke(this, EventArgs.Empty);
						//Task saveToOneDrive = Task.Run(() => SaveToOneDrive(localFileContent, _oneDriveAccountSession.AccessToken), CancToken).ContinueWith(state => localFileContent?.Dispose());
					}
				}
			}
			catch (Exception ex) //must be tolerant or the app might crash when starting
			{
				errorMessage = "could not restore the data, starting afresh";
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename);
			}
			finally
			{
				_oneDriveMetaBriefcaseSemaphore.TryRelease();
				//SemaphoreSlimSafeRelease.TryRelease(_loadSaveSemaphore);
			}

			if (string.IsNullOrWhiteSpace(errorMessage))
			{
				if (newMetaBriefcase != null) CopyFrom(newMetaBriefcase);
			}

			Debug.WriteLine("ended method MetaBriefcase.LoadAsync()");
		}
		private async Task<bool> Save2Async(StorageFile file = null)
		{
			//for (int i = 0; i < 100000000; i++) //wait a few seconds, for testing
			//{
			//    String aaa = i.ToString();
			//}
			await Logger.AddAsync("MetaBriefcase about to save", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);

			try
			{
				_oneDriveMetaBriefcaseSemaphore.WaitOne();
				// await _loadSaveSemaphore.WaitAsync().ConfigureAwait(false);

				if (file == null)
				{
					file = await GetDirectory()
						.CreateFileAsync(FILENAME, CreationCollisionOption.ReplaceExisting)
						.AsTask().ConfigureAwait(false);
				}

				var memoryStream = new MemoryStream();
				var sessionDataSerializer = new DataContractSerializer(typeof(MetaBriefcase));
				sessionDataSerializer.WriteObject(memoryStream, this);

				using (var fileStream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
				{
					memoryStream.Seek(0, SeekOrigin.Begin);
					await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
					await memoryStream.FlushAsync().ConfigureAwait(false);
					await fileStream.FlushAsync().ConfigureAwait(false);
				}

				Debug.WriteLine("ended method MetaBriefcase.SaveAsync()");
				return true;
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
			finally
			{
				_oneDriveMetaBriefcaseSemaphore.TryRelease();
				//SemaphoreSlimSafeRelease.TryRelease(_loadSaveSemaphore);
			}
			return false;
		}

		private static readonly Semaphore _oneDriveMetaBriefcaseSemaphore = new Semaphore(1, 1, "Unifiler10_OneDriveMetaBriefcaseSemaphore");
		public async Task SaveToOneDriveAsync(CancellationToken cancToken)
		{
			try
			{
				if (!_runtimeData.IsConnectionAvailable) return;

				var localFile =
					await GetDirectory().TryGetItemAsync(FILENAME).AsTask(cancToken).ConfigureAwait(false) as StorageFile;
				if (localFile == null) return;

				_oneDriveMetaBriefcaseSemaphore.WaitOne();

				using (var localFileContent = await localFile.OpenStreamForReadAsync().ConfigureAwait(false))
				{
					var localFileContentString = string.Empty;
					using (var streamReader = new StreamReader(localFileContent))
					{
						localFileContentString = streamReader.ReadToEnd();

						if (cancToken.IsCancellationRequested) return;

						//var uri = new Uri(_oneDriveAppRootUri4Path + FILENAME);
						using (var client = new HttpClient())
						{
							client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OneDriveAccessToken);
							//var json = await client.GetStringAsync(uri);

							var uri = new Uri(_oneDriveAppRootUri4Path + FILENAME + ":/content");
							string remoteFilecontentString = string.Empty;
							try
							{
								remoteFilecontentString = await client.GetStringAsync(uri).ConfigureAwait(false);
							}
							catch { }

							if (localFileContentString.Trim().Equals(remoteFilecontentString.Trim(), StringComparison.Ordinal)) return;

							if (cancToken.IsCancellationRequested) return;

							localFileContent.Position = 0;
							using (var content = new StreamContent(localFileContent))
							{
								await client.PutAsync(uri, content, cancToken).ConfigureAwait(false);
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
				_oneDriveMetaBriefcaseSemaphore.TryRelease();
			}
		}

		private bool CopyFrom(MetaBriefcase source)
		{
			if (source == null) return false;

			IsElevated = source._isElevated;
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
			return ApplicationData.Current.LocalFolder; // was .RoamingFolder;
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

		public Task SetIsElevatedAsync(bool newValue)
		{
			return RunFunctionIfOpenAsyncA(delegate { IsElevated = newValue; });
		}

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
				if (cat != null && (cat.IsJustAdded || _isElevated))
				{
					bool isRemoved = false;
					await RunInUiThreadAsync(() => isRemoved = _categories.Remove(cat)).ConfigureAwait(false);
					return isRemoved;
				}
				else
				{
					return false;
				}
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
				if (fldDesc != null && (fldDesc.IsJustAdded || _isElevated))
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
				if (isAdded && save) isAdded = await Save2Async().ConfigureAwait(false);
				return isAdded;
			});
		}

		public Task<bool> RemovePossibleValueFromCurrentFieldDescriptionAsync(FieldValue fldVal)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (fldVal == null || _currentFieldDescription == null || (!fldVal.IsJustAdded && !_isElevated)) return false;

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
				if (fldDsc == null || _currentCategory == null || (!fldDsc.JustAssignedToCats.Contains(_currentCategoryId) && !_isElevated)) return false;

				bool isRemoved = false;
				await RunInUiThreadAsync(() => isRemoved = _currentCategory.RemoveFieldDescription(fldDsc)).ConfigureAwait(false);
				return isRemoved;
			});
		}

		public Task<bool> SaveACopyAsync(StorageFile file)
		{
			return RunFunctionIfOpenAsyncTB(() => Save2Async(file));
		}

		public async Task<bool> SaveAsync()
		{
			var result = await RunFunctionIfOpenAsyncTB(() => Save2Async());
			UpdateOneDriveMetaBriefcaseRequested?.Invoke(this, EventArgs.Empty);
			return result;
		}
		#endregion while open methods
	}
}

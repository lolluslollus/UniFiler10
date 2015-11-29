using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Metadata;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Streams;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public sealed class Briefcase : OpenableObservableData //, IPaneOpener
	{
		#region construct and dispose
		private static readonly object _instanceLock = new object();
		public static Briefcase GetOrCreateInstance()
		{
			lock (_instanceLock)
			{
				if (_instance == null || _instance._isDisposed)
				{
					_instance = new Briefcase();
				}
				return _instance;
			}
		}
		public static Briefcase GetCurrentInstance()
		{
			lock (_instanceLock)
			{
				return _instance;
			}
		}
		private Briefcase() { }
		#endregion construct and dispose

		#region open and close
		protected override async Task OpenMayOverrideAsync()
		{
			_metaBriefcase = MetaBriefcase.CreateInstance();
			await _metaBriefcase.OpenAsync().ConfigureAwait(false);
			RaisePropertyChanged_UI(nameof(MetaBriefcase)); // notify the UI once the data has been loaded

			await LoadAsync().ConfigureAwait(false);

			_runtimeData = RuntimeData.CreateInstance(this);
			await _runtimeData.OpenAsync().ConfigureAwait(false);
			RaisePropertyChanged_UI(nameof(RuntimeData)); // notify the UI once the data has been loaded

			await UpdateCurrentBinder2Async(false).ConfigureAwait(false);
		}
		protected override async Task CloseMayOverrideAsync()
		{
			await SaveAsync().ConfigureAwait(false);

			await CloseCurrentBinderAsync().ConfigureAwait(false);

			var rd = _runtimeData;
			if (rd != null)
			{
				await rd.CloseAsync();
				rd.Dispose();
				RuntimeData = null;
			}

			var mb = _metaBriefcase;
			if (mb != null)
			{
				await mb.CloseAsync().ConfigureAwait(false);
				mb.Dispose();
				MetaBriefcase = null;
			}
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_dbNames?.Dispose();
			_dbNames = null;
		}
		#endregion open and close

		#region properties
		private static Briefcase _instance = null;

		private MetaBriefcase _metaBriefcase = null;
		[IgnoreDataMember]
		public MetaBriefcase MetaBriefcase { get { return _metaBriefcase; } private set { if (_metaBriefcase != value) { _metaBriefcase = value; RaisePropertyChanged_UI(); } } }

		private RuntimeData _runtimeData = null;
		[IgnoreDataMember]
		public RuntimeData RuntimeData { get { return _runtimeData; } private set { _runtimeData = value; RaisePropertyChanged_UI(); } }

		private bool _isAllowMeteredConnection = false;
		[DataMember]
		public bool IsAllowMeteredConnection { get { return _isAllowMeteredConnection; } set { if (_isAllowMeteredConnection != value) { _isAllowMeteredConnection = value; RaisePropertyChanged_UI(); } } }

		private string _currentBinderName = string.Empty;
		[DataMember]
		public string CurrentBinderName
		{
			get { return _currentBinderName; }
			set
			{
				if (_currentBinderName != value)
				{
					_currentBinderName = value;
					Task upd = UpdateCurrentBinderAsync().ContinueWith(delegate
					{
						RaisePropertyChanged_UI();
					});
				}
				else if (_currentBinder == null)
				{
					Task upd = UpdateCurrentBinderAsync();
				}
			}
		}

		private Binder _currentBinder = null;
		[IgnoreDataMember]
		public Binder CurrentBinder { get { return _currentBinder; } private set { if (_currentBinder != value) { _currentBinder = value; RaisePropertyChanged_UI(); } } }

		private SwitchableObservableCollection<string> _dbNames = new SwitchableObservableCollection<string>();
		[DataMember]
		public SwitchableObservableCollection<string> DbNames { get { return _dbNames; } private set { if (_dbNames != value) { _dbNames = value; RaisePropertyChanged_UI(); } } }

		private string _newDbName = string.Empty;
		[DataMember]
		public string NewDbName { get { return _newDbName; } set { if (_newDbName != value) { _newDbName = value; RaisePropertyChanged_UI(); } } }
		#endregion properties


		#region loaded methods
		public Task<bool> SetCurrentBinderAsync(string dbName)
		{
			return RunFunctionWhileOpenAsyncTB(delegate
			{
				_currentBinderName = dbName;
				return UpdateCurrentBinder2Async(false);
			});
		}

		public Task<bool> OpenCurrentBinderAsync()
		{
			return RunFunctionWhileOpenAsyncTB(delegate
			{
				return UpdateCurrentBinder2Async(true);
			});
		}

		public Task<bool> OpenBinderAsync(string dbName)
		{
			return RunFunctionWhileOpenAsyncTB(delegate
			{
				_currentBinderName = dbName;
				return UpdateCurrentBinder2Async(true);
			});
		}

		public Task<bool> AddBinderAsync(string dbName)
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				return await AddBinder2Async(dbName).ConfigureAwait(false);
			});
		}
		private async Task<bool> AddBinder2Async(string dbName)
		{
			if (IsNewDbNameWrong2(dbName))
			{
				return false;
			}
			else
			{
				await RunInUiThreadAsync(delegate
				{
					DbNames.Add(dbName);
					CurrentBinderName = dbName;
				}).ConfigureAwait(false);
				return true;
			}
		}
		public Task<bool> DeleteBinderAsync(string dbName)
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				if (string.IsNullOrWhiteSpace(dbName)) return false;

				if (_dbNames.Remove(dbName))
				{
					// close the current binder if it is the one to be deleted
					if (_currentBinderName == dbName)
					{
						await _currentBinder.CloseAsync().ConfigureAwait(false);
						if (DbNames.Count > 0)
						{
							CurrentBinderName = DbNames[0];
						}
						else
						{
							CurrentBinderName = string.Empty;
						}
					}
					return await Binder.DeleteClosedBinderAsync(dbName).ConfigureAwait(false);
				}
				return false;
			});
		}
		public Task<bool> BackupBinderAsync(string dbName, StorageFolder intoStorageFolder)
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				if (string.IsNullOrWhiteSpace(dbName) || !_dbNames.Contains(dbName) || intoStorageFolder == null) return false;

				//bool wasCurrentBinder = false;
				//// close the current binder if it is the one to be backed up
				//if (_currentBinderName == dbName)
				//{
				//	wasCurrentBinder = true;

				//	await _currentBinder.SetIsEnabledAsync(false).ConfigureAwait(false);
				//}
				bool isOk = await Binder.BackupDisabledBinderAsync(dbName, intoStorageFolder).ConfigureAwait(false);

				//if (wasCurrentBinder)
				//{
				//	await _currentBinder.SetIsEnabledAsync(true).ConfigureAwait(false);
				//}

				return isOk;
			});
		}
		public Task<bool> RestoreBinderAsync(StorageFolder fromStorageFolder)
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				if (fromStorageFolder == null) return false;

				// close the current binder if it is the one to be restored
				if (_currentBinderName == fromStorageFolder.Name)
				{
					await _currentBinder.CloseAsync().ConfigureAwait(false);
					if (DbNames.Count > 0)
					{
						CurrentBinderName = DbNames[0];
					}
					else
					{
						CurrentBinderName = string.Empty;
					}
				}
				if (await Binder.RestoreClosedBinderAsync(fromStorageFolder).ConfigureAwait(false))
				{
					return true;
				}
				return false;
			});
		}

		private Task UpdateCurrentBinderAsync()
		{
			return RunFunctionWhileOpenAsyncT(delegate { return UpdateCurrentBinder2Async(false); });
		}

		private async Task<bool> UpdateCurrentBinder2Async(bool openBinder)
		{
			if (string.IsNullOrEmpty(_currentBinderName))
			{
				await CloseCurrentBinderAsync().ConfigureAwait(false);
				RaisePropertyChanged_UI(nameof(CurrentBinder));
				return false;
			}
			else if ((_currentBinder == null && !string.IsNullOrEmpty(_currentBinderName))
				|| (_currentBinder != null && _currentBinder.DBName != _currentBinderName))
			{
				await CloseCurrentBinderAsync().ConfigureAwait(false);

				_currentBinder = Binder.CreateInstance(_currentBinderName);
				if (openBinder) await _currentBinder.OpenAsync().ConfigureAwait(false);
				RaisePropertyChanged_UI(nameof(CurrentBinder)); // notify the UI once the data has been loaded
				return true;
			}
			else if (_currentBinder != null)
			{
				if (openBinder) await _currentBinder.OpenAsync().ConfigureAwait(false);
				RaisePropertyChanged_UI(nameof(CurrentBinder));
				return true;
			}
			return false;
		}
		public async Task<bool> CloseCurrentBinderAsync()
		{
			var cb = _currentBinder;
			if (cb != null)
			{
				await cb.CloseAsync().ConfigureAwait(false);
				cb.Dispose();
				_currentBinder = null; // don't use CurrentBinder here, it triggers stuff
				return true;
			}
			return false;
		}
		#endregion loaded methods
		public Task<bool> IsNewDbNameWrongAsync(string newDbName)
		{
			return RunFunctionWhileOpenAsyncB(delegate { return IsNewDbNameWrong2(newDbName); });
		}
		private bool IsNewDbNameWrong2(string newDbName)
		{
			if (string.IsNullOrWhiteSpace(newDbName))
			{
				return true;
			}
			else
			{
				return _dbNames.Contains(newDbName);
			}
		}

		#region loading methods
		private const string FILENAME = "LolloSessionDataBriefcase.xml";

		private async Task LoadAsync()
		{
			string errorMessage = string.Empty;
			Briefcase newBriefcase = null;

			try
			{
				var file = await ApplicationData.Current.LocalFolder
					.CreateFileAsync(FILENAME, CreationCollisionOption.OpenIfExists)
					.AsTask().ConfigureAwait(false);

				//String ssss = null; //this is useful when you debug and want to see the file as a string
				//using (IInputStream inStream = await file.OpenSequentialReadAsync())
				//{
				//    using (StreamReader streamReader = new StreamReader(inStream.AsStreamForRead()))
				//    {
				//      ssss = streamReader.ReadToEnd();
				//    }
				//}

				using (IInputStream inStream = await file.OpenSequentialReadAsync().AsTask().ConfigureAwait(false))
				{
					using (var iinStream = inStream.AsStreamForRead())
					{
						DataContractSerializer serializer = new DataContractSerializer(typeof(Briefcase));
						iinStream.Position = 0;
						newBriefcase = (Briefcase)(serializer.ReadObject(iinStream));
						await iinStream.FlushAsync().ConfigureAwait(false);
					}
				}
			}
			catch (FileNotFoundException ex) //ignore file not found, this may be the first run just after installing
			{
				errorMessage = "starting afresh";
				await Logger.AddAsync(errorMessage + ex.ToString(), Logger.FileErrorLogFilename);
			}
			catch (Exception ex)                 //must be tolerant or the app might crash when starting
			{
				errorMessage = "could not restore the data, starting afresh";
				await Logger.AddAsync(errorMessage + ex.ToString(), Logger.FileErrorLogFilename);
			}
			if (string.IsNullOrWhiteSpace(errorMessage))
			{
				if (newBriefcase != null) CopyNonDbProperties(newBriefcase);
			}

			Debug.WriteLine("ended method Briefcase.LoadAsync()");
		}
		private async Task SaveAsync()
		{
			Briefcase briefcaseClone = CloneNonDbProperties();
			//for (int i = 0; i < 100000000; i++) //wait a few seconds, for testing
			//{
			//    String aaa = i.ToString();
			//}

			try
			{
				using (MemoryStream memoryStream = new MemoryStream())
				{
					DataContractSerializer sessionDataSerializer = new DataContractSerializer(typeof(Briefcase));
					sessionDataSerializer.WriteObject(memoryStream, briefcaseClone);

					var file = await ApplicationData.Current.LocalFolder
						.CreateFileAsync(FILENAME, CreationCollisionOption.ReplaceExisting)
						.AsTask().ConfigureAwait(false);
					using (Stream fileStream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
					{
						memoryStream.Seek(0, SeekOrigin.Begin);
						await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
						await memoryStream.FlushAsync().ConfigureAwait(false);
						await fileStream.FlushAsync().ConfigureAwait(false);
					}
				}
				Debug.WriteLine("ended method Briefcase.SaveAsync()");
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
		}
		private bool CopyNonDbProperties(Briefcase source)
		{
			if (source == null) return false;

			if (source.DbNames != null) DbNames = source.DbNames;
			//IsPaneOpen = source.IsPaneOpen;
			NewDbName = source.NewDbName;

			//IsShowingBinder = source.IsShowingBinder;
			//IsShowingBinderCover = source.IsShowingBinderCover;
			//IsShowingCover = source.IsShowingCover;
			//IsShowingSettings = source.IsShowingSettings;

			CurrentBinderName = source.CurrentBinderName; // must be last

			return true;
		}
		private Briefcase CloneNonDbProperties()
		{
			Briefcase target = new Briefcase();
			target.CurrentBinderName = CurrentBinderName;
			target.DbNames = DbNames;
			//target.IsPaneOpen = IsPaneOpen;

			//target.IsShowingBinder = IsShowingBinder;
			//target.IsShowingBinderCover = IsShowingBinderCover;
			//target.IsShowingCover = IsShowingCover;
			//target.IsShowingSettings = IsShowingSettings;

			target.NewDbName = NewDbName;
			return target;
		}
		#endregion loading methods
	}

	//public interface IPaneOpener
	//{
	//	//bool IsPaneOpen { get; set; }
	//	bool IsShowingSettings { get; set; }
	//	bool IsShowingCover { get; set; }
	//}
}

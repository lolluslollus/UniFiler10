using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using UniFiler10.Data.Runtime;
using Utilz;
using Windows.ApplicationModel.Resources.Core;
using Windows.Storage;
using Windows.Storage.Streams;

namespace UniFiler10.Data.Metadata
{
	[DataContract]
	public sealed class MetaBriefcase : OpenableObservableData
	{
		#region properties
		private static volatile MetaBriefcase _instance = null;
		[IgnoreDataMember]
		public static MetaBriefcase OpenInstance { get { var instance = _instance; if (instance != null && instance._isOpen) return instance; else return null; } }

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
				///CurrentFieldDescription = _currentCategory.FieldDescriptions.FirstOrDefault(fd => fd.Id == _currentFieldDescriptionId);
			}
			else
			{
				CurrentFieldDescription = null;
			}
		}

		private SwitchableObservableCollection<Category> _categories = new SwitchableObservableCollection<Category>();
		[DataMember]
		public SwitchableObservableCollection<Category> Categories { get { return _categories; } private set { _categories = value; RaisePropertyChanged_UI(); } }

		private SwitchableObservableCollection<FieldDescription> _fieldDescriptions = new SwitchableObservableCollection<FieldDescription>();
		[DataMember]
		public SwitchableObservableCollection<FieldDescription> FieldDescriptions { get { return _fieldDescriptions; } private set { _fieldDescriptions = value; RaisePropertyChanged_UI(); } }

		private bool _isElevated = false;
		[DataMember]
		public bool IsElevated { get { return _isElevated; } set { _isElevated = value; RaisePropertyChanged_UI(); } }
		#endregion properties


		#region construct and dispose
		private static readonly object _instanceLock = new object();
		internal static MetaBriefcase GetCreateInstance()
		{
			lock (_instanceLock)
			{
				if (_instance == null || _instance._isDisposed)
				{
					_instance = new MetaBriefcase();
				}
				return _instance;
			}
		}

		private MetaBriefcase() { }

		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_categories?.Dispose();
			_categories = null;

			_fieldDescriptions?.Dispose();
			_fieldDescriptions = null;
		}
		#endregion construct and dispose


		#region open and close
		protected override async Task OpenMayOverrideAsync()
		{
			await LoadAsync().ConfigureAwait(false);
		}

		protected override async Task CloseMayOverrideAsync()
		{
			await SaveAsync().ConfigureAwait(false);

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
		#endregion open and close


		#region loading methods
		public const string FILENAME = "LolloSessionDataMetaBriefcase.xml";
		private StorageFile _sourceFile = null;
		public void SetSourceFileJustOnce(StorageFile sourceFile)
		{
			_sourceFile = sourceFile;
		}

		private async Task LoadAsync()
		{
			string errorMessage = string.Empty;
			MetaBriefcase newMetaBriefcase = null;

			try
			{
				StorageFile file = _sourceFile;
				if (file == null)
				{
					file = await GetDirectory()
						.CreateFileAsync(FILENAME, CreationCollisionOption.OpenIfExists)
						.AsTask().ConfigureAwait(false);
				}
				_sourceFile = null;
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
						DataContractSerializer serializer = new DataContractSerializer(typeof(MetaBriefcase));
						iinStream.Position = 0;
						newMetaBriefcase = (MetaBriefcase)(serializer.ReadObject(iinStream));
						await iinStream.FlushAsync().ConfigureAwait(false);
					}
				}
			}
			catch (FileNotFoundException ex) //ignore file not found, this may be the first run just after installing
			{
				errorMessage = "starting afresh";
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename);
			}
			catch (Exception ex)                 //must be tolerant or the app might crash when starting
			{
				errorMessage = "could not restore the data, starting afresh";
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename);
			}
			if (string.IsNullOrWhiteSpace(errorMessage))
			{
				if (newMetaBriefcase != null) CopyFrom(newMetaBriefcase);
			}

			Debug.WriteLine("ended method MetaBriefcase.LoadAsync()");
		}
		private async Task<bool> SaveAsync(StorageFile file = null)
		{
			//for (int i = 0; i < 100000000; i++) //wait a few seconds, for testing
			//{
			//    String aaa = i.ToString();
			//}
			await Logger.AddAsync("MetaBriefcase about to save", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);

			try
			{
				if (file == null)
				{
					file = await GetDirectory()
						.CreateFileAsync(FILENAME, CreationCollisionOption.ReplaceExisting)
						.AsTask().ConfigureAwait(false);
				}

				using (MemoryStream memoryStream = new MemoryStream())
				{
					DataContractSerializer sessionDataSerializer = new DataContractSerializer(typeof(MetaBriefcase));
					sessionDataSerializer.WriteObject(memoryStream, this);

					using (Stream fileStream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
					{
						memoryStream.Seek(0, SeekOrigin.Begin);
						await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
						await memoryStream.FlushAsync().ConfigureAwait(false);
						await fileStream.FlushAsync().ConfigureAwait(false);
					}
				}
				Debug.WriteLine("ended method MetaBriefcase.SaveAsync()");
				return true;
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
			return false;
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
		private StorageFolder GetDirectory()
		{
			return ApplicationData.Current.RoamingFolder; // was LocalFolder
		}
		#endregion loading methods


		#region while open methods
		public Task SetCurrentCategoryAsync(Category cat)
		{
			return RunFunctionWhileOpenAsyncA(delegate
			{
				if (cat != null)
				{
					CurrentCategoryId = cat.Id;
				}
			});
		}

		public Task SetCurrentFieldDescriptionAsync(FieldDescription fldDsc)
		{
			return RunFunctionWhileOpenAsyncA(delegate
			{
				if (fldDsc != null)
				{
					CurrentFieldDescriptionId = fldDsc.Id;
				}
			});
		}

		public Task SetIsElevatedAsync(bool newValue)
		{
			return RunFunctionWhileOpenAsyncA(delegate { IsElevated = newValue; });
		}

		public Task<bool> AddCategoryAsync()
		{
			return RunFunctionWhileOpenAsyncB(delegate
			{
				string name = RuntimeData.GetText("NewCategory");
				var newCat = new Category() { Name = name, IsCustom = true, IsJustAdded = true };

				if (Category.Check(newCat) && !Categories.Any(cat => cat.Name == newCat.Name || cat.Id == newCat.Id))
				{
					_categories.Add(newCat);
					return true;
				}
				return false;
			});
		}
		public Task<bool> RemoveCategoryAsync(Category cat)
		{
			return RunFunctionWhileOpenAsyncB(delegate
			{
				if (cat != null && (cat.IsJustAdded || _isElevated))
				{
					return _categories.Remove(cat);
				}
				else
				{
					return false;
				}
			});
		}

		public Task<bool> AddFieldDescriptionAsync()
		{
			return RunFunctionWhileOpenAsyncB(delegate
			{
				string name = RuntimeData.GetText("NewFieldDescription");
				var newFieldDesc = new FieldDescription() { Caption = name, IsCustom = true, IsJustAdded = true };

				if (FieldDescription.Check(newFieldDesc) && !_fieldDescriptions.Any(fd => fd.Caption == newFieldDesc.Caption || fd.Id == newFieldDesc.Id))
				{
					_fieldDescriptions.Add(newFieldDesc);
					return true;
				}
				return false;
			});
		}

		public Task<bool> RemoveFieldDescriptionAsync(FieldDescription fldDesc)
		{
			return RunFunctionWhileOpenAsyncB(delegate
			{
				if (fldDesc != null && (fldDesc.IsJustAdded || _isElevated))
				{
					foreach (var cat in _categories)
					{
						cat.RemoveFieldDescription(fldDesc);
					}
					return FieldDescriptions.Remove(fldDesc);
				}
				else return false;
			});
		}

		public Task<bool> AddPossibleValueToCurrentFieldDescriptionAsync()
		{
			return RunFunctionWhileOpenAsyncB(delegate
			{
				if (_currentFieldDescription == null) return false;

				string name = RuntimeData.GetText("NewFieldValue");
				var newFldVal = new FieldValue() { Vaalue = name, IsCustom = true, IsJustAdded = true };

				return _currentFieldDescription.AddPossibleValue(newFldVal);
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
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				if (fldDsc == null || newFldVal == null) return false;

				bool isOk = fldDsc.AddPossibleValue(newFldVal);
				if (isOk && save) isOk = await SaveAsync().ConfigureAwait(false);
				return isOk;
			});
		}

		public Task<bool> RemovePossibleValueFromCurrentFieldDescriptionAsync(FieldValue fldVal)
		{
			return RunFunctionWhileOpenAsyncB(delegate
			{
				if (fldVal == null || _currentFieldDescription == null || (!fldVal.IsJustAdded && !_isElevated)) return false;

				return _currentFieldDescription.RemovePossibleValue(fldVal);
			});
		}

		public Task<bool> AssignFieldDescriptionToCurrentCategoryAsync(FieldDescription fldDsc)
		{
			return RunFunctionWhileOpenAsyncB(delegate
			{
				if (fldDsc == null || _currentCategory == null) return false;

				return _currentCategory.AddFieldDescription(fldDsc);
			});
		}

		public Task<bool> UnassignFieldDescriptionFromCurrentCategoryAsync(FieldDescription fldDsc)
		{
			return RunFunctionWhileOpenAsyncB(delegate
			{
				if (fldDsc == null || _currentCategory == null || (!fldDsc.JustAssignedToCats.Contains(_currentCategoryId) && !_isElevated)) return false;

				return _currentCategory.RemoveFieldDescription(fldDsc);
			});
		}

		public Task<bool> SaveACopyAsync(StorageFile file)
		{
			return RunFunctionWhileOpenAsyncTB(delegate { return SaveAsync(file); });
			//return SaveAsync(file);
		}

		public Task<bool> SaveACopyAsync()
		{
			return RunFunctionWhileOpenAsyncTB(delegate { return SaveAsync(); });
		}
		#endregion while open methods
	}
}

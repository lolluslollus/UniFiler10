﻿using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using Utilz;
using Utilz.Data;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public class Document : DbBoundObservableData
	{
		#region lifecycle
		public Document() { }
		public Document(DBManager dbManager, string parentId) : base()
		{
			DBManager = dbManager;
			ParentId = parentId;
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_dbManager = null;
		}
		#endregion lifecycle


		#region properties
		private readonly object _dbManagerLocker = new object();
		private DBManager _dbManager = null;
		[IgnoreDataMember]
		[Ignore]
		public DBManager DBManager { get { lock (_dbManagerLocker) { return _dbManager; } } set { lock (_dbManagerLocker) { _dbManager = value; } } }

		private readonly object _uri0Locker = new object();
		private string _uri0 = string.Empty;
		[DataMember]
		public string Uri0
		{
			get
			{
				return GetPropertyLocking(ref _uri0, _uri0Locker);
			}
			set // this lockless setter is only for the serialiser and the db
			{
				string newValue = value == null ? string.Empty : Path.GetFileName(value);
				SetPropertyUpdatingDb(ref _uri0, newValue, false);
			}
		}
		public void SetUri0(string newValue)
		{
			string okValue = newValue == null ? string.Empty : Path.GetFileName(newValue);
			SetPropertyLockingUpdatingDb(ref _uri0, okValue, _uri0Locker);
		}
		public string GetFullUri0()
		{
			if (string.IsNullOrWhiteSpace(Uri0)) return string.Empty;
			else
			{
				var dbM = DBManager;
				if (dbM != null)
				{
					return Path.Combine(dbM.Directory.Path, Uri0);
				}
				else
				{
					return string.Empty;
				}
			}
		}
		public string GetFullUri0(StorageFolder directory)
		{
			if (string.IsNullOrWhiteSpace(Uri0) || directory == null) return string.Empty;
			else return Path.Combine(directory.Path, Uri0);
		}
		#endregion properties


		protected override bool UpdateDbMustOverride()
		{
			return DBManager?.UpdateDocuments(this) == true;
		}

		//protected override bool IsEqualToMustOverride(DbBoundObservableData that)
		//{
		//	var target = that as Document;

		//	return _parentId == that._parentId && // I don't want it for the folder, but I want it for the smaller objects
		//		_uri0 == target._uri0;
		//}

		protected override bool CheckMeMustOverride()
		{
			return _id != DEFAULT_ID && _parentId != DEFAULT_ID;
		}


		#region while open methods
		public Task<bool> RemoveContentAsync()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				try
				{
					if (!string.IsNullOrWhiteSpace(Uri0))
					{
						var file = await StorageFile.GetFileFromPathAsync(GetFullUri0()).AsTask().ConfigureAwait(false);
						if (file != null) await file.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
					}
					return true;
				}
				catch (Exception ex)
				{
					await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
				}
				return false;
			});
		}
		public async Task<List<string>> GetTextFromPictureAsync()
		{
			string uri = GetFullUri0();
			if (string.IsNullOrWhiteSpace(uri) || !DocumentExtensions.IMAGE_EXTENSIONS.Contains(Path.GetExtension(uri).ToLower())) return null;

			var file = await StorageFile.GetFileFromPathAsync(uri).AsTask().ConfigureAwait(false);
			SoftwareBitmap bitmap = null;
			using (var stream = await file.OpenAsync(FileAccessMode.Read))
			{
				var decoder = await BitmapDecoder.CreateAsync(stream);
				bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
			}
			if (bitmap.PixelWidth > OcrEngine.MaxImageDimension || bitmap.PixelHeight > OcrEngine.MaxImageDimension) return null;

			var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
			if (ocrEngine == null) return null;

			var ocrResult = await ocrEngine.RecognizeAsync(bitmap).AsTask().ConfigureAwait(false);
			if (ocrResult == null) return null;

			var result = new List<string>();
			foreach (var line in ocrResult.Lines)
			{
				result.Add(line.Text);
			}
			return result;
		}
		#endregion while open methods
	}
}

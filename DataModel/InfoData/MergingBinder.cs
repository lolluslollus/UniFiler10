using System;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using Windows.Storage;

namespace UniFiler10.Data.Model
{
	public sealed class MergingBinder : Binder
	{
		#region ctor
		private static readonly object _instanceLocker = new object();
		public static MergingBinder CreateInstance(string dbName, StorageFolder directory)
		{
			lock (_instanceLocker)
			{
				if (_instance == null || _instance._isDisposed)
				{
					_instance = new MergingBinder(dbName, directory);
				}
				return _instance;
			}
		}
		private MergingBinder(string dbName, StorageFolder directory) : base(dbName)
		{
			if (directory == null) throw new ArgumentException("MergingBinder ctor: directory cannot be null or empty");
			_directory = directory;
		}
		#endregion ctor


		#region open and close
		protected override async Task OpenMayOverrideAsync()
		{
			_dbManager = new DBManager(_directory, false);
			await _dbManager.OpenAsync().ConfigureAwait(false);

			await LoadNonDbPropertiesAsync().ConfigureAwait(false);
			await LoadFoldersWithoutContentAsync().ConfigureAwait(false);
		}
		protected override async Task CloseMayOverrideAsync()
		{
			var dbM = _dbManager;
			if (dbM != null)
			{
				await dbM.CloseAsync().ConfigureAwait(false);
				dbM.Dispose();
			}
			_dbManager = null;

			await RunInUiThreadAsync(delegate
			{
				_folders.Clear();
			}).ConfigureAwait(false);
		}
		#endregion open and close


		#region properties
		private static MergingBinder _instance = null;
		#endregion properties
	}
}

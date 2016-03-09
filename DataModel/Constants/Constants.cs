using Windows.ApplicationModel;

namespace UniFiler10.Data.Constants
{
	public static class ConstantData
	{
		public const string DATE_TIME_FORMAT = "yyyy-MM-ddTHH:mm:ssZ";
		public const string MYMAIL = "lollus@hotmail.co.uk";
		public const string APPNAME = "Paper Mate for Windows 10";
		public const string APPNAME_ALL_IN_ONE = "Paper Mate";
		public const string ASSEMBLY_NAME = "Unifiler10";
		public const string VIEWS_NAMESPACE = "UniFiler10.Views.";
		public const string XML_EXTENSION = ".xml";
		public const string DB_EXTENSION = ".db";
		public const string BUY_URI = @"ms-windows-store://pdp/?ProductId=9nblggh6g8vv"; // LOLLO TODO this id comes from the dashboard
		public const string RATE_URI = @"ms-windows-store://review/?ProductId=9nblggh6g8vv"; // LOLLO TODO this id comes from the dashboard
		public const int TRIAL_LENGTH_DAYS = 7;

		public const string ClientID = "000000004817C625";
		//Client secret:   KoXu5wZMz5e8GK1TWY1wMMsq8wgTA6GR

		public const ulong MAX_IMPORTABLE_MEDIA_FILE_SIZE = 100000000;

		public const string REG_EXPORT_BINDER_IS_EXPORTING = "ExportBinder.IsExporting";
		public const string REG_EXPORT_BINDER_DBNAME = "ExportBinder.DbName";
		public const string REG_IMPORT_BINDER_IS_IMPORTING = "ImportBinder.IsImporting";
		//public const string REG_IMPORT_BINDER_STEP = "ImportBinder.Step";
		//public const string REG_IMPORT_BINDER_STEP2_ACTION = "ImportBinder.Step2.Action";

		public const string REG_IMPORT_FOLDERS_IS_IMPORTING = "ImportFolders.IsImporting";

		// public const string DEFAULT_AUDIO_FILE_NAME = "Audio.mp3"; // LOLLO NOTE this fails with the phone, wav is good
		public const string DEFAULT_AUDIO_FILE_NAME = "Audio.wav";
		public const string DEFAULT_PHOTO_FILE_NAME = "Photo.jpg";

		public const string REG_IMPORT_MEDIA_IS_IMPORTING = "ImportMedia.IsImporting";
		public const string REG_IMPORT_MEDIA_FOLDERID = "ImportMedia.FolderId";
		public const string REG_IMPORT_MEDIA_PARENTWALLETID = "ImportMedia.ParentWalletId";
		public const string REG_IMPORT_MEDIA_IS_SHOOTING = "ImportMedia.IsShooting";

		public const string REG_SETTINGS_IS_EXPORTING = "ImpExpSettings.IsExporting";
		public const string REG_SETTINGS_IS_IMPORTING = "ImpExpSettings.IsImporting";

		public const string REG_BRIEFCASE = "Briefcase.LastSave";

		public const string ODU_BACKGROUND_TASK_NAME = "BackgroundOneDriveUploader";
		public const string ODU_BACKGROUND_TASK_ENTRY_POINT = "BackgroundTasks.BackgroundOneDriveUploader";
		public const string ODU_BACKGROUND_TASK_ALLOWED_INSTANCE_ID = "BackgroundOneDriveUploader.TaskInstanceId";

		public const string REG_MBC_ODU_LOCAL_SYNCED_SINCE_OPEN = "MetaBriefcase.IsLocalSyncedOnceSinceLastOpen";
		public const string REG_MBC_ODU_TKN = "MetaBriefcase.OneDriveAccessToken";
		public const string REG_MBC_IS_ELEVATED = "MetaBriefcase.IsElevated";
		public const string REG_MBC_LAST_TIME_UPDATE_ONEDRIVE_CALLED = "MetaBriefcase.LastTimeUpdateOneDriveCalled";
		public const string REG_MBC_LAST_TIME_UPDATE_ONEDRIVE_RAN = "MetaBriefcase.LastTimeUpdateOneDriveRan";
		public const string REG_MBC_LAST_TIME_PULL_ONEDRIVE_RAN = "MetaBriefcase.LastTimePullOneDriveRan";
		//public const string REG_MBC_IS_LOAD_FROM_ONE_DRIVE = "MetaBriefcase.IsLoadFromOneDrive";

		public static string AppName { get { return APPNAME; } }
		private static readonly string _version = Package.Current.Id.Version.Major.ToString()
			+ "."
			+ Package.Current.Id.Version.Minor.ToString()
			+ "."
			+ Package.Current.Id.Version.Build.ToString()
			+ "."
			+ Package.Current.Id.Version.Revision.ToString();
		public static string Version { get { return "Version " + _version; } }
	}
}

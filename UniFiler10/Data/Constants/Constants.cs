using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

		//public const string BAK_EXPORT_BINDER_TEMP_DIR = "ExportBinderDir";
		public const string REG_EXPORT_BINDER_CONTINUE_EXPORTING = "ExportBinder.ContinueExport";

		//public const string TEMP_DIR_4_IMPORT_FOLDERS = "ImportFoldersDir";
		////public const string REG_IMPORT_FOLDERS_SOURCE_BINDER_NAME = "ImportFolders.SourceBinderBinder.Name";
		//public const string REG_IMPORT_FOLDERS_BINDER_NAME = "ImportFolders.Binder.Name";
		//public const string REG_IMPORT_FOLDERS_DIR_PATH = "ImportFolders.Dir.Path";
		public const string REG_IMPORT_FOLDERS_IS_IMPORTING = "ImportFolders.IsImporting";
		//public const string REG_IMPORT_FOLDERS_CONTINUE_IMPORTING = "ImportFolders.ContinueImport";

		// public const string DEFAULT_AUDIO_FILE_NAME = "Audio.mp3"; // LOLLO NOTE this fails with the phone, wav is good
		public const string DEFAULT_AUDIO_FILE_NAME = "Audio.wav";
		public const string DEFAULT_PHOTO_FILE_NAME = "Photo.jpg";

		public const string REG_FP_CONTINUE_IMPORTING = "FilePicker.ContinueImporting";
		public const string REG_FP_FOLDERID = "FilePicker.FolderId";
		public const string REG_FP_PARENTWALLETID = "FilePicker.ParentWalletId";
		public const string REG_FP_FILEPATH = "FilePicker.FilePath";

		//public const string REG_SHOOT_FOLDERID = "ShootUi.FolderId";
		//public const string REG_SHOOT_PARENTWALLET = "ShootUi.ParentWallet";
		//public const string REG_SHOOT_FILEPATH = "ShootUi.FilePath";

		//public const string REG_IMPORT_SETTINGS_FILEPATH = "SettingsFilePicker.ImportFilePath";
		public const string REG_SETTINGS_IS_EXPORTING = "ImpExpSettings.IsExporting";
		public const string REG_SETTINGS_IS_IMPORTING = "ImpExpSettings.IsImporting";
		//public const string REG_IMPEXP_SETTINGS_CONTINUE_EXPORTING = "ImpExpSettings.ContinueExport";
		//public const string REG_IMPEXP_SETTINGS_CONTINUE_IMPORTING = "ImpExpSettings.ContinueImport";

		public const string REG_IMPORT_MERGE_BINDER_STEP1_CONTINUE = "ImportMergeBinder.Step1.Continue";
		public const string REG_IMPORT_BINDER_STEP2_CONTINUE = "ImportBinder.Step2.Continue";
		public const string REG_MERGE_BINDER_STEP2_CONTINUE = "MergeBinder.Step2.Continue";

		public static string AppName { get { return ConstantData.APPNAME; } }
        private static string _version = Package.Current.Id.Version.Major.ToString()
            + "."
            + Package.Current.Id.Version.Minor.ToString()
            + "."
            + Package.Current.Id.Version.Build.ToString()
            + "."
            + Package.Current.Id.Version.Revision.ToString();
        public static string Version { get { return "Version " + _version; } }
    }
}

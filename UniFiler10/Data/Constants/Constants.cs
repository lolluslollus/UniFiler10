﻿using System;
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

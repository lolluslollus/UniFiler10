﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace UniFiler10.Utilz
{
    public static class ConstantData
    {
        public const string DATE_TIME_FORMAT = "yyyy-MM-ddTHH:mm:ssZ";
        public const string MYMAIL = "lollus@hotmail.co.uk";
        public const string APPNAME = "PapierKrieger for Windows 10";
        public const string APPNAME_ALL_IN_ONE = "PapierKrieger";
        //public const string BUY_URI = @"ms-windows-store://pdp/?ProductId=9NBLGGH1Z7LM"; // this id comes from the dashboard
        //public const string RATE_URI = @"ms-windows-store://review/?ProductId=9NBLGGH1Z7LM"; // this id comes from the dashboard
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

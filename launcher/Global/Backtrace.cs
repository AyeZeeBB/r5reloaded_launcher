﻿using Backtrace.Model;
using Backtrace;
using static launcher.Global.Logger;
using System.Globalization;
using System.IO;

namespace launcher.Global
{
    public static class Backtrace
    {
        public static BacktraceCredentials Credentials = new(@"https://submit.backtrace.io/r5rlauncher/6193e7e11129f7cd24cba1c1388f4a4649c30b0d07940a25896171ff162902e5/json");
        public static BacktraceClient Client = new(Credentials);

        public static void Send(Exception exception, LogSource source)
        {
            if (AppState.IsOnline && (bool)Ini.Get(Ini.Vars.Upload_Crashes))
            {
                //BacktraceReport report = new(exception);
                //report.Attributes.Add("launcher.version", Launcher.VERSION);
                //report.Attributes.Add("log.source", Enum.GetName(typeof(LogSource), LogSource).ToUpper(new CultureInfo("en-US")));

                //if (File.Exists(Path.Combine(Launcher.PATH, "launcher_data\\cfg\\launcherConfig.ini")))
                //    report.AttachmentPaths.Add(Path.Combine(Launcher.PATH, "launcher_data\\cfg\\launcherConfig.ini"));

                //if (File.Exists(LogFilePath))
                //    report.AttachmentPaths.Add(LogFilePath);

                //Client.Send(report);
            }
        }
    }
}
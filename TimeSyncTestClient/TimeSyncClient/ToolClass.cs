using System;
using System.IO;

namespace TimeSyncClient
{
    class ToolClass
    {
        private static string c_isWriteLog = "1";
        /// <summary>
        /// 表示是否记录日志0：不记录日志，1：记录日志
        /// </summary>
        public static string C_isWriteLog
        {
            get { return c_isWriteLog; }
            set { c_isWriteLog = value; }
        }
        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="txt">日志内容</param>
        public static void applogs(string txt)
        {
            try
            {
                if (C_isWriteLog == "1")
                {
                    txt = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "]:" + txt + "\r\n";
                    File.AppendAllText(@"ntpdate_" + DateTime.Now.ToString("yyyyMMdd") + ".log", txt);
                }
            }
            catch
            {

            }
        }
    }
}

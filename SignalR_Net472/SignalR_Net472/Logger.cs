using System;
using System.IO;

namespace SignalRNet472
{
    public static class Logger
    {
        public static string GetLogFileName()
        {
            DateTime dt = DateTime.Now;
            string logFileName = string.Format("{0}{1:D2}{2:D2}_Log.txt", dt.Year, dt.Month, dt.Day);
            string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogData");

            string folderPath = string.Format("{0}\\{1}{2:D2}", dataPath, dt.Year, dt.Month);
            Directory.CreateDirectory(folderPath);
            string logfilePath = string.Format("{0}\\{1}", folderPath, logFileName);

            return logfilePath;
        }

        public static void WriteErrorLog(string msg, string logpath)
        {
            string logMsg = string.Format("[{0}] {1} ------------------- <Error Reported> ------------------.", DateTime.Now, msg);

            StreamWriter logStream = null;
            try
            {
                if (File.Exists(logpath))
                    logStream = File.AppendText(logpath);
                else
                    logStream = new StreamWriter(logpath);

                logStream.WriteLine(logMsg);
                logStream.Flush();
            }
            catch (Exception)
            {
            }
            finally
            {
                logStream?.Close();
            }
        }

        public static void WriteLog(string msg, string logpath)
        {
            string logMsg = string.Format("[{0}] {1}", DateTime.Now, msg);

            StreamWriter logStream = null;
            try
            {
                if (!File.Exists(logpath))
                {
                    logStream = new StreamWriter(logpath);
                }
                else
                {
                    logStream = File.AppendText(logpath);
                }

                logStream.WriteLine(logMsg);
                logStream.Flush();
            }
            catch (IOException)
            {
            }
            finally
            {
                logStream?.Close();
            }
        }
    }
}

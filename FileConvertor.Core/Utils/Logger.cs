using System;
using System.IO;
using System.Text;

namespace FileConvertor.Core.Utils
{
    /// <summary>
    /// 简单文件日志器（线程安全）
    ///
    /// 四个等级：Debug / Info / Warning / Error
    /// 日志文件：程序运行目录下 logs\yyyy-MM-dd.log
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string _logDirectory;
        private static string _currentDate;
        private static string _currentLogPath;

        /// <summary>
        /// 日志目录（默认：程序基目录\logs）
        /// </summary>
        public static string LogDirectory
        {
            get
            {
                if (_logDirectory == null)
                {
                    _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                }
                return _logDirectory;
            }
            set { _logDirectory = value; }
        }

        /// <summary>
        /// 获取当日日志文件路径
        /// </summary>
        private static string GetLogPath()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (_currentDate != today)
            {
                _currentDate = today;
                _currentLogPath = Path.Combine(LogDirectory, $"{today}.log");
                Directory.CreateDirectory(LogDirectory);
            }
            return _currentLogPath;
        }

        /// <summary>
        /// 写一条日志
        /// </summary>
        public static void Log(LogLevel level, string message, Exception exception = null)
        {
#if DEBUG
            // Debug 等级仅在 Debug 配置下输出到文件
            if (level == LogLevel.Debug)
            {
                WriteLine(level, message, exception);
                return;
            }
#endif
            if (level == LogLevel.Debug)
                return;

            WriteLine(level, message, exception);
        }

        private static void WriteLine(LogLevel level, string message, Exception exception)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendFormat("[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1,-7}] ", DateTime.Now, level);
                sb.Append(message);
                if (exception != null)
                {
                    sb.AppendLine();
                    sb.Append("    异常: ").Append(exception.GetType().Name).Append(" - ").Append(exception.Message);
                    if (exception.StackTrace != null)
                    {
                        sb.AppendLine();
                        sb.Append("    堆栈: ").Append(exception.StackTrace);
                    }
                }

                lock (_lock)
                {
                    File.AppendAllText(GetLogPath(), sb.ToString() + Environment.NewLine);
                }
            }
            catch
            {
                // 日志失败不能影响业务
            }
        }

        public static void Debug(string message) => Log(LogLevel.Debug, message);
        public static void Info(string message) => Log(LogLevel.Info, message);
        public static void Warning(string message, Exception ex = null) => Log(LogLevel.Warning, message, ex);
        public static void Error(string message, Exception ex = null) => Log(LogLevel.Error, message, ex);
    }
}

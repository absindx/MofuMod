namespace MofuMod {
	public static class Logger {
		private const int LevelLength	= 5;

		private static Action<string>	writeFunction	= Console.WriteLine;
		private static bool		enableDebugLog	= false;

		public static void	SetWriteFunction(Action<string> writeFunction) {
			Logger.writeFunction	= writeFunction;
		}
		public static void	SetEnableDebugLog(bool enableDebugLog) {
			Logger.enableDebugLog	= enableDebugLog;
		}

		private static void WriteLog(string levelName, string format, params object[] args) {
			string	timeString	= DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
			string	message		= string.Format(format, args);
			string	logMessage	= $"{timeString} [{levelName.PadRight(LevelLength, ' ')}] {message}";
			writeFunction(logMessage);
		}

		public static void	Debug(string message, params object[] args) {
			if(enableDebugLog) {
				WriteLog("DEBUG", message, args);
			}
		}
		public static void	Information(string message, params object[] args) {
			WriteLog("INFO",  message, args);
		}
		public static void	Warning(string message, params object[] args) {
			WriteLog("WARN",  message, args);
		}
		public static void	Error(string message, params object[] args) {
			WriteLog("ERROR", message, args);
		}
		public static void	Exception(Exception exception, string message, params object[] args) {
			message		= string.Format(message, args);
			WriteLog("ERROR", message + "\n" + exception.ToString());
		}
	}
}

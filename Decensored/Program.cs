using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace MofuMod {
	internal class Program {
		private static void	Main(string[] args) {
			// set logger
			#if DEBUG
			Logger.SetEnableDebugLog(true);
			#endif

			// check arguments count
			bool	validArgumentsCount	= args.Length >= 1;
			ShowCliHeader(!validArgumentsCount);
			if(!validArgumentsCount) {
				return;
			}

			// get module
			string			modulePath	= args[0];
			AbstructPatcher?	patcher		= DecensoredPatcher.GetModule(modulePath);
			if(patcher == null) {
				return;
			}

			// backup
			if(!BackupModule(patcher.ModulePath, "_original")) {
				return;
			}

			// check patched
			if(patcher.CheckPatched()) {
				Logger.Warning("This module has already been patched. It may not be patched correctly.");
			}

			// patch
			bool	patched	= patcher.PatchAll();
			if(!patched) {
				return;
			}

			// save file
			string	targetModule	= patcher.ModulePath;
			string?	saveModule	= AddSuffixToFilename(targetModule, "_patched");
			if(saveModule == null) {
				Logger.Error("Failed to get save file name.");
				return;
			}
			if(!patcher.SaveModule(saveModule)) {
				return;
			}
			Logger.Information("Saved the patched module. (\"{0}\")", saveModule);
			patcher.Dispose();
			patcher	= null;

			// swap file
			if(!OverwriteModule(targetModule, saveModule)) {
				return;
			}
			Logger.Information("Overwrites the original module. (\"{0}\")", targetModule);

			Logger.Information("All completed successfully!");
		}

		private static void	ShowCliHeader(bool showUsage) {
			string	assemblyName	= "?";
			string	executeFile	= "?";
			string	version		= "?";

			try {
				Assembly	assembly	= Assembly.GetExecutingAssembly();
				assemblyName			= assembly.GetName().Name ?? assemblyName;
				string?		exePath		= System.Environment.ProcessPath;
				if(!string.IsNullOrEmpty(exePath)) {
					executeFile		= System.IO.Path.GetFileName(exePath);
					version			= FileVersionInfo.GetVersionInfo(exePath).FileVersion ?? version;
				}
			}
			catch(Exception e) {
				Logger.Warning("Failed to get assembly information.");
				Logger.Exception(e, "Failed to get assembly information.");
			}

			Console.WriteLine($"--------------------------------------------------");
			Console.WriteLine($"{assemblyName} Version {version}");
			Console.WriteLine($"--------------------------------------------------");

			if(!showUsage) {
				return;
			}

			Console.WriteLine($"usage: {executeFile} <target>");
			Console.WriteLine($"    <target> = Assembly-CSharp.dll");
		}

		private static string?	AddSuffixToFilename(string filePath, string suffix) {
			try {
				string	directory	= Path.GetDirectoryName(filePath) ?? string.Empty;
				string	baseFilename	= Path.GetFileNameWithoutExtension(filePath);
				string	extension	= Path.GetExtension(filePath);
				string	addedFilename	= baseFilename + suffix + extension;
				string	addedFilepath	= Path.Combine(directory, addedFilename);
				return addedFilename;
			}
			catch {
				return null;
			}
		}

		private static bool	BackupModule(string modulePath, string filenameSuffix) {
			string?	backupPath	= AddSuffixToFilename(modulePath, filenameSuffix);
			if (backupPath == null) {
				return false;
			}

			try {
				if(!File.Exists(backupPath)) {
					Logger.Information("Copy the module as a backup. (\"{0}\")", backupPath);
					File.Copy(modulePath, backupPath);
				}
				else {
					Logger.Warning("Backup file already exists, skipping backup. (\"{0}\")", backupPath);
				}
				return true;
			}
			catch(Exception e) {
				Logger.Exception(e, "Failed to backup the module. (\"{0}\")", modulePath);
				return false;
			}
		}
		private static bool	OverwriteModule(string targetModule, string overwriteModule) {
			try {
				File.Copy(overwriteModule, targetModule, true);
				return true;
			}
			catch(Exception e) {
				Logger.Exception(e, "Failed to overwrite the module. (\"{0}\")", targetModule);
				return false;
			}
		}
	}
}

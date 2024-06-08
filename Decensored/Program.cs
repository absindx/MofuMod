using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

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

	public abstract class DecensoredPatcher : AbstructPatcher {
		protected DecensoredPatcher(ModuleDefinition module, string modulePath) : base(module, modulePath){
		}

		override protected Func<bool>[]	GetPatchList() {
			Func<bool>[]	patchList = {
				this.PatchDecensored,
			};

			return patchList;
		}

		abstract protected string	GetPatchName();
		abstract public bool		CheckTargetModule();
		abstract protected bool		PatchDecensored();

		public static DecensoredPatcher?	GetModule(string modulePath) {
			var	supportModuleList	= new (string, Func<string, DecensoredPatcher?>)[]{
				("CF", CF.Patcher.ReadModule),
				("FV", FV.Patcher.ReadModule),
			};

			foreach (var moduleReader in supportModuleList){
				string			name	= moduleReader.Item1;
				DecensoredPatcher?	patcher	= moduleReader.Item2(modulePath);
				bool			result	= (patcher != null) && (patcher.CheckTargetModule());
				Logger.Debug("Check module: {0} = {1}", name, result);

				if(result) {
					return patcher;
				}
			}
			return null;
		}
	}

	namespace CF {
		public class Patcher : DecensoredPatcher {
			protected Patcher(ModuleDefinition module, string modulePath) : base(module, modulePath){
			}

			internal static Patcher?	ReadModule(string modulePath) {
				try {
					ModuleDefinition	module	= ModuleDefinition.ReadModule(modulePath);
					return new Patcher(module, modulePath);
				}
				catch(Exception e){
					Logger.Exception(e, "Failed to open the module. (\"{0}\")", modulePath);
					return null;
				}
			}

			override protected string	GetPatchName() {
				return "CF.Decensored";
			}

			override public bool CheckTargetModule() {
				// -.TM.SetMSC()
				MethodDefinition?	method		= this.GetMethod("", "TM", "SetMSC");
				return method != null;
			}

			override protected bool	PatchDecensored() {
				string	patchName	= this.GetPatchName();
				try {
					// -.TM.SetMSC()
					MethodDefinition?	method		= this.GetMethod("", "TM", "SetMSC");
					if(method == null) {
						Logger.Error("Failed to get the target method. (patch: {0})", patchName);
						return false;
					}

					ILProcessor	ilProcessor	= method.Body.GetILProcessor();
					var instructions		= ilProcessor.Body.Instructions;
					Logger.Debug("----- Instructions");
					for(int i = 0; i < instructions.Count; i++) {
						Instruction	instruction	= instructions[i];
						Logger.Debug("[{0:D3}] {1}", i, instruction);

						// ldfld int32 GMain::m_MSC
						FieldDefinition?	operandField	= instruction.Operand as FieldDefinition;
						if(
							(i >= 2)
							&& (instruction.OpCode.Code == Code.Ldfld)
							&& (operandField != null && operandField.Name == "m_MSC")
						) {
							// ; call(..., this.GM.m_MSC)           -> call(..., 1)
							// ldarg.0                              -> ldc.i4.1
							// ldfld        class GMain TM::GM      -> nop
							// ldfld        int32 GMain::m_MSC      -> nop
							// ldfld        int32 GMain::m_MSC      -> nop
							// conv.r4
							// call         void TM::(Material_SetFloat)(Material, string, float32)

							Logger.Debug("`m_MSC` found. replace...");

							ilProcessor.Replace(instructions[i - 2], Instruction.Create(OpCodes.Ldc_R4, 0.0f));
							ilProcessor.Replace(instructions[i - 1], Instruction.Create(OpCodes.Nop));
							ilProcessor.Replace(instructions[i - 0], Instruction.Create(OpCodes.Nop));

							method.Body.Optimize();

							Logger.Information("The patch was applied successfully. (patch: {0})", patchName);
							return true;
						}
					}

					Logger.Error("No instructions found to patch. (patch: {0})", patchName);

					return false;
				}
				catch(Exception e) {
					Logger.Exception(e, "Failed to patch. (patch: {0})", patchName);
					return false;
				}
			}
		}
	}

	namespace FV {
		public class Patcher : DecensoredPatcher {
			protected Patcher(ModuleDefinition module, string modulePath) : base(module, modulePath){
			}

			internal static Patcher?	ReadModule(string modulePath) {
				try {
					ModuleDefinition	module	= ModuleDefinition.ReadModule(modulePath);
					return new Patcher(module, modulePath);
				}
				catch(Exception e){
					Logger.Exception(e, "Failed to open the module. (\"{0}\")", modulePath);
					return null;
				}
			}

			override protected string	GetPatchName() {
				return "FV.Decensored";
			}

			override public bool CheckTargetModule() {
				// -.GMC.IsMSC()
				MethodDefinition?	method		= this.GetMethod("", "GMC", "IsMSC");
				return method != null;
			}

			override protected bool	PatchDecensored() {
				string	patchName	= this.GetPatchName();
				try {
					// -.GMC.IsMSC()
					MethodDefinition?	method		= this.GetMethod("", "GMC", "IsMSC");
					if(method == null) {
						Logger.Error("Failed to get the target method. (patch: {0})", patchName);
						return false;
					}

					ILProcessor	ilProcessor	= method.Body.GetILProcessor();
					var instructions		= ilProcessor.Body.Instructions;
					for(int i = 0; i < instructions.Count; i++) {
						Instruction	instruction	= instructions[i];
						Logger.Debug("[{0:D3}] {1}", i, instruction);

						// ldfld int32 GMC::m_MSC
						FieldDefinition?	operandField	= instruction.Operand as FieldDefinition;
						if(
							(i >= 1)
							&& (instruction.OpCode.Code == Code.Ldfld)
							&& (operandField != null && operandField.Name == "m_MSC")
						) {
							// ; if(this.m_MSC <= 1){ ... }         -> if(1 <= 1){ ... }
							// ldarg.0                              -> ldc.i4.1
							// ldfld        int32 GMC::m_MSC        -> nop
							// ldc.i4.1
							// bgt.s        +6

							Logger.Debug("`m_MSC` found. replace...");

							ilProcessor.Replace(instructions[i - 1], Instruction.Create(OpCodes.Ldc_I4_1));
							ilProcessor.Replace(instructions[i - 0], Instruction.Create(OpCodes.Nop));

							method.Body.Optimize();

							Logger.Information("The patch was applied successfully. (patch: {0})", patchName);
							return true;
						}
					}

					Logger.Error("No instructions found to patch. (patch: {0})", patchName);

					return false;
				}
				catch(Exception e) {
					Logger.Exception(e, "Failed to patch. (patch: {0})", patchName);
					return false;
				}
			}
		}
	}
}

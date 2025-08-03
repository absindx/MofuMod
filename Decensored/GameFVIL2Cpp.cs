using System;
using LibCpp2IL;
using LibCpp2IL.Metadata;
using Iced;
using Iced.Intel;

namespace MofuMod {
	namespace FV {
		public class PatcherIL2Cpp : DecensoredPatcher {
			protected readonly string	gamePlayer	= "";
			protected readonly string	gameModule	= "";
			protected readonly string	gameData	= "";
			protected readonly string	metaData	= "";
			protected readonly byte[]	moduleBytes	= [];
			protected readonly bool		initialized;

			protected PatcherIL2Cpp(string modulePath) : base(null, modulePath){
				this.initialized	= false;
	
				this.gamePlayer	= Path.Combine(modulePath, @"Game.exe");
				this.gameModule	= Path.Combine(modulePath, @"GameAssembly.dll");
				this.gameData	= Path.Combine(modulePath, @"Game_Data");
				this.metaData	= Path.Combine(modulePath, @"Game_Data\il2cpp_data\Metadata\global-metadata.dat");
				this.ModulePath	= this.gameModule;

				try {
					if(
						!File.Exists(this.gamePlayer)
						|| !File.Exists(this.gameModule)
						|| !File.Exists(this.metaData)
					){
						Logger.Debug("The files do not exist.");
						return;
					}

					this.moduleBytes	= File.ReadAllBytes(this.gameModule);
				}
				catch(Exception e) {
					Logger.Exception(e, "Failed to open the module. (\"{0}\")", modulePath);
					return;
				}

				this.initialized	= true;
			}

			internal static PatcherIL2Cpp?	ReadModule(string modulePath) {
				try {
					PatcherIL2Cpp	patcher	= new PatcherIL2Cpp(modulePath);

					var	unityVersion	= LibCpp2IlMain.DetermineUnityVersion(patcher.gamePlayer, patcher.gameData);
					Logger.Debug("Unity version = " + unityVersion.ToString());

					if(!LibCpp2IlMain.LoadFromFile(patcher.gameModule, patcher.metaData, unityVersion)){
						Logger.Debug("Failed to load the module.");
						return null;
					}

					if(LibCpp2IlMain.TheMetadata == null) {
						return null;
					}

					return patcher;
				}
				catch(Exception e){
					Logger.Exception(e, "Failed to open the module. (\"{0}\")", modulePath);
					return null;
				}
			}

			override protected string	GetPatchName() {
				return "FV.Decensored(IL2CPP)";
			}

			override public bool	CheckTargetModule() {
				// -.GMC.IsMSC()
				var	method	= this.GetMethodFromName("GMC", "IsMSC");
				return method != null;
			}

			override public bool	CheckPatched() {
				string	patchName	= this.GetPatchName();

				// -.GMC.IsMSC()
				var	method	= this.GetMethodFromName("GMC", "IsMSC");
				if(method == null) {
					Logger.Error("Failed to get the target method. (patch: {0})", patchName);
					return false;
				}

				int?	patchOffset	= this.GetPatchOffset();
				if(patchOffset.HasValue) {
					bool isPatched	= this.moduleBytes[patchOffset.Value] == 0x30;	// xor r/m8, r8
					return isPatched;
				}
				else {
					return false;
				}
			}

			override protected bool	PatchDecensored() {
				string	patchName	= this.GetPatchName();

				// -.GMC.IsMSC()
				var	method	= this.GetMethodFromName("GMC", "IsMSC");
				if(method == null) {
					Logger.Error("Failed to get the target method. (patch: {0})", patchName);
					return false;
				}

				int?	patchOffset	= this.GetPatchOffset();
				if(patchOffset.HasValue) {
					this.moduleBytes[patchOffset.Value]	= 0x30;	// xor r/m8, r8

					Logger.Information("The patch was applied successfully. (patch: {0})", patchName);
					return true;
				}
				else {
					return false;
				}
			}

			protected int?	GetPatchOffset() {
				string	patchName	= this.GetPatchName();
				try {
					// -.GMC.IsMSC()
					var	method	= this.GetMethodFromName("GMC", "IsMSC");
					if(method == null) {
						Logger.Error("Failed to get the target method. (patch: {0})", patchName);
						return null;
					}

					// get decoder
					var decoderSet	= this.GetDecoder(method, 0);
					if(decoderSet == null) {
						return null;
					}
					var codeReader	= decoderSet.Value.Item1;
					var decoder	= decoderSet.Value.Item2;

					Iced.Intel.Instruction	instruction		= Iced.Intel.Instruction.Create(Iced.Intel.Code.Nopd);
					Iced.Intel.Instruction	previousInstruction;
					bool			firstChecked		= false;
					while(codeReader.CanReadByte){
						previousInstruction	= instruction;
						decoder.Decode(out instruction);

						Logger.Debug($"{instruction.IP:X16} {instruction}");

						// check first instruction
						if(!firstChecked) {
							if(
								// MOV [rsp+8], rbx
								(instruction.Mnemonic == Iced.Intel.Mnemonic.Mov)
								&& (instruction.MemoryBase == Iced.Intel.Register.RSP)
							){
								// OK
								firstChecked	= true;
							}
							else{
								// invalid address
								break;
							}
						}

						// subroutine end
						if(instruction.Mnemonic == Iced.Intel.Mnemonic.Int3) {
							break;
						}

						// patch before
						//   84 C0 test al, al
						//   74 XX je   exitBranch
						// patch after
						//   30 C0 xor  al, al
						//   74 XX je   exitBranch
						if(instruction.FlowControl == Iced.Intel.FlowControl.ConditionalBranch) {
							// branch
							// check branch destination
							int	targetOffset	= (int)(instruction.MemoryDisplacement64 - method.MethodPointer);
							bool	isExitBranch	= this.DetectExitBranch(method, targetOffset, 8);
							if(!isExitBranch) {
								continue;
							}

							Logger.Debug("`m_MSC` found. replace...");
							Logger.Debug($"{previousInstruction.IP:X16} {previousInstruction}");
							Logger.Debug($"{instruction.IP        :X16} {instruction        }");

							// check previous instruction
							if(previousInstruction.Mnemonic != Iced.Intel.Mnemonic.Test) {
								break;
							}

							// patch
							ulong	patchOffset	= previousInstruction.IP - method.MethodPointer;
							int	fileOffset	= (int)(method.MethodOffsetInFile + (long)patchOffset);
							return fileOffset;
						}
					}

					Logger.Error("No instructions found to patch. (patch: {0})", patchName);

					return null;
				}
				catch(Exception e) {
					Logger.Exception(e, "Failed to patch. (patch: {0})", patchName);
					return null;
				}
			}

			override public bool	SaveModule(string modulePath) {
				try {
					// check same path
					if(this.ModulePath == Path.GetFullPath(modulePath)) {
						Logger.Error("Modules cannot be saved with the same file name.");
						return false;
					}

					File.WriteAllBytes(modulePath, this.moduleBytes);

					return true;
				}
				catch(Exception e) {
					Logger.Exception(e, "Failed to write the module. (\"{0}\")", modulePath);
					return false;
				}
			}

			internal Il2CppMethodDefinition?	GetMethodFromName(string className, string methodName) {
				if(LibCpp2IlMain.TheMetadata == null) {
					return null;
				}

				// normalize name
				className	= className.Replace("+", ".");

				foreach(var method in LibCpp2IlMain.TheMetadata.methodDefs) {
					string?	methodClassName	= method.DeclaringType?.Name?.Replace("+", ".");

					if(method.Name != methodName) {
						continue;
					}
					if(methodClassName != className) {
						continue;
					}
					return method;
				}
			
				return null;
			}

			internal (Iced.Intel.ByteArrayCodeReader, Iced.Intel.Decoder)?	GetDecoder(Il2CppMethodDefinition method, int offset) {
				ulong		rvaOffset	= (ulong)((long)method.MethodPointer + offset);
				int		fileOffset	= (int)(method.MethodOffsetInFile + offset);
				const int	readLength	= 256;
				if(this.moduleBytes.Length < (fileOffset + offset)) {
					return null;
				}

				int	architecture	= 64;
				var	codeReader	= new Iced.Intel.ByteArrayCodeReader(this.moduleBytes, fileOffset, readLength);
				var	decoder		= Iced.Intel.Decoder.Create(architecture, codeReader);
				decoder.IP		= rvaOffset;

				return (codeReader, decoder);
			}

			private bool	DetectExitBranch(Il2CppMethodDefinition method, int startOffset, int instructionDepth) {
				Logger.Debug($"check destination");

				// get decoder
				var decoderSet	= this.GetDecoder(method, startOffset);
				if(decoderSet == null) {
					return false;
				}
				var codeReader	= decoderSet.Value.Item1;
				var decoder	= decoderSet.Value.Item2;

				var	instructionInfo	= new Iced.Intel.InstructionInfoFactory();
				for(int instructionCount = 0; instructionCount< instructionDepth; instructionCount++){
					if(!codeReader.CanReadByte) {
						break;
					}

					decoder.Decode(out var instruction);

					Logger.Debug($"    {instruction.IP:X16} {instruction}");

					if(instruction.Mnemonic == Iced.Intel.Mnemonic.Int3) {
						break;
					}

					if(instruction.Mnemonic == Iced.Intel.Mnemonic.Ret) {
						Logger.Debug($"found");
						return true;
					}
				}

				Logger.Debug($"not found");
				return false;
			}
		}
	}
}

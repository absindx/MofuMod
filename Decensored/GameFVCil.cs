using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace MofuMod {
	namespace FV {
		public class PatcherCil : DecensoredPatcher {
			protected PatcherCil(ModuleDefinition module, string modulePath) : base(module, modulePath){
			}

			internal static PatcherCil?	ReadModule(string modulePath) {
				try {
					if(!File.Exists(modulePath)) {
						Logger.Debug("Module does not exist.");
						return null;
					}

					ModuleDefinition	module	= ModuleDefinition.ReadModule(modulePath);
					return new PatcherCil(module, modulePath);
				}
				catch(Exception e){
					Logger.Exception(e, "Failed to open the module. (\"{0}\")", modulePath);
					return null;
				}
			}

			override protected string	GetPatchName() {
				return "FV.Decensored(CIL)";
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
						Instruction		instruction	= instructions[i];
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

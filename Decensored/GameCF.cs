using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace MofuMod {
	namespace CF {
		public class Patcher : DecensoredPatcher {
			protected Patcher(ModuleDefinition module, string modulePath) : base(module, modulePath){
			}

			internal static Patcher?	ReadModule(string modulePath) {
				try {
					if(!File.Exists(modulePath)) {
						Logger.Debug("Module does not exist.");
						return null;
					}

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
}

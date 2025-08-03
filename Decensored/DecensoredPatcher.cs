using System;
using Mono.Cecil;

namespace MofuMod {
	public abstract class DecensoredPatcher : AbstructPatcher {
		protected DecensoredPatcher(ModuleDefinition? module, string modulePath) : base(module, modulePath){
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
				("FV", FV.PatcherCil.ReadModule),
				("FV", FV.PatcherIL2Cpp.ReadModule),
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
}

using Mono.Cecil;

namespace MofuMod {
	public class AbstructPatcher : IDisposable {
		protected ModuleDefinition	module;
		public string			ModulePath	{get; protected set;}

		protected AbstructPatcher(ModuleDefinition module, string modulePath) {
			this.module	= module;

			try {
				this.ModulePath	= Path.GetFullPath(modulePath);
			}
			catch {
				this.ModulePath	= modulePath;
			}
		}

		//--------------------------------------------------
		#region Dispose pattern
		//--------------------------------------------------

		private bool	disposed	= false;
		public void	Dispose() {
			this.Dispose(true);
		}
		protected virtual void Dispose(bool disposing) {
			if(this.disposed) {
				return;
			}

			if(disposing) {
				this.module.Dispose();
			}
			this.disposed	= true;
		}

		#endregion	// Dispose pattern

		public bool	CheckPatched() {
			// check type exists `MonoMod.WasHere` in the target assembly
			AssemblyDefinition	assembly	= this.module.Assembly;
			bool			patched		= assembly.MainModule.GetType("MonoMod.WasHere") != null;
			return patched;
		}

		public bool	SaveModule(string modulePath) {
			try {
				// check same path
				if(this.ModulePath == Path.GetFullPath(modulePath)) {
					Logger.Error("Modules cannot be saved with the same file name.");
					return false;
				}

				this.module.Write(modulePath);

				return true;
			}
			catch(Exception e) {
				Logger.Exception(e, "Failed to write the module. (\"{0}\")", modulePath);
				return false;
			}
		}

		protected MethodDefinition?	GetMethod(string targetNamespace, string targetClass, string targetFunction) {
			try {
				TypeDefinition		targetType	= this.module.GetType(targetNamespace, targetClass);
				if(targetType == null) {
					return null;
				}

				foreach(MethodDefinition method in targetType.Methods) {
					if(method.Name == targetFunction) {
						return method;
					}
				}
				return null;
			}
			catch {
				return null;
			}
		}
	}
}

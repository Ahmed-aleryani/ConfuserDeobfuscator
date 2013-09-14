﻿/*
    Copyright (C) 2011-2013 de4dot@gmail.com

    This file is part of de4dot.

    de4dot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dot.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using dnlib.DotNet;
using de4dot.blocks;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v3 {
	// Finds the type that saves the native lib (if in resources) to disk
	class NativeLibSaver {
		ModuleDefMD module;
		TypeDef nativeLibCallerType;
		MethodDef initMethod;
		Resource nativeFileResource;

		public TypeDef Type {
			get { return nativeLibCallerType; }
		}

		public MethodDef InitMethod {
			get { return initMethod; }
		}

		public Resource Resource {
			get { return nativeFileResource; }
		}

		public bool Detected {
			get { return nativeLibCallerType != null; }
		}

		public NativeLibSaver(ModuleDefMD module) {
			this.module = module;
		}

		public NativeLibSaver(ModuleDefMD module, NativeLibSaver oldOne) {
			this.module = module;
			this.nativeLibCallerType = lookup(oldOne.nativeLibCallerType, "Could not find nativeLibCallerType");
			this.initMethod = lookup(oldOne.initMethod, "Could not find initMethod");
			if (oldOne.nativeFileResource != null) {
				this.nativeFileResource = DotNetUtils.getResource(module, oldOne.nativeFileResource.Name.String);
				if (this.nativeFileResource == null)
					throw new ApplicationException("Could not find nativeFileResource");
			}
		}

		T lookup<T>(T def, string errorMessage) where T : class, ICodedToken {
			return DeobUtils.lookup(module, def, errorMessage);
		}

		public void find() {
			foreach (var calledMethod in DotNetUtils.getCalledMethods(module, DotNetUtils.getModuleTypeCctor(module))) {
				if (!DotNetUtils.isMethod(calledMethod, "System.Void", "()"))
					continue;
				if (calledMethod.DeclaringType.FullName != "<PrivateImplementationDetails>{F1C5056B-0AFC-4423-9B83-D13A26B48869}")
					continue;

				nativeLibCallerType = calledMethod.DeclaringType;
				initMethod = calledMethod;
				foreach (var s in DotNetUtils.getCodeStrings(initMethod)) {
					nativeFileResource = DotNetUtils.getResource(module, s);
					if (nativeFileResource != null)
						break;
				}
				return;
			}
		}
	}
}

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
using System.IO;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using de4dot.blocks;

namespace de4dot.code {
	class AssemblyModule {
		string filename;
		ModuleDefMD module;
		ModuleContext moduleContext;
	    MemoryStream data;

        public AssemblyModule(string filename, ModuleContext moduleContext)
        {
            this.filename = Utils.getFullPath(filename);
            this.moduleContext = moduleContext;
        }

		public AssemblyModule(string filename, ModuleContext moduleContext, MemoryStream data) {
			this.filename = Utils.getFullPath(filename);
			this.moduleContext = moduleContext;
            this.data = data;
		}

		public ModuleDefMD load() {
			return setModule(ModuleDefMD.Load(data, moduleContext));
		}

		public ModuleDefMD load(byte[] fileData) {
			return setModule(ModuleDefMD.Load(fileData, moduleContext));
		}

		ModuleDefMD setModule(ModuleDefMD newModule) {
			module = newModule;
			TheAssemblyResolver.Instance.addModule(module);
			module.EnableTypeDefFindCache = true;
			module.Location = filename;
			return module;
		}

        public void save(Stream memStream)
        {
            if (module.IsILOnly) {

				module.Write(memStream);
			}
			else {

				module.NativeWrite(memStream);
			}
        }

		public void save(Stream memStream, MetaDataFlags mdFlags, IModuleWriterListener writerListener) {
			if (module.IsILOnly) {
				var writerOptions = new ModuleWriterOptions(module, writerListener);
				writerOptions.MetaDataOptions.Flags |= mdFlags;
				writerOptions.Logger = Logger.Instance;
                module.Write(memStream, writerOptions);
			}
			else {
				var writerOptions = new NativeModuleWriterOptions(module, writerListener);
				writerOptions.MetaDataOptions.Flags |= mdFlags;
				writerOptions.Logger = Logger.Instance;
				writerOptions.KeepExtraPEData = true;
				writerOptions.KeepWin32Resources = true;
                module.NativeWrite(memStream, writerOptions);
			}
		}

		public ModuleDefMD reload(byte[] newModuleData, DumpedMethodsRestorer dumpedMethodsRestorer, IStringDecrypter stringDecrypter) {
			TheAssemblyResolver.Instance.Remove(module);
			var mod = ModuleDefMD.Load(newModuleData, moduleContext);
			if (dumpedMethodsRestorer != null)
				dumpedMethodsRestorer.Module = mod;
			mod.StringDecrypter = stringDecrypter;
			mod.MethodDecrypter = dumpedMethodsRestorer;
			mod.TablesStream.ColumnReader = dumpedMethodsRestorer;
			mod.TablesStream.MethodRowReader = dumpedMethodsRestorer;
			return setModule(mod);
		}

		public override string ToString() {
			return filename;
		}
	}
}

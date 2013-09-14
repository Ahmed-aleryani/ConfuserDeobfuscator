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

using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using de4dot.blocks;

namespace de4dot.code.deobfuscators {
	class MethodCallRestorerBase {
		protected MemberRefBuilder builder;
		protected ModuleDefMD module;
		MethodDefAndDeclaringTypeDict<NewMethodInfo> oldToNewMethod = new MethodDefAndDeclaringTypeDict<NewMethodInfo>();

		class NewMethodInfo {
			public OpCode opCode;
			public IMethod method;

			public NewMethodInfo(OpCode opCode, IMethod method) {
				this.opCode = opCode;
				this.method = method;
			}
		}

		public MethodCallRestorerBase(ModuleDefMD module) {
			this.module = module;
			this.builder = new MemberRefBuilder(module);
		}

		public void createGetManifestResourceStream1(MethodDef oldMethod) {
			if (oldMethod == null)
				return;
			var assemblyType = builder.type("System.Reflection", "Assembly", builder.CorLib);
			var streamType = builder.type("System.IO", "Stream", builder.CorLib);
			var newMethod = builder.instanceMethod("GetManifestResourceStream", assemblyType.TypeDefOrRef, streamType, builder.String);
			add(oldMethod, newMethod, OpCodes.Callvirt);
		}

		public void createGetManifestResourceStream2(MethodDef oldMethod) {
			if (oldMethod == null)
				return;
			var assemblyType = builder.type("System.Reflection", "Assembly", builder.CorLib);
			var typeType = builder.type("System", "Type", builder.CorLib);
			var streamType = builder.type("System.IO", "Stream", builder.CorLib);
			var newMethod = builder.instanceMethod("GetManifestResourceStream", assemblyType.TypeDefOrRef, streamType, typeType, builder.String);
			add(oldMethod, newMethod, OpCodes.Callvirt);
		}

		public void createGetManifestResourceNames(MethodDef oldMethod) {
			if (oldMethod == null)
				return;
			var assemblyType = builder.type("System.Reflection", "Assembly", builder.CorLib);
			var stringArrayType = builder.array(builder.String);
			var newMethod = builder.instanceMethod("GetManifestResourceNames", assemblyType.TypeDefOrRef, stringArrayType);
			add(oldMethod, newMethod, OpCodes.Callvirt);
		}

		public void createBitmapCtor(MethodDef oldMethod) {
			if (oldMethod == null)
				return;
			var bitmapType = builder.type("System.Drawing", "Bitmap", "System.Drawing");
			var typeType = builder.type("System", "Type", builder.CorLib);
			var newMethod = builder.instanceMethod(".ctor", bitmapType.TypeDefOrRef, builder.Void, typeType, builder.String);
			add(oldMethod, newMethod, OpCodes.Newobj);
		}

		public void createIconCtor(MethodDef oldMethod) {
			if (oldMethod == null)
				return;
			var iconType = builder.type("System.Drawing", "Icon", "System.Drawing");
			var typeType = builder.type("System", "Type", builder.CorLib);
			var newMethod = builder.instanceMethod(".ctor", iconType.TypeDefOrRef, builder.Void, typeType, builder.String);
			add(oldMethod, newMethod, OpCodes.Newobj);
		}

		protected void add(MethodDef oldMethod, IMethod newMethod) {
			add(oldMethod, newMethod, OpCodes.Callvirt);
		}

		protected void add(MethodDef oldMethod, IMethod newMethod, OpCode opCode) {
			if (oldMethod == null)
				return;
			oldToNewMethod.add(oldMethod, new NewMethodInfo(opCode, newMethod));
		}

		public void deobfuscate(Blocks blocks) {
			if (oldToNewMethod.Count == 0)
				return;
			foreach (var block in blocks.MethodBlocks.getAllBlocks()) {
				var instrs = block.Instructions;
				for (int i = 0; i < instrs.Count; i++) {
					var call = instrs[i];
					if (call.OpCode.Code != Code.Call)
						continue;
					var calledMethod = call.Operand as MethodDef;
					if (calledMethod == null)
						continue;

					var newMethodInfo = oldToNewMethod.find(calledMethod);
					if (newMethodInfo == null)
						continue;

					instrs[i] = new Instr(Instruction.Create(newMethodInfo.opCode, newMethodInfo.method));
				}
			}
		}
	}
}

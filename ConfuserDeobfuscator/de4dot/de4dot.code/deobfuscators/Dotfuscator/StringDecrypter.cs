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

namespace de4dot.code.deobfuscators.Dotfuscator {
	class StringDecrypter {
		ModuleDefMD module;
		MethodDefAndDeclaringTypeDict<StringDecrypterInfo> stringDecrypterMethods = new MethodDefAndDeclaringTypeDict<StringDecrypterInfo>();

		public class StringDecrypterInfo {
			public MethodDef method;
			public int magic;
			public StringDecrypterInfo(MethodDef method, int magic) {
				this.method = method;
				this.magic = magic;
			}
		}

		public bool Detected {
			get { return stringDecrypterMethods.Count > 0; }
		}

		public IEnumerable<MethodDef> StringDecrypters {
			get {
				var list = new List<MethodDef>(stringDecrypterMethods.Count);
				foreach (var info in stringDecrypterMethods.getValues())
					list.Add(info.method);
				return list;
			}
		}

		public IEnumerable<StringDecrypterInfo> StringDecrypterInfos {
			get { return stringDecrypterMethods.getValues(); }
		}

		public StringDecrypter(ModuleDefMD module) {
			this.module = module;
		}

		public void find(ISimpleDeobfuscator simpleDeobfuscator) {
			foreach (var type in module.GetTypes())
				findStringDecrypterMethods(type, simpleDeobfuscator);
		}

		void findStringDecrypterMethods(TypeDef type, ISimpleDeobfuscator simpleDeobfuscator) {
			foreach (var method in DotNetUtils.findMethods(type.Methods, "System.String", new string[] { "System.String", "System.Int32" })) {
				if (method.Body.HasExceptionHandlers)
					continue;

				if (DotNetUtils.getMethodCalls(method, "System.Char[] System.String::ToCharArray()") != 1)
					continue;
				if (DotNetUtils.getMethodCalls(method, "System.String System.String::Intern(System.String)") != 1)
					continue;

				simpleDeobfuscator.deobfuscate(method);
				var instructions = method.Body.Instructions;
				for (int i = 0; i <= instructions.Count - 3; i++) {
					var ldci4 = method.Body.Instructions[i];
					if (!ldci4.IsLdcI4())
						continue;
					if (instructions[i + 1].OpCode.Code != Code.Ldarg_1)
						continue;
					if (instructions[i + 2].OpCode.Code != Code.Add)
						continue;

					var info = new StringDecrypterInfo(method, ldci4.GetLdcI4Value());
					stringDecrypterMethods.add(info.method, info);
					Logger.v("Found string decrypter method: {0}, magic: 0x{1:X8}", Utils.removeNewlines(info.method), info.magic);
					break;
				}
			}
		}

		public string decrypt(IMethod method, string encrypted, int value) {
			var info = stringDecrypterMethods.findAny(method);
			char[] chars = encrypted.ToCharArray();
			byte key = (byte)(info.magic + value);
			for (int i = 0; i < chars.Length; i++) {
				char c = chars[i];
				byte b1 = (byte)((byte)c ^ key++);
				byte b2 = (byte)((byte)(c >> 8) ^ key++);
				chars[i] = (char)((b1 << 8) | b2);
			}
			return new string(chars);
		}
	}
}

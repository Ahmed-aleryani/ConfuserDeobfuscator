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

using System.IO;
using System.Security.Cryptography;
using System.Text;
using dnlib.IO;
using dnlib.DotNet;
using de4dot.blocks;

namespace de4dot.code.deobfuscators.Agile_NET {
	class ResourceDecrypter {
		ModuleDefMD module;
		TypeDef rsrcType;
		MethodDef rsrcRrrMethod;
		MethodDef rsrcResolveMethod;

		public bool Detected {
			get { return rsrcType != null; }
		}

		public TypeDef Type {
			get { return rsrcType; }
		}

		public MethodDef RsrcRrrMethod {
			get { return rsrcRrrMethod; }
		}

		public ResourceDecrypter(ModuleDefMD module) {
			this.module = module;
		}

		public ResourceDecrypter(ModuleDefMD module, ResourceDecrypter oldOne) {
			this.module = module;
			rsrcType = lookup(oldOne.rsrcType, "Could not find rsrcType");
			rsrcRrrMethod = lookup(oldOne.rsrcRrrMethod, "Could not find rsrcRrrMethod");
			rsrcResolveMethod = lookup(oldOne.rsrcResolveMethod, "Could not find rsrcResolveMethod");
		}

		T lookup<T>(T def, string errorMessage) where T : class, ICodedToken {
			return DeobUtils.lookup(module, def, errorMessage);
		}

		public void find() {
			findResourceType();
		}

		static readonly string[] requiredFields = new string[] {
			"System.Reflection.Assembly",
			"System.String[]",
		};
		void findResourceType() {
			var cctor = DotNetUtils.getModuleTypeCctor(module);
			if (cctor == null)
				return;

			foreach (var calledMethod in DotNetUtils.getCalledMethods(module, cctor)) {
				if (!calledMethod.IsStatic || calledMethod.Body == null)
					continue;
				if (!DotNetUtils.isMethod(calledMethod, "System.Void", "()"))
					continue;
				var type = calledMethod.DeclaringType;
				if (!new FieldTypes(type).exactly(requiredFields))
					continue;

				var resolveHandler = DotNetUtils.getMethod(type, "System.Reflection.Assembly", "(System.Object,System.ResolveEventArgs)");
				var decryptMethod = DotNetUtils.getMethod(type, "System.Byte[]", "(System.IO.Stream)");
				if (resolveHandler == null || !resolveHandler.IsStatic)
					continue;
				if (decryptMethod == null || !decryptMethod.IsStatic)
					continue;

				rsrcType = type;
				rsrcRrrMethod = calledMethod;
				rsrcResolveMethod = resolveHandler;
				return;
			}
		}

		public EmbeddedResource mergeResources() {
			if (rsrcResolveMethod == null)
				return null;
			var resource = DotNetUtils.getResource(module, DotNetUtils.getCodeStrings(rsrcResolveMethod)) as EmbeddedResource;
			if (resource == null)
				return null;
			DeobUtils.decryptAndAddResources(module, resource.Name.String, () => decryptResource(resource));
			return resource;
		}

		byte[] decryptResource(EmbeddedResource resource) {
			var reader = resource.Data;
			reader.Position = 0;
			var key = reader.ReadString();
			var data = reader.ReadRemainingBytes();
			var cryptoTransform = new DESCryptoServiceProvider {
				Key = Encoding.ASCII.GetBytes(key),
				IV = Encoding.ASCII.GetBytes(key),
			}.CreateDecryptor();
			var memStream = new MemoryStream(data);
			using (var reader2 = new BinaryReader(new CryptoStream(memStream, cryptoTransform, CryptoStreamMode.Read))) {
				return reader2.ReadBytes((int)memStream.Length);
			}
		}
	}
}

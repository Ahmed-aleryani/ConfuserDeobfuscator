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
using System.Collections.Generic;
using System.IO;
using dnlib.DotNet;
using de4dot.blocks;
using de4dot.blocks.cflow;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v3 {
	class MemoryPatcher {
		DecryptMethod decryptMethod = new DecryptMethod();
		List<PatchInfo> patchInfos = new List<PatchInfo>();

		class PatchInfo {
			public int[] offsets;
			public int[] values;
			public PatchInfo(int[] offsets, int[] values) {
				this.offsets = offsets;
				this.values = values;
			}
		}

		public bool Detected {
			get { return decryptMethod.Detected; }
		}

		public MemoryPatcher(TypeDef type, ICflowDeobfuscator cflowDeobfuscator) {
			find(type, cflowDeobfuscator);
		}

		void find(TypeDef type, ICflowDeobfuscator cflowDeobfuscator) {
			var additionalTypes = new List<string> {
				"System.IO.BinaryWriter",
			};
			foreach (var method in type.Methods) {
				if (!DotNetUtils.isMethod(method, "System.Void", "(System.Int32[],System.UInt32[])"))
					continue;
				if (!DecryptMethod.couldBeDecryptMethod(method, additionalTypes))
					continue;
				cflowDeobfuscator.deobfuscate(method);
				if (!decryptMethod.getKey(method))
					continue;

				findPatchData(type, cflowDeobfuscator);
				return;
			}
		}

		void findPatchData(TypeDef type, ICflowDeobfuscator cflowDeobfuscator) {
			var locals = new List<string> {
				"System.Int32[]",
				"System.UInt32[]",
			};
			foreach (var method in type.Methods) {
				if (method.Attributes != MethodAttributes.Private)
					continue;
				if (!DotNetUtils.isMethod(method, "System.Void", "()"))
					continue;
				if (!new LocalTypes(method).exactly(locals))
					continue;
				cflowDeobfuscator.deobfuscate(method);
				var patchInfo = getPatchInfo(method);
				if (patchInfo == null)
					continue;

				patchInfos.Add(patchInfo);
			}
		}

		PatchInfo getPatchInfo(MethodDef method) {
			int index1 = 0, index2, index3, size1, size2, size3;
			if (!ArrayFinder.findNewarr(method, ref index1, out size1))
				return null;
			index2 = index1 + 1;
			if (!ArrayFinder.findNewarr(method, ref index2, out size2))
				return null;
			index3 = index2 + 1;
			if (ArrayFinder.findNewarr(method, ref index3, out size3))
				return null;

			if (size1 <= 0 || size1 > 35)
				return null;

			var ary1 = ArrayFinder.getInitializedInt32Array(size1, method, ref index1);
			var ary2 = ArrayFinder.getInitializedInt32Array(size2, method, ref index2);
			if (ary1 == null || ary2 == null)
				return null;
			ary2 = decrypt(ary2);
			if (ary2 == null || ary1.Length != ary2.Length)
				return null;

			for (int i = 0; i < ary1.Length; i++)
				ary1[i] = -ary1[i];

			return new PatchInfo(ary1, ary2);
		}

		int[] decrypt(int[] data) {
			var memStream = new MemoryStream();
			var writer = new BinaryWriter(memStream);
			foreach (var value in data)
				writer.Write(value);
			byte[] decrypted;
			try {
				decrypted = DeobUtils.aesDecrypt(memStream.ToArray(), decryptMethod.Key, decryptMethod.Iv);
			}
			catch {
				return null;
			}
			if (decrypted.Length / 4 * 4 != decrypted.Length)
				return null;
			var newData = new int[decrypted.Length / 4];
			for (int i = 0; i < newData.Length; i++)
				newData[i] = BitConverter.ToInt32(decrypted, i * 4);
			return newData;
		}

		public void patch(byte[] peImageData) {
			using (var peImage = new MyPEImage(peImageData)) {
				foreach (var info in patchInfos) {
					for (int i = 0; i < info.offsets.Length; i++)
						peImage.dotNetSafeWriteOffset((uint)info.offsets[i], BitConverter.GetBytes(info.values[i]));
				}
			}
		}
	}
}

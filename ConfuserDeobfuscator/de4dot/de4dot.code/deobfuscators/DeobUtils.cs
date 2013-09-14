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
using System.IO.Compression;
using System.Security.Cryptography;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using ICSharpCode.SharpZipLib.Zip.Compression;
using de4dot.blocks;

namespace de4dot.code.deobfuscators {
	static class DeobUtils {
		public static void decryptAndAddResources(ModuleDef module, string encryptedName, Func<byte[]> decryptResource) {
			Logger.v("Decrypting resources, name: {0}", Utils.toCsharpString(encryptedName));
			var decryptedResourceData = decryptResource();
			if (decryptedResourceData == null)
				throw new ApplicationException("decryptedResourceData is null");
			var resourceModule = ModuleDefMD.Load(decryptedResourceData);

			Logger.Instance.indent();
			foreach (var rsrc in resourceModule.Resources) {
				Logger.v("Adding decrypted resource {0}", Utils.toCsharpString(rsrc.Name));
				module.Resources.Add(rsrc);
			}
			Logger.Instance.deIndent();
		}

		public static T lookup<T>(ModuleDefMD module, T def, string errorMessage) where T : class, ICodedToken {
			if (def == null)
				return null;
			var newDef = module.ResolveToken(def.MDToken.Raw) as T;
			if (newDef == null)
				throw new ApplicationException(errorMessage);
			return newDef;
		}

		public static byte[] readModule(ModuleDef module) {
			return Utils.readFile(module.Location);
		}

		public static bool isCode(short[] nativeCode, byte[] code) {
			if (nativeCode.Length != code.Length)
				return false;
			for (int i = 0; i < nativeCode.Length; i++) {
				if (nativeCode[i] == -1)
					continue;
				if ((byte)nativeCode[i] != code[i])
					return false;
			}
			return true;
		}

		public static byte[] md5Sum(byte[] data) {
			return MD5.Create().ComputeHash(data);
		}

		public static byte[] sha1Sum(byte[] data) {
			return SHA1.Create().ComputeHash(data);
		}

		public static byte[] sha256Sum(byte[] data) {
			return SHA256.Create().ComputeHash(data);
		}

		public static byte[] aesDecrypt(byte[] data, byte[] key, byte[] iv) {
			using (var aes = new RijndaelManaged { Mode = CipherMode.CBC }) {
				using (var transform = aes.CreateDecryptor(key, iv)) {
					return transform.TransformFinalBlock(data, 0, data.Length);
				}
			}
		}

		public static byte[] des3Decrypt(byte[] data, byte[] key, byte[] iv) {
			using (var des3 = TripleDES.Create()) {
				using (var transform = des3.CreateDecryptor(key, iv)) {
					return transform.TransformFinalBlock(data, 0, data.Length);
				}
			}
		}

		public static byte[] desDecrypt(byte[] data, int start, int len, byte[] key, byte[] iv) {
			using (var des = new DESCryptoServiceProvider()) {
				using (var transform = des.CreateDecryptor(key, iv)) {
					return transform.TransformFinalBlock(data, start, len);
				}
			}
		}

		// Code converted from C implementation @ http://en.wikipedia.org/wiki/XXTEA (btea() func)
		public static void xxteaDecrypt(uint[] v, uint[] key) {
			const uint DELTA = 0x9E3779B9;
			int n = v.Length;
			uint rounds = (uint)(6 + 52 / n);
			uint sum = rounds * DELTA;
			uint y = v[0];
			uint z;
			//#define MX (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (key[(p & 3) ^ e] ^ z)))
			do {
				int e = (int)((sum >> 2) & 3);
				int p;
				for (p = n - 1; p > 0; p--) {
					z = v[p - 1];
					y = v[p] -= (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (key[(p & 3) ^ e] ^ z)));
				}
				z = v[n - 1];
				y = v[0] -= (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (key[(p & 3) ^ e] ^ z)));
			} while ((sum -= DELTA) != 0);
		}

		// Code converted from C implementation @ http://en.wikipedia.org/wiki/XTEA (decipher() func)
		public static void xteaDecrypt(ref uint v0, ref uint v1, uint[] key, int rounds) {
			const uint delta = 0x9E3779B9;
			uint sum = (uint)(delta * rounds);
			for (int i = 0; i < rounds; i++) {
				v1 -= (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(sum >> 11) & 3]);
				sum -= delta;
				v0 -= (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[sum & 3]);
			}
		}

		public static string getExtension(ModuleKind kind) {
			switch (kind) {
			case ModuleKind.Dll:
				return ".dll";
			case ModuleKind.NetModule:
				return ".netmodule";
			case ModuleKind.Console:
			case ModuleKind.Windows:
			default:
				return ".exe";
			}
		}

		public static byte[] inflate(byte[] data, bool noHeader) {
			return inflate(data, 0, data.Length, noHeader);
		}

		public static byte[] inflate(byte[] data, int start, int len, bool noHeader) {
			return inflate(data, start, len, new Inflater(noHeader));
		}

		public static byte[] inflate(byte[] data, Inflater inflater) {
			return inflate(data, 0, data.Length, inflater);
		}

		public static byte[] inflate(byte[] data, int start, int len, Inflater inflater) {
			var buffer = new byte[0x1000];
			var memStream = new MemoryStream();
			inflater.SetInput(data, start, len);
			while (true) {
				int count = inflater.Inflate(buffer, 0, buffer.Length);
				if (count == 0)
					break;
				memStream.Write(buffer, 0, count);
			}
			return memStream.ToArray();
		}

		public static byte[] gunzip(Stream input, int decompressedSize) {
			using (var gzip = new GZipStream(input, CompressionMode.Decompress)) {
				var decompressed = new byte[decompressedSize];
				if (gzip.Read(decompressed, 0, decompressedSize) != decompressedSize)
					throw new ApplicationException("Could not gzip decompress");
				return decompressed;
			}
		}

		public static EmbeddedResource getEmbeddedResourceFromCodeStrings(ModuleDef module, MethodDef method) {
			foreach (var s in DotNetUtils.getCodeStrings(method)) {
				var resource = DotNetUtils.getResource(module, s) as EmbeddedResource;
				if (resource != null)
					return resource;
			}
			return null;
		}

		public static int readVariableLengthInt32(byte[] data, ref int index) {
			byte b = data[index++];
			if ((b & 0x80) == 0)
				return b;
			if ((b & 0x40) == 0)
				return (((int)b & 0x3F) << 8) + data[index++];
			return (((int)b & 0x1F) << 24) +
					((int)data[index++] << 16) +
					((int)data[index++] << 8) +
					data[index++];
		}

		public static bool hasInteger(MethodDef method, uint value) {
			return hasInteger(method, (int)value);
		}

		public static bool hasInteger(MethodDef method, int value) {
			return indexOfLdci4Instruction(method, value) >= 0;
		}

		public static int indexOfLdci4Instruction(MethodDef method, int value) {
			if (method == null || method.Body == null)
				return -1;
			var instrs = method.Body.Instructions;
			for (int i = 0; i < instrs.Count; i++) {
				var instr = instrs[i];
				if (!instr.IsLdcI4())
					continue;
				if (instr.GetLdcI4Value() == value)
					return i;
			}
			return -1;
		}

		public static IEnumerable<MethodDef> getInitCctors(ModuleDef module, int maxCctors) {
			var cctor = DotNetUtils.getModuleTypeCctor(module);
			if (cctor != null)
				yield return cctor;

			var entryPoint = module.EntryPoint;
			if (entryPoint != null) {
				cctor = entryPoint.DeclaringType.FindStaticConstructor();
				if (cctor != null)
					yield return cctor;
			}

			foreach (var type in module.GetTypes()) {
				if (type == module.GlobalType)
					continue;
				cctor = type.FindStaticConstructor();
				if (cctor == null)
					continue;
				yield return cctor;
				if (!type.IsEnum && --maxCctors <= 0)
					break;
			}
		}

		public static List<MethodDef> getAllResolveHandlers(MethodDef method) {
			var list = new List<MethodDef>();
			if (method == null || method.Body == null)
				return list;
			foreach (var instr in method.Body.Instructions) {
				if (instr.OpCode.Code != Code.Ldftn && instr.OpCode.Code != Code.Ldvirtftn)
					continue;
				var handler = instr.Operand as MethodDef;
				if (handler == null)
					continue;
				if (!DotNetUtils.isMethod(handler, "System.Reflection.Assembly", "(System.Object,System.ResolveEventArgs)"))
					continue;
				list.Add(handler);
			}
			return list;
		}

		public static MethodDef getResolveMethod(MethodDef method) {
			var handlers = DeobUtils.getAllResolveHandlers(method);
			if (handlers.Count == 0)
				return null;
			return handlers[0];
		}
	}
}

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
using System.Text;
using dnlib.DotNet;
using de4dot.code;
using de4dot.code.deobfuscators;
using System.IO;

namespace de4dot.cui {
	class ExitException : Exception {
		public readonly int code;
		public ExitException(int code) {
			this.code = code;
		}
	}

	class Program {
		static IList<IDeobfuscatorInfo> deobfuscatorInfos = createDeobfuscatorInfos();

		static IList<IDeobfuscatorInfo> createDeobfuscatorInfos() {
			return new List<IDeobfuscatorInfo> {
				new de4dot.code.deobfuscators.Unknown.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.Agile_NET.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.Babel_NET.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.CodeFort.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.CodeVeil.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.CodeWall.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.CryptoObfuscator.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.DeepSea.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.Dotfuscator.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.dotNET_Reactor.v3.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.dotNET_Reactor.v4.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.Eazfuscator_NET.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.Goliath_NET.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.ILProtector.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.MaxtoCode.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.MPRESS.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.Rummage.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.Skater_NET.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.SmartAssembly.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.Spices_Net.DeobfuscatorInfo(),
				new de4dot.code.deobfuscators.Xenocode.DeobfuscatorInfo(),
			};
		}

		public static int main(string[] args) {
			int exitCode = 0;

			const string showAllMessagesEnvName = "SHOWALLMESSAGES";
			try {
				if (Console.OutputEncoding.IsSingleByte)
					Console.OutputEncoding = new UTF8Encoding(false);

				Logger.Instance.CanIgnoreMessages = !hasEnv(showAllMessagesEnvName);

				Logger.n("");
				Logger.n("de4dot v{0} Copyright (C) 2011-2013 de4dot@gmail.com", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
				Logger.n("Latest version and source code: https://bitbucket.org/0xd4d/de4dot");
				Logger.n("");

			}
			catch (ExitException ex) {
				exitCode = ex.code;
			}
			catch (UserException ex) {
				Logger.Instance.LogErrorDontIgnore("{0}", ex.Message);
				exitCode = 1;
			}
			catch (Exception ex) {
				if (printFullStackTrace()) {
					printStackTrace(ex);
					Logger.Instance.LogErrorDontIgnore("\nTry the latest version before reporting this problem!");
				}
				else {
					Logger.Instance.LogErrorDontIgnore("\n\n");
					Logger.Instance.LogErrorDontIgnore("Hmmmm... something didn't work. Try the latest version.");
					Logger.Instance.LogErrorDontIgnore("    EX: {0} : message: {1}", ex.GetType(), ex.Message);
					Logger.Instance.LogErrorDontIgnore("If it's a supported obfuscator, it could be a bug or a new obfuscator version.");
					Logger.Instance.LogErrorDontIgnore("If it's an unsupported obfuscator, make sure the methods are decrypted!");
				}
				Logger.Instance.LogErrorDontIgnore("Send bug reports to de4dot@gmail.com or go to https://bitbucket.org/0xd4d/de4dot/issues");
				Logger.Instance.LogErrorDontIgnore("I will need the original files, so email me a link to the installer or a zip/rar file.");
				exitCode = 1;
			}

			if (Logger.Instance.NumIgnoredMessages > 0) {
				Logger.n("Ignored {0} warnings/errors", Logger.Instance.NumIgnoredMessages);
				Logger.n("Use -v/-vv option or set environment variable {0}=1 to see all messages", showAllMessagesEnvName);
			}

			if (isN00bUser()) {
				Console.Error.WriteLine("\n\nPress any key to exit...\n");
				try {
					Console.ReadKey(true);
				}
				catch (InvalidOperationException) {
				}
			}

			return exitCode;
		}

		static bool printFullStackTrace() {
			if (!Logger.Instance.IgnoresEvent(LoggerEvent.Verbose))
				return true;
			if (hasEnv("STACKTRACE"))
				return true;

			return false;
		}

		static bool hasEnv(string name) {
			foreach (var tmp in Environment.GetEnvironmentVariables().Keys) {
				var env = tmp as string;
				if (env == null)
					continue;
				if (string.Equals(env, name, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		static bool isN00bUser() {
			if (hasEnv("VisualStudioDir"))
				return false;
			return hasEnv("windir") && !hasEnv("PROMPT");
		}

		public static void printStackTrace(Exception ex) {
			printStackTrace(ex, LoggerEvent.Error);
		}

		public static void printStackTrace(Exception ex, LoggerEvent loggerEvent) {
			var line = new string('-', 78);
			Logger.Instance.Log(false, null, loggerEvent, "\n\n");
			Logger.Instance.Log(false, null, loggerEvent, line);
			Logger.Instance.Log(false, null, loggerEvent, "Stack trace:\n{0}", ex.StackTrace);
			Logger.Instance.Log(false, null, loggerEvent, "\n\nCaught an exception:\n");
			Logger.Instance.Log(false, null, loggerEvent, line);
			Logger.Instance.Log(false, null, loggerEvent, "Message:");
			Logger.Instance.Log(false, null, loggerEvent, "  {0}", ex.Message);
			Logger.Instance.Log(false, null, loggerEvent, "Type:");
			Logger.Instance.Log(false, null, loggerEvent, "  {0}", ex.GetType());
			Logger.Instance.Log(false, null, loggerEvent, line);
		}

		static void parseCommandLine(string[] args, FilesDeobfuscator.Options options) {
			new CommandLineParser(deobfuscatorInfos, options).parse(args);

			Logger.vv("Args:");
			Logger.Instance.indent();
			foreach (var arg in args)
				Logger.vv("{0}", Utils.toCsharpString(arg));
			Logger.Instance.deIndent();
		}
	}
}

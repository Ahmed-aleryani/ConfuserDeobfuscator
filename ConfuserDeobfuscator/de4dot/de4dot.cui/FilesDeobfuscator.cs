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
using de4dot.code;
using de4dot.code.renamer;
using de4dot.code.deobfuscators;
using de4dot.code.AssemblyClient;

namespace de4dot.cui {
	public class FilesDeobfuscator {
		Options options;
		IDeobfuscatorContext deobfuscatorContext = new DeobfuscatorContext();

		public class Options {
			public ModuleContext ModuleContext { get; set; }
			public IList<IDeobfuscatorInfo> DeobfuscatorInfos { get; set; }
			public IList<IObfuscatedFile> Files { get; set; }
			public IList<SearchDir> SearchDirs { get; set; }
			public MetaDataFlags MetaDataFlags { get; set; }
			public bool DetectObfuscators { get; set; }
			public RenamerFlags RenamerFlags { get; set; }
			public bool RenameSymbols { get; set; }
			public bool ControlFlowDeobfuscation { get; set; }
			public bool KeepObfuscatorTypes { get; set; }
			public bool OneFileAtATime { get; set; }
			public DecrypterType? DefaultStringDecrypterType { get; set; }
			public List<string> DefaultStringDecrypterMethods { get; private set; }
			public IAssemblyClientFactory AssemblyClientFactory { get; set; }

			public Options() {
				ModuleContext = new ModuleContext(TheAssemblyResolver.Instance);
				DeobfuscatorInfos = new List<IDeobfuscatorInfo>();
				Files = new List<IObfuscatedFile>();
				SearchDirs = new List<SearchDir>();
				DefaultStringDecrypterMethods = new List<string>();
				RenamerFlags = RenamerFlags.RenameNamespaces |
						RenamerFlags.RenameTypes |
						RenamerFlags.RenameProperties |
						RenamerFlags.RenameEvents |
						RenamerFlags.RenameFields |
						RenamerFlags.RenameMethods |
						RenamerFlags.RenameMethodArgs |
						RenamerFlags.RenameGenericParams |
						RenamerFlags.RestorePropertiesFromNames |
						RenamerFlags.RestoreEventsFromNames |
						RenamerFlags.RestoreProperties |
						RenamerFlags.RestoreEvents;
				RenameSymbols = true;
				ControlFlowDeobfuscation = true;
			}
		}

		public class SearchDir {
			public string InputDirectory { get; set; }
			public string OutputDirectory { get; set; }
			public bool SkipUnknownObfuscators { get; set; }
		}

		public FilesDeobfuscator(Options options) {
			this.options = options;
		}

		public void doIt(Stream memStream, bool doRename) {
            //if (options.DetectObfuscators)
            //    detectObfuscators();
            //else if (options.OneFileAtATime)
            //    deobfuscateOneAtATime();
            //else
            //    deobfuscateAll();

            var loader = new DotNetFileLoader(new DotNetFileLoader.Options
            {
                ModuleContext = options.ModuleContext,
                PossibleFiles = options.Files,
                SearchDirs = options.SearchDirs,
                CreateDeobfuscators = () => createDeobfuscators(),
                DefaultStringDecrypterType = options.DefaultStringDecrypterType,
                DefaultStringDecrypterMethods = options.DefaultStringDecrypterMethods,
                AssemblyClientFactory = options.AssemblyClientFactory,
                DeobfuscatorContext = deobfuscatorContext,
                ControlFlowDeobfuscation = options.ControlFlowDeobfuscation,
                KeepObfuscatorTypes = options.KeepObfuscatorTypes,
                MetaDataFlags = options.MetaDataFlags,
                RenamerFlags = options.RenamerFlags
            });

            foreach (var file in loader.load())
            {
                file.deobfuscateBegin();
                file.deobfuscate();
                file.deobfuscateEnd();
                if(doRename)
                    rename(new List<IObfuscatedFile> { file });
                file.save(memStream);
            }    
            
		}

		static void removeModule(ModuleDef module) {
			TheAssemblyResolver.Instance.Remove(module);
		}

		void detectObfuscators() {
			foreach (var file in loadAllFiles(true)) {
				removeModule(file.ModuleDefMD);
				file.Dispose();
				deobfuscatorContext.clear();
			}
		}

		void deobfuscateOneAtATime() {
			foreach (var file in loadAllFiles()) {
				int oldIndentLevel = Logger.Instance.IndentLevel;
				try {
					file.deobfuscateBegin();
					file.deobfuscate();
					file.deobfuscateEnd();
					rename(new List<IObfuscatedFile> { file });
					//file.save();

					removeModule(file.ModuleDefMD);
					TheAssemblyResolver.Instance.clearAll();
					deobfuscatorContext.clear();
				}
				catch (Exception ex) {
					Logger.Instance.Log(false, null, LoggerEvent.Warning, "Could not deobfuscate {0}. Use -v to see stack trace", file.Filename);
					Program.printStackTrace(ex, LoggerEvent.Verbose);
				}
				finally {
					file.Dispose();
					Logger.Instance.IndentLevel = oldIndentLevel;
				}
			}
		}

		void deobfuscateAll() {
			var allFiles = new List<IObfuscatedFile>(loadAllFiles());
			try {
				deobfuscateAllFiles(allFiles);
				rename(allFiles);
				saveAllFiles(allFiles);
			}
			finally {
				foreach (var file in allFiles) {
					if (file != null)
						file.Dispose();
				}
			}
		}

		IEnumerable<IObfuscatedFile> loadAllFiles() {
			return loadAllFiles(false);
		}

		IEnumerable<IObfuscatedFile> loadAllFiles(bool onlyScan) {
			var loader = new DotNetFileLoader(new DotNetFileLoader.Options {
				ModuleContext = options.ModuleContext,
				PossibleFiles  = options.Files,
				SearchDirs = options.SearchDirs,
				CreateDeobfuscators = () => createDeobfuscators(),
				DefaultStringDecrypterType = options.DefaultStringDecrypterType,
				DefaultStringDecrypterMethods = options.DefaultStringDecrypterMethods,
				AssemblyClientFactory = options.AssemblyClientFactory,
				DeobfuscatorContext = deobfuscatorContext,
				ControlFlowDeobfuscation = options.ControlFlowDeobfuscation,
				KeepObfuscatorTypes = options.KeepObfuscatorTypes,
				MetaDataFlags = options.MetaDataFlags,
				RenamerFlags = options.RenamerFlags,
				CreateDestinationDir = !onlyScan,
			});

			foreach (var file in loader.load())
				yield return file;
		}

		class DotNetFileLoader {
			Options options;
			Dictionary<string, bool> allFiles = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, bool> visitedDirectory = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

			public class Options {
				public ModuleContext ModuleContext { get; set; }
				public IEnumerable<IObfuscatedFile> PossibleFiles { get; set; }
				public IEnumerable<SearchDir> SearchDirs { get; set; }
				public Func<IList<IDeobfuscator>> CreateDeobfuscators { get; set; }
				public DecrypterType? DefaultStringDecrypterType { get; set; }
				public List<string> DefaultStringDecrypterMethods { get; set; }
				public IAssemblyClientFactory AssemblyClientFactory { get; set; }
				public IDeobfuscatorContext DeobfuscatorContext { get; set; }
				public bool ControlFlowDeobfuscation { get; set; }
				public bool KeepObfuscatorTypes { get; set; }
				public MetaDataFlags MetaDataFlags { get; set; }
				public RenamerFlags RenamerFlags { get; set; }
				public bool CreateDestinationDir { get; set; }
			}

			public DotNetFileLoader(Options options) {
				this.options = options;
			}

			public IEnumerable<IObfuscatedFile> load() {
				foreach (var file in options.PossibleFiles) {
					if (add(file, false, true))
						yield return file;
				}

				foreach (var searchDir in options.SearchDirs) {
					foreach (var file in loadFiles(searchDir))
						yield return file;
				}
			}

			bool add(IObfuscatedFile file, bool skipUnknownObfuscator, bool isFromPossibleFiles) {
				var key = Utils.getFullPath(file.Filename);
				if (allFiles.ContainsKey(key)) {
					Logger.Instance.Log(false, null, LoggerEvent.Warning, "Ingoring duplicate file: {0}", file.Filename);
					return false;
				}
				allFiles[key] = true;

				int oldIndentLevel = Logger.Instance.IndentLevel;
				try {
					file.DeobfuscatorContext = options.DeobfuscatorContext;
					file.load(options.CreateDeobfuscators());
				}
				catch (NotSupportedException) {
					return false;	// Eg. unsupported architecture
				}
				catch (BadImageFormatException) {
					if (isFromPossibleFiles)
						Logger.Instance.Log(false, null, LoggerEvent.Warning, "The file isn't a .NET PE file: {0}", file.Filename);
					return false;	// Not a .NET file
				}
				catch (EndOfStreamException) {
					return false;
				}
				catch (Exception ex) {
					Logger.Instance.Log(false, null, LoggerEvent.Warning, "Could not load file ({0}): {1}", ex.GetType(), file.Filename);
					return false;
				}
				finally {
					Logger.Instance.IndentLevel = oldIndentLevel;
				}

				var deob = file.Deobfuscator;
				if (skipUnknownObfuscator && deob.Type == "un") {
					Logger.v("Skipping unknown obfuscator: {0}", file.Filename);
					removeModule(file.ModuleDefMD);
					return false;
				}
				else {
					Logger.n("Detected {0} ({1})", deob.Name, file.Filename);
					if (options.CreateDestinationDir)
						createDirectories(Path.GetDirectoryName(file.NewFilename));
					return true;
				}
			}

			IEnumerable<IObfuscatedFile> loadFiles(SearchDir searchDir) {
				DirectoryInfo di = null;
				bool ok = false;
				try {
					di = new DirectoryInfo(searchDir.InputDirectory);
					if (di.Exists)
						ok = true;
				}
				catch (System.Security.SecurityException) {
				}
				catch (ArgumentException) {
				}
				if (ok) {
					foreach (var filename in doDirectoryInfo(searchDir, di)) {
						var obfuscatedFile = createObfuscatedFile(searchDir, filename);
						if (obfuscatedFile != null)
							yield return obfuscatedFile;
					}					
				}
			}

			IEnumerable<string> recursiveAdd(SearchDir searchDir, IEnumerable<FileSystemInfo> fileSystemInfos) {
				foreach (var fsi in fileSystemInfos) {
					if ((int)(fsi.Attributes & System.IO.FileAttributes.Directory) != 0) {
						foreach (var filename in doDirectoryInfo(searchDir, (DirectoryInfo)fsi))
							yield return filename;
					}
					else {
						var fi = (FileInfo)fsi;
						if (fi.Exists)
							yield return fi.FullName;
					}
				}
			}

			IEnumerable<string> doDirectoryInfo(SearchDir searchDir, DirectoryInfo di) {
				if (!di.Exists)
					return new List<string>();

				if (visitedDirectory.ContainsKey(di.FullName))
					return new List<string>();
				visitedDirectory[di.FullName] = true;

				FileSystemInfo[] fsinfos;
				try {
					fsinfos = di.GetFileSystemInfos();
				}
				catch (UnauthorizedAccessException) {
					return new List<string>();
				}
				catch (IOException) {
					return new List<string>();
				}
				return recursiveAdd(searchDir, fsinfos);
			}

			IObfuscatedFile createObfuscatedFile(SearchDir searchDir, string filename) {
				var fileOptions = new ObfuscatedFile.Options {
					Filename = Utils.getFullPath(filename),
					ControlFlowDeobfuscation = options.ControlFlowDeobfuscation,
					KeepObfuscatorTypes = options.KeepObfuscatorTypes,
					MetaDataFlags = options.MetaDataFlags,
					RenamerFlags = options.RenamerFlags,
				};
				if (options.DefaultStringDecrypterType != null)
					fileOptions.StringDecrypterType = options.DefaultStringDecrypterType.Value;
				fileOptions.StringDecrypterMethods.AddRange(options.DefaultStringDecrypterMethods);

				if (!string.IsNullOrEmpty(searchDir.OutputDirectory)) {
					var inDir = Utils.getFullPath(searchDir.InputDirectory);
					var outDir = Utils.getFullPath(searchDir.OutputDirectory);

					if (!Utils.StartsWith(fileOptions.Filename, inDir, StringComparison.OrdinalIgnoreCase))
						throw new UserException(string.Format("Filename {0} does not start with inDir {1}", fileOptions.Filename, inDir));

					var subDirs = fileOptions.Filename.Substring(inDir.Length);
					if (subDirs.Length > 0 && subDirs[0] == Path.DirectorySeparatorChar)
						subDirs = subDirs.Substring(1);
					fileOptions.NewFilename = Utils.getFullPath(Path.Combine(outDir, subDirs));

					if (fileOptions.Filename.Equals(fileOptions.NewFilename, StringComparison.OrdinalIgnoreCase))
						throw new UserException(string.Format("Input and output filename is the same: {0}", fileOptions.Filename));
				}

				var obfuscatedFile = new ObfuscatedFile(fileOptions, options.ModuleContext, options.AssemblyClientFactory);
				if (add(obfuscatedFile, searchDir.SkipUnknownObfuscators, false))
					return obfuscatedFile;
				obfuscatedFile.Dispose();
				return null;
			}

			void createDirectories(string path) {
				if (string.IsNullOrEmpty(path))
					return;
				try {
					var di = new DirectoryInfo(path);
					if (!di.Exists)
						di.Create();
				}
				catch (System.Security.SecurityException) {
				}
				catch (ArgumentException) {
				}
			}
		}

		void deobfuscateAllFiles(IEnumerable<IObfuscatedFile> allFiles) {
			try {
				foreach (var file in allFiles)
					file.deobfuscateBegin();
				foreach (var file in allFiles) {
					file.deobfuscate();
					file.deobfuscateEnd();
				}
			}
			finally {
				foreach (var file in allFiles)
					file.deobfuscateCleanUp();
			}
		}

		void saveAllFiles(IEnumerable<IObfuscatedFile> allFiles) {
			//foreach (var file in allFiles)
				//file.save();
		}

		IList<IDeobfuscator> createDeobfuscators() {
			var list = new List<IDeobfuscator>(options.DeobfuscatorInfos.Count);
			foreach (var info in options.DeobfuscatorInfos)
				list.Add(info.createDeobfuscator());
			return list;
		}

		void rename(IEnumerable<IObfuscatedFile> theFiles) {
			if (!options.RenameSymbols)
				return;
			var renamer = new Renamer(deobfuscatorContext, theFiles, options.RenamerFlags);
			renamer.rename();
		}
	}
}

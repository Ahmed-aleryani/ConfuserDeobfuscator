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
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using de4dot.blocks;

namespace de4dot.code {
	class MethodPrinter {
		LoggerEvent loggerEvent;
		IList<Instruction> allInstructions;
		IList<ExceptionHandler> allExceptionHandlers;
		Dictionary<Instruction, bool> targets = new Dictionary<Instruction, bool>();
		Dictionary<Instruction, string> labels = new Dictionary<Instruction, string>();

		class ExInfo {
			public List<ExceptionHandler> tryStarts = new List<ExceptionHandler>();
			public List<ExceptionHandler> tryEnds = new List<ExceptionHandler>();
			public List<ExceptionHandler> filterStarts = new List<ExceptionHandler>();
			public List<ExceptionHandler> handlerStarts = new List<ExceptionHandler>();
			public List<ExceptionHandler> handlerEnds = new List<ExceptionHandler>();
		}
		Dictionary<Instruction, ExInfo> exInfos = new Dictionary<Instruction, ExInfo>();
		ExInfo lastExInfo;

		public void print(LoggerEvent loggerEvent, IList<Instruction> allInstructions, IList<ExceptionHandler> allExceptionHandlers) {
			try {
				this.loggerEvent = loggerEvent;
				this.allInstructions = allInstructions;
				this.allExceptionHandlers = allExceptionHandlers;
				lastExInfo = new ExInfo();
				print();
			}
			finally {
				this.allInstructions = null;
				this.allExceptionHandlers = null;
				targets.Clear();
				labels.Clear();
				exInfos.Clear();
				lastExInfo = null;
			}
		}

		void initTargets() {
			foreach (var instr in allInstructions) {
				switch (instr.OpCode.OperandType) {
				case OperandType.ShortInlineBrTarget:
				case OperandType.InlineBrTarget:
					setTarget(instr.Operand as Instruction);
					break;

				case OperandType.InlineSwitch:
					foreach (var targetInstr in (Instruction[])instr.Operand)
						setTarget(targetInstr);
					break;
				}
			}

			foreach (var ex in allExceptionHandlers) {
				setTarget(ex.TryStart);
				setTarget(ex.TryEnd);
				setTarget(ex.FilterStart);
				setTarget(ex.HandlerStart);
				setTarget(ex.HandlerEnd);
			}

			var sortedTargets = new List<Instruction>(targets.Keys);
			sortedTargets.Sort((a, b) => a.Offset.CompareTo(b.Offset));
			for (int i = 0; i < sortedTargets.Count; i++)
				labels[sortedTargets[i]] = string.Format("label_{0}", i);
		}

		void setTarget(Instruction instr) {
			if (instr != null)
				targets[instr] = true;
		}

		void initExHandlers() {
			foreach (var ex in allExceptionHandlers) {
				if (ex.TryStart != null) {
					getExInfo(ex.TryStart).tryStarts.Add(ex);
					getExInfo(ex.TryEnd).tryEnds.Add(ex);
				}
				if (ex.FilterStart != null)
					getExInfo(ex.FilterStart).filterStarts.Add(ex);
				if (ex.HandlerStart != null) {
					getExInfo(ex.HandlerStart).handlerStarts.Add(ex);
					getExInfo(ex.HandlerEnd).handlerEnds.Add(ex);
				}
			}
		}

		ExInfo getExInfo(Instruction instruction) {
			if (instruction == null)
				return lastExInfo;
			ExInfo exInfo;
			if (!exInfos.TryGetValue(instruction, out exInfo))
				exInfos[instruction] = exInfo = new ExInfo();
			return exInfo;
		}

		void print() {
			initTargets();
			initExHandlers();

			Logger.Instance.indent();
			foreach (var instr in allInstructions) {
				if (targets.ContainsKey(instr)) {
					Logger.Instance.deIndent();
					Logger.log(loggerEvent, "{0}:", getLabel(instr));
					Logger.Instance.indent();
				}
				ExInfo exInfo;
				if (exInfos.TryGetValue(instr, out exInfo))
					printExInfo(exInfo);
				var instrString = instr.OpCode.Name;
				var operandString = getOperandString(instr);
				var memberRef = instr.Operand as ITokenOperand;
				if (operandString == "")
					Logger.log(loggerEvent, "{0}", instrString);
				else if (memberRef != null)
					Logger.log(loggerEvent, "{0,-9} {1} // {2:X8}", instrString, Utils.removeNewlines(operandString), memberRef.MDToken.ToUInt32());
				else
					Logger.log(loggerEvent, "{0,-9} {1}", instrString, Utils.removeNewlines(operandString));
			}
			printExInfo(lastExInfo);
			Logger.Instance.deIndent();
		}

		string getOperandString(Instruction instr) {
			if (instr.Operand is Instruction)
				return getLabel((Instruction)instr.Operand);
			else if (instr.Operand is Instruction[]) {
				var sb = new StringBuilder();
				var targets = (Instruction[])instr.Operand;
				for (int i = 0; i < targets.Length; i++) {
					if (i > 0)
						sb.Append(',');
					sb.Append(getLabel(targets[i]));
				}
				return sb.ToString();
			}
			else if (instr.Operand is string)
				return Utils.toCsharpString((string)instr.Operand);
			else if (instr.Operand is Parameter) {
				var arg = (Parameter)instr.Operand;
				var s = InstructionPrinter.GetOperandString(instr);
				if (s != "")
					return s;
				return string.Format("<arg_{0}>", arg.Index);
			}
			else
				return InstructionPrinter.GetOperandString(instr);
		}

		void printExInfo(ExInfo exInfo) {
			Logger.Instance.deIndent();
			foreach (var ex in exInfo.tryStarts)
				Logger.log(loggerEvent, "// try start: {0}", getExceptionString(ex));
			foreach (var ex in exInfo.tryEnds)
				Logger.log(loggerEvent, "// try end: {0}", getExceptionString(ex));
			foreach (var ex in exInfo.filterStarts)
				Logger.log(loggerEvent, "// filter start: {0}", getExceptionString(ex));
			foreach (var ex in exInfo.handlerStarts)
				Logger.log(loggerEvent, "// handler start: {0}", getExceptionString(ex));
			foreach (var ex in exInfo.handlerEnds)
				Logger.log(loggerEvent, "// handler end: {0}", getExceptionString(ex));
			Logger.Instance.indent();
		}

		string getExceptionString(ExceptionHandler ex) {
			var sb = new StringBuilder();
			if (ex.TryStart != null)
				sb.Append(string.Format("TRY: {0}-{1}", getLabel(ex.TryStart), getLabel(ex.TryEnd)));
			if (ex.FilterStart != null)
				sb.Append(string.Format(", FILTER: {0}", getLabel(ex.FilterStart)));
			if (ex.HandlerStart != null)
				sb.Append(string.Format(", HANDLER: {0}-{1}", getLabel(ex.HandlerStart), getLabel(ex.HandlerEnd)));
			sb.Append(string.Format(", TYPE: {0}", ex.HandlerType));
			if (ex.CatchType != null)
				sb.Append(string.Format(", CATCH: {0}", ex.CatchType));
			return sb.ToString();
		}

		string getLabel(Instruction instr) {
			if (instr == null)
				return "<end>";
			return labels[instr];
		}
	}
}

/*
    Copyright (C) 2012-2013 de4dot@gmail.com

    Permission is hereby granted, free of charge, to any person obtaining
    a copy of this software and associated documentation files (the
    "Software"), to deal in the Software without restriction, including
    without limitation the rights to use, copy, modify, merge, publish,
    distribute, sublicense, and/or sell copies of the Software, and to
    permit persons to whom the Software is furnished to do so, subject to
    the following conditions:

    The above copyright notice and this permission notice shall be
    included in all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
    EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
    MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
    IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
    CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
    TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
    SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

﻿using System;
using System.Diagnostics;
using dnlib.Utils;
using dnlib.DotNet.MD;

namespace dnlib.DotNet {
	/// <summary>
	/// A high-level representation of a row in the Param table
	/// </summary>
	[DebuggerDisplay("{Sequence} {Name}")]
	public abstract class ParamDef : IHasConstant, IHasCustomAttribute, IHasFieldMarshal {
		/// <summary>
		/// The row id in its table
		/// </summary>
		protected uint rid;

		/// <inheritdoc/>
		public MDToken MDToken {
			get { return new MDToken(Table.Param, rid); }
		}

		/// <inheritdoc/>
		public uint Rid {
			get { return rid; }
			set { rid = value; }
		}

		/// <inheritdoc/>
		public int HasConstantTag {
			get { return 1; }
		}

		/// <inheritdoc/>
		public int HasCustomAttributeTag {
			get { return 4; }
		}

		/// <inheritdoc/>
		public int HasFieldMarshalTag {
			get { return 1; }
		}

		/// <summary>
		/// Gets the declaring method
		/// </summary>
		public abstract MethodDef DeclaringMethod { get; internal set; }

		/// <summary>
		/// From column Param.Flags
		/// </summary>
		public abstract ParamAttributes Attributes { get; set; }

		/// <summary>
		/// From column Param.Sequence
		/// </summary>
		public abstract ushort Sequence { get; set; }

		/// <summary>
		/// From column Param.Name
		/// </summary>
		public abstract UTF8String Name { get; set; }

		/// <inheritdoc/>
		public abstract FieldMarshal FieldMarshal { get; set; }

		/// <inheritdoc/>
		public abstract Constant Constant { get; set; }

		/// <summary>
		/// Gets all custom attributes
		/// </summary>
		public abstract CustomAttributeCollection CustomAttributes { get; }

		/// <inheritdoc/>
		public bool HasCustomAttributes {
			get { return CustomAttributes.Count > 0; }
		}

		/// <summary>
		/// <c>true</c> if <see cref="Constant"/> is not <c>null</c>
		/// </summary>
		public bool HasConstant {
			get { return Constant != null; }
		}

		/// <summary>
		/// Gets the constant element type or <see cref="dnlib.DotNet.ElementType.End"/> if there's no constant
		/// </summary>
		public ElementType ElementType {
			get { return Constant == null ? ElementType.End : Constant.Type; }
		}

		/// <summary>
		/// <c>true</c> if <see cref="FieldMarshal"/> is not <c>null</c>
		/// </summary>
		public bool HasMarshalInfo {
			get { return FieldMarshal != null; }
		}

		/// <inheritdoc/>
		public string FullName {
			get {
				var name = Name;
				if (UTF8String.IsNullOrEmpty(name))
					return string.Format("A_{0}", Sequence);
				return name.String;
			}
		}

		/// <summary>
		/// Gets/sets the <see cref="ParamAttributes.In"/> bit
		/// </summary>
		public bool IsIn {
			get { return (Attributes & ParamAttributes.In) != 0; }
			set {
				if (value)
					Attributes |= ParamAttributes.In;
				else
					Attributes &= ~ParamAttributes.In;
			}
		}

		/// <summary>
		/// Gets/sets the <see cref="ParamAttributes.Out"/> bit
		/// </summary>
		public bool IsOut {
			get { return (Attributes & ParamAttributes.Out) != 0; }
			set {
				if (value)
					Attributes |= ParamAttributes.Out;
				else
					Attributes &= ~ParamAttributes.Out;
			}
		}

		/// <summary>
		/// Gets/sets the <see cref="ParamAttributes.Optional"/> bit
		/// </summary>
		public bool IsOptional {
			get { return (Attributes & ParamAttributes.Optional) != 0; }
			set {
				if (value)
					Attributes |= ParamAttributes.Optional;
				else
					Attributes &= ~ParamAttributes.Optional;
			}
		}

		/// <summary>
		/// Gets/sets the <see cref="ParamAttributes.HasDefault"/> bit
		/// </summary>
		public bool HasDefault {
			get { return (Attributes & ParamAttributes.HasDefault) != 0; }
			set {
				if (value)
					Attributes |= ParamAttributes.HasDefault;
				else
					Attributes &= ~ParamAttributes.HasDefault;
			}
		}

		/// <summary>
		/// Gets/sets the <see cref="ParamAttributes.HasFieldMarshal"/> bit
		/// </summary>
		public bool HasFieldMarshal {
			get { return (Attributes & ParamAttributes.HasFieldMarshal) != 0; }
			set {
				if (value)
					Attributes |= ParamAttributes.HasFieldMarshal;
				else
					Attributes &= ~ParamAttributes.HasFieldMarshal;
			}
		}
	}

	/// <summary>
	/// A Param row created by the user and not present in the original .NET file
	/// </summary>
	public class ParamDefUser : ParamDef {
		MethodDef declaringMethod;
		ParamAttributes flags;
		ushort sequence;
		UTF8String name;
		FieldMarshal fieldMarshal;
		Constant constant;
		CustomAttributeCollection customAttributeCollection = new CustomAttributeCollection();

		/// <inheritdoc/>
		public override MethodDef DeclaringMethod {
			get { return declaringMethod; }
			internal set { declaringMethod = value; }
		}

		/// <inheritdoc/>
		public override ParamAttributes Attributes {
			get { return flags; }
			set { flags = value; }
		}

		/// <inheritdoc/>
		public override ushort Sequence {
			get { return sequence; }
			set { sequence = value; }
		}

		/// <inheritdoc/>
		public override UTF8String Name {
			get { return name; }
			set { name = value; }
		}

		/// <inheritdoc/>
		public override FieldMarshal FieldMarshal {
			get { return fieldMarshal; }
			set { fieldMarshal = value; }
		}

		/// <inheritdoc/>
		public override Constant Constant {
			get { return constant; }
			set { constant = value; }
		}

		/// <inheritdoc/>
		public override CustomAttributeCollection CustomAttributes {
			get { return customAttributeCollection; }
		}

		/// <summary>
		/// Default constructor
		/// </summary>
		public ParamDefUser() {
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="name">Name</param>
		public ParamDefUser(UTF8String name)
			: this(name, 0) {
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="name">Name</param>
		/// <param name="sequence">Sequence</param>
		public ParamDefUser(UTF8String name, ushort sequence)
			: this(name, sequence, 0) {
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="name">Name</param>
		/// <param name="sequence">Sequence</param>
		/// <param name="flags">Flags</param>
		public ParamDefUser(UTF8String name, ushort sequence, ParamAttributes flags) {
			this.name = name;
			this.sequence = sequence;
			this.flags = flags;
		}
	}

	/// <summary>
	/// Created from a row in the Param table
	/// </summary>
	sealed class ParamDefMD : ParamDef {
		/// <summary>The module where this instance is located</summary>
		ModuleDefMD readerModule;
		/// <summary>The raw table row. It's <c>null</c> until <see cref="InitializeRawRow"/> is called</summary>
		RawParamRow rawRow;

		UserValue<MethodDef> declaringMethod;
		UserValue<ParamAttributes> flags;
		UserValue<ushort> sequence;
		UserValue<UTF8String> name;
		UserValue<FieldMarshal> fieldMarshal;
		UserValue<Constant> constant;
		CustomAttributeCollection customAttributeCollection;

		/// <inheritdoc/>
		public override MethodDef DeclaringMethod {
			get { return declaringMethod.Value; }
			internal set { declaringMethod.Value = value; }
		}

		/// <inheritdoc/>
		public override ParamAttributes Attributes {
			get { return flags.Value; }
			set { flags.Value = value; }
		}

		/// <inheritdoc/>
		public override ushort Sequence {
			get { return sequence.Value; }
			set { sequence.Value = value; }
		}

		/// <inheritdoc/>
		public override UTF8String Name {
			get { return name.Value; }
			set { name.Value = value; }
		}

		/// <inheritdoc/>
		public override FieldMarshal FieldMarshal {
			get { return fieldMarshal.Value; }
			set { fieldMarshal.Value = value; }
		}

		/// <inheritdoc/>
		public override Constant Constant {
			get { return constant.Value; }
			set { constant.Value = value; }
		}

		/// <inheritdoc/>
		public override CustomAttributeCollection CustomAttributes {
			get {
				if (customAttributeCollection == null) {
					var list = readerModule.MetaData.GetCustomAttributeRidList(Table.Param, rid);
					customAttributeCollection = new CustomAttributeCollection((int)list.Length, list, (list2, index) => readerModule.ReadCustomAttribute(((RidList)list2)[index]));
				}
				return customAttributeCollection;
			}
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="readerModule">The module which contains this <c>Param</c> row</param>
		/// <param name="rid">Row ID</param>
		/// <exception cref="ArgumentNullException">If <paramref name="readerModule"/> is <c>null</c></exception>
		/// <exception cref="ArgumentException">If <paramref name="rid"/> is invalid</exception>
		public ParamDefMD(ModuleDefMD readerModule, uint rid) {
#if DEBUG
			if (readerModule == null)
				throw new ArgumentNullException("readerModule");
			if (readerModule.TablesStream.ParamTable.IsInvalidRID(rid))
				throw new BadImageFormatException(string.Format("Param rid {0} does not exist", rid));
#endif
			this.rid = rid;
			this.readerModule = readerModule;
			Initialize();
		}

		void Initialize() {
			declaringMethod.ReadOriginalValue = () => {
				return readerModule.GetOwner(this);
			};
			flags.ReadOriginalValue = () => {
				InitializeRawRow();
				return (ParamAttributes)rawRow.Flags;
			};
			sequence.ReadOriginalValue = () => {
				InitializeRawRow();
				return rawRow.Sequence;
			};
			name.ReadOriginalValue = () => {
				InitializeRawRow();
				return readerModule.StringsStream.ReadNoNull(rawRow.Name);
			};
			fieldMarshal.ReadOriginalValue = () => {
				return readerModule.ResolveFieldMarshal(readerModule.MetaData.GetFieldMarshalRid(Table.Param, rid));
			};
			constant.ReadOriginalValue = () => {
				return readerModule.ResolveConstant(readerModule.MetaData.GetConstantRid(Table.Param, rid));
			};
		}

		void InitializeRawRow() {
			if (rawRow != null)
				return;
			rawRow = readerModule.TablesStream.ReadParamRow(rid);
		}

		internal ParamDefMD InitializeAll() {
			MemberMDInitializer.Initialize(DeclaringMethod);
			MemberMDInitializer.Initialize(Attributes);
			MemberMDInitializer.Initialize(Sequence);
			MemberMDInitializer.Initialize(Name);
			MemberMDInitializer.Initialize(FieldMarshal);
			MemberMDInitializer.Initialize(Constant);
			MemberMDInitializer.Initialize(CustomAttributes);
			return this;
		}
	}
}

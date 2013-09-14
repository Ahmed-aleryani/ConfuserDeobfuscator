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
	/// A high-level representation of a row in the DeclSecurity table
	/// </summary>
	[DebuggerDisplay("{Action} {Parent}")]
	public abstract class DeclSecurity : IHasCustomAttribute {
		/// <summary>
		/// The row id in its table
		/// </summary>
		protected uint rid;

		/// <inheritdoc/>
		public MDToken MDToken {
			get { return new MDToken(Table.DeclSecurity, rid); }
		}

		/// <inheritdoc/>
		public uint Rid {
			get { return rid; }
			set { rid = value; }
		}

		/// <inheritdoc/>
		public int HasCustomAttributeTag {
			get { return 8; }
		}

		/// <summary>
		/// From column DeclSecurity.Action
		/// </summary>
		public abstract DeclSecurityAction Action { get; set; }

		/// <summary>
		/// From column DeclSecurity.PermissionSet
		/// </summary>
		public abstract byte[] PermissionSet { get; set; }

		/// <summary>
		/// Gets all custom attributes
		/// </summary>
		public abstract CustomAttributeCollection CustomAttributes { get; }

		/// <inheritdoc/>
		public bool HasCustomAttributes {
			get { return CustomAttributes.Count > 0; }
		}
	}

	/// <summary>
	/// A DeclSecurity row created by the user and not present in the original .NET file
	/// </summary>
	public class DeclSecurityUser : DeclSecurity {
		DeclSecurityAction action;
		byte[] permissionSet;
		CustomAttributeCollection customAttributeCollection = new CustomAttributeCollection();

		/// <inheritdoc/>
		public override DeclSecurityAction Action {
			get { return action; }
			set { action = value; }
		}

		/// <inheritdoc/>
		public override byte[] PermissionSet {
			get { return permissionSet; }
			set { permissionSet = value; }
		}

		/// <inheritdoc/>
		public override CustomAttributeCollection CustomAttributes {
			get { return customAttributeCollection; }
		}

		/// <summary>
		/// Default constructor
		/// </summary>
		public DeclSecurityUser() {
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="action">The security action</param>
		/// <param name="permissionSet">The permission set</param>
		public DeclSecurityUser(DeclSecurityAction action, byte[] permissionSet) {
			this.action = action;
			this.permissionSet = permissionSet;
		}
	}

	/// <summary>
	/// Created from a row in the DeclSecurity table
	/// </summary>
	sealed class DeclSecurityMD : DeclSecurity {
		/// <summary>The module where this instance is located</summary>
		ModuleDefMD readerModule;
		/// <summary>The raw table row. It's <c>null</c> until <see cref="InitializeRawRow"/> is called</summary>
		RawDeclSecurityRow rawRow;

		UserValue<DeclSecurityAction> action;
		UserValue<byte[]> permissionSet;
		CustomAttributeCollection customAttributeCollection;

		/// <inheritdoc/>
		public override DeclSecurityAction Action {
			get { return action.Value; }
			set { action.Value = value; }
		}

		/// <inheritdoc/>
		public override byte[] PermissionSet {
			get { return permissionSet.Value; }
			set { permissionSet.Value = value; }
		}

		/// <inheritdoc/>
		public override CustomAttributeCollection CustomAttributes {
			get {
				if (customAttributeCollection == null) {
					var list = readerModule.MetaData.GetCustomAttributeRidList(Table.DeclSecurity, rid);
					customAttributeCollection = new CustomAttributeCollection((int)list.Length, list, (list2, index) => readerModule.ReadCustomAttribute(((RidList)list2)[index]));
				}
				return customAttributeCollection;
			}
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="readerModule">The module which contains this <c>DeclSecurity</c> row</param>
		/// <param name="rid">Row ID</param>
		/// <exception cref="ArgumentNullException">If <paramref name="readerModule"/> is <c>null</c></exception>
		/// <exception cref="ArgumentException">If <paramref name="rid"/> is invalid</exception>
		public DeclSecurityMD(ModuleDefMD readerModule, uint rid) {
#if DEBUG
			if (readerModule == null)
				throw new ArgumentNullException("readerModule");
			if (readerModule.TablesStream.DeclSecurityTable.IsInvalidRID(rid))
				throw new BadImageFormatException(string.Format("DeclSecurity rid {0} does not exist", rid));
#endif
			this.rid = rid;
			this.readerModule = readerModule;
			Initialize();
		}

		void Initialize() {
			action.ReadOriginalValue = () => {
				InitializeRawRow();
				return (DeclSecurityAction)rawRow.Action;
			};
			permissionSet.ReadOriginalValue = () => {
				InitializeRawRow();
				return readerModule.BlobStream.Read(rawRow.PermissionSet);
			};
		}

		void InitializeRawRow() {
			if (rawRow != null)
				return;
			rawRow = readerModule.TablesStream.ReadDeclSecurityRow(rid);
		}
	}
}

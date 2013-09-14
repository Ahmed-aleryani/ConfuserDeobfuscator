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
using System.Collections.Generic;
using dnlib.PE;

namespace dnlib.DotNet.MD {
	/// <summary>
	/// Common base class for #~ and #- metadata classes
	/// </summary>
	abstract class MetaData : IMetaData {
		/// <summary>
		/// The PE image
		/// </summary>
		protected IPEImage peImage;

		/// <summary>
		/// The .NET header
		/// </summary>
		protected ImageCor20Header cor20Header;

		/// <summary>
		/// The MD header
		/// </summary>
		protected MetaDataHeader mdHeader;

		/// <summary>
		/// The #Strings stream
		/// </summary>
		protected StringsStream stringsStream;

		/// <summary>
		/// The #US stream
		/// </summary>
		protected USStream usStream;

		/// <summary>
		/// The #Blob stream
		/// </summary>
		protected BlobStream blobStream;

		/// <summary>
		/// The #GUID stream
		/// </summary>
		protected GuidStream guidStream;

		/// <summary>
		/// The #~ or #- stream
		/// </summary>
		protected TablesStream tablesStream;

		/// <summary>
		/// All the streams that are present in the PE image
		/// </summary>
		protected List<DotNetStream> allStreams;

		uint[] fieldRidToTypeDefRid;
		uint[] methodRidToTypeDefRid;
		uint[] eventRidToTypeDefRid;
		uint[] propertyRidToTypeDefRid;
		uint[] gpRidToOwnerRid;
		uint[] gpcRidToOwnerRid;
		uint[] paramRidToOwnerRid;
		Dictionary<uint, RandomRidList> typeDefRidToNestedClasses;
		RandomRidList nonNestedTypes;

		/// <inheritdoc/>
		public ImageCor20Header ImageCor20Header {
			get { return cor20Header; }
		}

		/// <inheritdoc/>
		public ushort MajorVersion {
			get { return mdHeader.MajorVersion; }
		}

		/// <inheritdoc/>
		public ushort MinorVersion {
			get { return mdHeader.MinorVersion; }
		}

		/// <inheritdoc/>
		public string VersionString {
			get { return mdHeader.VersionString; }
		}

		/// <inheritdoc/>
		public IPEImage PEImage {
			get { return peImage; }
		}

		/// <inheritdoc/>
		public MetaDataHeader MetaDataHeader {
			get { return mdHeader; }
		}

		/// <inheritdoc/>
		public StringsStream StringsStream {
			get { return stringsStream; }
		}

		/// <inheritdoc/>
		public USStream USStream {
			get { return usStream; }
		}

		/// <inheritdoc/>
		public BlobStream BlobStream {
			get { return blobStream; }
		}

		/// <inheritdoc/>
		public GuidStream GuidStream {
			get { return guidStream; }
		}

		/// <inheritdoc/>
		public TablesStream TablesStream {
			get { return tablesStream; }
		}

		/// <inheritdoc/>
		public IList<DotNetStream> AllStreams {
			get { return allStreams; }
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="peImage">The PE image</param>
		/// <param name="cor20Header">The .NET header</param>
		/// <param name="mdHeader">The MD header</param>
		protected MetaData(IPEImage peImage, ImageCor20Header cor20Header, MetaDataHeader mdHeader) {
			try {
				this.allStreams = new List<DotNetStream>();
				this.peImage = peImage;
				this.cor20Header = cor20Header;
				this.mdHeader = mdHeader;
			}
			catch {
				if (peImage != null)
					peImage.Dispose();
				throw;
			}
		}

		/// <summary>
		/// Initializes the metadata, tables, streams
		/// </summary>
		public void Initialize() {
			Initialize2();

			if (tablesStream == null)
				throw new BadImageFormatException("Missing MD stream");
			if (stringsStream == null)
				stringsStream = new StringsStream();
			if (usStream == null)
				usStream = new USStream();
			if (blobStream == null)
				blobStream = new BlobStream();
			if (guidStream == null)
				guidStream = new GuidStream();
		}

		/// <summary>
		/// Called by <see cref="Initialize()"/>
		/// </summary>
		protected abstract void Initialize2();

		/// <inheritdoc/>
		public virtual RidList GetTypeDefRidList() {
			return new ContiguousRidList(1, tablesStream.TypeDefTable.Rows);
		}

		/// <inheritdoc/>
		public virtual RidList GetExportedTypeRidList() {
			return new ContiguousRidList(1, tablesStream.ExportedTypeTable.Rows);
		}

		/// <inheritdoc/>
		public abstract RidList GetFieldRidList(uint typeDefRid);

		/// <inheritdoc/>
		public abstract RidList GetMethodRidList(uint typeDefRid);

		/// <inheritdoc/>
		public abstract RidList GetParamRidList(uint methodRid);

		/// <inheritdoc/>
		public abstract RidList GetEventRidList(uint eventMapRid);

		/// <inheritdoc/>
		public abstract RidList GetPropertyRidList(uint propertyMapRid);

		/// <summary>
		/// Binary searches the table for a <c>rid</c> whose key column at index
		/// <paramref name="keyColIndex"/> is equal to <paramref name="key"/>.
		/// </summary>
		/// <param name="tableSource">Table to search</param>
		/// <param name="keyColIndex">Key column index</param>
		/// <param name="key">Key</param>
		/// <returns>The <c>rid</c> of the found row, or 0 if none found</returns>
		protected abstract uint BinarySearch(MDTable tableSource, int keyColIndex, uint key);

		/// <summary>
		/// Finds all rows owned by <paramref name="key"/> in table <paramref name="tableSource"/>
		/// whose index is <paramref name="keyColIndex"/>
		/// </summary>
		/// <param name="tableSource">Table to search</param>
		/// <param name="keyColIndex">Key column index</param>
		/// <param name="key">Key</param>
		/// <returns>A <see cref="RidList"/> instance</returns>
		protected RidList FindAllRows(MDTable tableSource, int keyColIndex, uint key) {
			uint startRid = BinarySearch(tableSource, keyColIndex, key);
			if (tableSource == null || tableSource.IsInvalidRID(startRid))
				return ContiguousRidList.Empty;
			uint endRid = startRid + 1;
			for (; startRid > 1; startRid--) {
				uint key2;
				if (!tablesStream.ReadColumn(tableSource, startRid - 1, keyColIndex, out key2))
					break;	// Should never happen since startRid is valid
				if (key != key2)
					break;
			}
			for (; endRid <= tableSource.Rows; endRid++) {
				uint key2;
				if (!tablesStream.ReadColumn(tableSource, endRid, keyColIndex, out key2))
					break;	// Should never happen since endRid is valid
				if (key != key2)
					break;
			}
			return new ContiguousRidList(startRid, endRid - startRid);
		}

		/// <summary>
		/// Finds all rows owned by <paramref name="key"/> in table <paramref name="tableSource"/>
		/// whose index is <paramref name="keyColIndex"/>. Should be called if <paramref name="tableSource"/>
		/// could be unsorted.
		/// </summary>
		/// <param name="tableSource">Table to search</param>
		/// <param name="keyColIndex">Key column index</param>
		/// <param name="key">Key</param>
		/// <returns>A <see cref="RidList"/> instance</returns>
		protected virtual RidList FindAllRowsUnsorted(MDTable tableSource, int keyColIndex, uint key) {
			return FindAllRows(tableSource, keyColIndex, key);
		}

		/// <inheritdoc/>
		public RidList GetInterfaceImplRidList(uint typeDefRid) {
			var tbl = tablesStream.TypeDefTable;
			if (tbl == null || tbl.IsInvalidRID(typeDefRid))
				return ContiguousRidList.Empty;
			return FindAllRowsUnsorted(tablesStream.InterfaceImplTable, 0, typeDefRid);
		}

		/// <inheritdoc/>
		public RidList GetGenericParamRidList(Table table, uint rid) {
			var tbl = tablesStream.Get(table);
			if (tbl == null || tbl.IsInvalidRID(rid))
				return ContiguousRidList.Empty;
			uint codedToken;
			if (!CodedToken.TypeOrMethodDef.Encode(new MDToken(table, rid), out codedToken))
				return ContiguousRidList.Empty;
			// Sorted or not, the CLR only searches this table as if it were sorted.
			return FindAllRows(tablesStream.GenericParamTable, 2, codedToken);
		}

		/// <inheritdoc/>
		public RidList GetGenericParamConstraintRidList(uint genericParamRid) {
			var tbl = tablesStream.GenericParamTable;
			if (tbl == null || tbl.IsInvalidRID(genericParamRid))
				return ContiguousRidList.Empty;
			return FindAllRowsUnsorted(tablesStream.GenericParamConstraintTable, 0, genericParamRid);
		}

		/// <inheritdoc/>
		public RidList GetCustomAttributeRidList(Table table, uint rid) {
			var tbl = tablesStream.Get(table);
			if (tbl == null || tbl.IsInvalidRID(rid))
				return ContiguousRidList.Empty;
			uint codedToken;
			if (!CodedToken.HasCustomAttribute.Encode(new MDToken(table, rid), out codedToken))
				return ContiguousRidList.Empty;
			return FindAllRowsUnsorted(tablesStream.CustomAttributeTable, 0, codedToken);
		}

		/// <inheritdoc/>
		public RidList GetDeclSecurityRidList(Table table, uint rid) {
			var tbl = tablesStream.Get(table);
			if (tbl == null || tbl.IsInvalidRID(rid))
				return ContiguousRidList.Empty;
			uint codedToken;
			if (!CodedToken.HasDeclSecurity.Encode(new MDToken(table, rid), out codedToken))
				return ContiguousRidList.Empty;
			return FindAllRowsUnsorted(tablesStream.DeclSecurityTable, 1, codedToken);
		}

		/// <inheritdoc/>
		public RidList GetMethodSemanticsRidList(Table table, uint rid) {
			var tbl = tablesStream.Get(table);
			if (tbl == null || tbl.IsInvalidRID(rid))
				return ContiguousRidList.Empty;
			uint codedToken;
			if (!CodedToken.HasSemantic.Encode(new MDToken(table, rid), out codedToken))
				return ContiguousRidList.Empty;
			return FindAllRowsUnsorted(tablesStream.MethodSemanticsTable, 2, codedToken);
		}

		/// <inheritdoc/>
		public RidList GetMethodImplRidList(uint typeDefRid) {
			var tbl = tablesStream.TypeDefTable;
			if (tbl == null || tbl.IsInvalidRID(typeDefRid))
				return ContiguousRidList.Empty;
			return FindAllRowsUnsorted(tablesStream.MethodImplTable, 0, typeDefRid);
		}

		/// <inheritdoc/>
		public uint GetClassLayoutRid(uint typeDefRid) {
			var tbl = tablesStream.TypeDefTable;
			if (tbl == null || tbl.IsInvalidRID(typeDefRid))
				return 0;
			var list = FindAllRowsUnsorted(tablesStream.ClassLayoutTable, 2, typeDefRid);
			return list.Length == 0 ? 0 : list[0];
		}

		/// <inheritdoc/>
		public uint GetFieldLayoutRid(uint fieldRid) {
			var tbl = tablesStream.FieldTable;
			if (tbl == null || tbl.IsInvalidRID(fieldRid))
				return 0;
			var list = FindAllRowsUnsorted(tablesStream.FieldLayoutTable, 1, fieldRid);
			return list.Length == 0 ? 0 : list[0];
		}

		/// <inheritdoc/>
		public uint GetFieldMarshalRid(Table table, uint rid) {
			var tbl = tablesStream.Get(table);
			if (tbl == null || tbl.IsInvalidRID(rid))
				return 0;
			uint codedToken;
			if (!CodedToken.HasFieldMarshal.Encode(new MDToken(table, rid), out codedToken))
				return 0;
			var list = FindAllRowsUnsorted(tablesStream.FieldMarshalTable, 0, codedToken);
			return list.Length == 0 ? 0 : list[0];
		}

		/// <inheritdoc/>
		public uint GetFieldRVARid(uint fieldRid) {
			var tbl = tablesStream.FieldTable;
			if (tbl == null || tbl.IsInvalidRID(fieldRid))
				return 0;
			var list = FindAllRowsUnsorted(tablesStream.FieldRVATable, 1, fieldRid);
			return list.Length == 0 ? 0 : list[0];
		}

		/// <inheritdoc/>
		public uint GetImplMapRid(Table table, uint rid) {
			var tbl = tablesStream.Get(table);
			if (tbl == null || tbl.IsInvalidRID(rid))
				return 0;
			uint codedToken;
			if (!CodedToken.MemberForwarded.Encode(new MDToken(table, rid), out codedToken))
				return 0;
			var list = FindAllRowsUnsorted(tablesStream.ImplMapTable, 1, codedToken);
			return list.Length == 0 ? 0 : list[0];
		}

		/// <inheritdoc/>
		public uint GetNestedClassRid(uint typeDefRid) {
			var tbl = tablesStream.TypeDefTable;
			if (tbl == null || tbl.IsInvalidRID(typeDefRid))
				return 0;
			var list = FindAllRowsUnsorted(tablesStream.NestedClassTable, 0, typeDefRid);
			return list.Length == 0 ? 0 : list[0];
		}

		/// <inheritdoc/>
		public uint GetEventMapRid(uint typeDefRid) {
			var tbl = tablesStream.TypeDefTable;
			if (tbl == null || tbl.IsInvalidRID(typeDefRid))
				return 0;
			var list = FindAllRowsUnsorted(tablesStream.EventMapTable, 0, typeDefRid);
			return list.Length == 0 ? 0 : list[0];
		}

		/// <inheritdoc/>
		public uint GetPropertyMapRid(uint typeDefRid) {
			var tbl = tablesStream.TypeDefTable;
			if (tbl == null || tbl.IsInvalidRID(typeDefRid))
				return 0;
			var list = FindAllRowsUnsorted(tablesStream.PropertyMapTable, 0, typeDefRid);
			return list.Length == 0 ? 0 : list[0];
		}

		/// <inheritdoc/>
		public uint GetConstantRid(Table table, uint rid) {
			var tbl = tablesStream.Get(table);
			if (tbl == null || tbl.IsInvalidRID(rid))
				return 0;
			uint codedToken;
			if (!CodedToken.HasConstant.Encode(new MDToken(table, rid), out codedToken))
				return 0;
			var list = FindAllRowsUnsorted(tablesStream.ConstantTable, 1, codedToken);
			return list.Length == 0 ? 0 : list[0];
		}

		/// <inheritdoc/>
		public uint GetOwnerTypeOfField(uint fieldRid) {
			if (fieldRidToTypeDefRid == null) 
				InitializeInverseFieldOwnerRidList();
			uint index = fieldRid - 1;
			if (index >= fieldRidToTypeDefRid.LongLength)
				return 0;
			return fieldRidToTypeDefRid[index];
		}

		void InitializeInverseFieldOwnerRidList() {
			if (fieldRidToTypeDefRid != null)
				return;
			fieldRidToTypeDefRid = new uint[tablesStream.FieldTable.Rows];
			var ownerList = GetTypeDefRidList();
			for (uint i = 0; i < ownerList.Length; i++) {
				var ownerRid = ownerList[i];
				var fieldList = GetFieldRidList(ownerRid);
				for (uint j = 0; j < fieldList.Length; j++) {
					uint ridIndex = fieldList[j] - 1;
					if (fieldRidToTypeDefRid[ridIndex] != 0)
						continue;
					fieldRidToTypeDefRid[ridIndex] = ownerRid;
				}
			}
		}

		/// <inheritdoc/>
		public uint GetOwnerTypeOfMethod(uint methodRid) {
			if (methodRidToTypeDefRid == null)
				InitializeInverseMethodOwnerRidList();
			uint index = methodRid - 1;
			if (index >= methodRidToTypeDefRid.LongLength)
				return 0;
			return methodRidToTypeDefRid[index];
		}

		void InitializeInverseMethodOwnerRidList() {
			if (methodRidToTypeDefRid != null)
				return;
			methodRidToTypeDefRid = new uint[tablesStream.MethodTable.Rows];
			var ownerList = GetTypeDefRidList();
			for (uint i = 0; i < ownerList.Length; i++) {
				var ownerRid = ownerList[i];
				var methodList = GetMethodRidList(ownerRid);
				for (uint j = 0; j < methodList.Length; j++) {
					uint ridIndex = methodList[j] - 1;
					if (methodRidToTypeDefRid[ridIndex] != 0)
						continue;
					methodRidToTypeDefRid[ridIndex] = ownerRid;
				}
			}
		}

		/// <inheritdoc/>
		public uint GetOwnerTypeOfEvent(uint eventRid) {
			if (eventRidToTypeDefRid == null)
				InitializeInverseEventOwnerRidList();
			uint index = eventRid - 1;
			if (index >= eventRidToTypeDefRid.LongLength)
				return 0;
			return eventRidToTypeDefRid[index];
		}

		void InitializeInverseEventOwnerRidList() {
			if (eventRidToTypeDefRid != null)
				return;
			eventRidToTypeDefRid = new uint[tablesStream.EventTable.Rows];
			var ownerList = GetTypeDefRidList();
			for (uint i = 0; i < ownerList.Length; i++) {
				var ownerRid = ownerList[i];
				var eventList = GetEventRidList(GetEventMapRid(ownerRid));
				for (uint j = 0; j < eventList.Length; j++) {
					uint ridIndex = eventList[j] - 1;
					if (eventRidToTypeDefRid[ridIndex] != 0)
						continue;
					eventRidToTypeDefRid[ridIndex] = ownerRid;
				}
			}
		}

		/// <inheritdoc/>
		public uint GetOwnerTypeOfProperty(uint propertyRid) {
			if (propertyRidToTypeDefRid == null)
				InitializeInversePropertyOwnerRidList();
			uint index = propertyRid - 1;
			if (index >= propertyRidToTypeDefRid.LongLength)
				return 0;
			return propertyRidToTypeDefRid[index];
		}

		void InitializeInversePropertyOwnerRidList() {
			if (propertyRidToTypeDefRid != null)
				return;
			propertyRidToTypeDefRid = new uint[tablesStream.PropertyTable.Rows];
			var ownerList = GetTypeDefRidList();
			for (uint i = 0; i < ownerList.Length; i++) {
				var ownerRid = ownerList[i];
				var propertyList = GetPropertyRidList(GetPropertyMapRid(ownerRid));
				for (uint j = 0; j < propertyList.Length; j++) {
					uint ridIndex = propertyList[j] - 1;
					if (propertyRidToTypeDefRid[ridIndex] != 0)
						continue;
					propertyRidToTypeDefRid[ridIndex] = ownerRid;
				}
			}
		}

		/// <inheritdoc/>
		public uint GetOwnerOfGenericParam(uint gpRid) {
			// Don't use GenericParam.Owner column. If the GP table is sorted, it's
			// possible to have two blocks of GPs with the same owner. Only one of the
			// blocks is the "real" generic params for the owner. Of course, rarely
			// if ever will this occur, but could happen if some obfuscator has
			// added it.

			if (gpRidToOwnerRid == null)
				InitializeInverseGenericParamOwnerRidList();
			uint index = gpRid - 1;
			if (index >= gpRidToOwnerRid.LongLength)
				return 0;
			return gpRidToOwnerRid[index];
		}

		void InitializeInverseGenericParamOwnerRidList() {
			if (gpRidToOwnerRid != null)
				return;
			var gpTable = tablesStream.GenericParamTable;
			gpRidToOwnerRid = new uint[gpTable.Rows];

			// Find all owners by reading the GenericParam.Owner column
			var ownerCol = gpTable.TableInfo.Columns[2];
			var ownersDict = new Dictionary<uint, bool>();
			for (uint rid = 1; rid <= gpTable.Rows; rid++) {
				uint owner;
				if (!tablesStream.ReadColumn(gpTable, rid, ownerCol, out owner))
					continue;
				ownersDict[owner] = true;
			}

			// Now that we have the owners, find all the generic params they own. An obfuscated
			// module could have 2+ owners pointing to the same generic param row.
			var owners = new List<uint>(ownersDict.Keys);
			owners.Sort();
			for (int i = 0; i < owners.Count; i++) {
				uint ownerToken;
				if (!CodedToken.TypeOrMethodDef.Decode(owners[i], out ownerToken))
					continue;
				var ridList = GetGenericParamRidList(MDToken.ToTable(ownerToken), MDToken.ToRID(ownerToken));
				for (uint j = 0; j < ridList.Length; j++) {
					uint ridIndex = ridList[j] - 1;
					if (gpRidToOwnerRid[ridIndex] != 0)
						continue;
					gpRidToOwnerRid[ridIndex] = owners[i];
				}
			}
		}

		/// <inheritdoc/>
		public uint GetOwnerOfGenericParamConstraint(uint gpcRid) {
			// Don't use GenericParamConstraint.Owner column for the same reason
			// as described in GetOwnerOfGenericParam().

			if (gpcRidToOwnerRid == null)
				InitializeInverseGenericParamConstraintOwnerRidList();
			uint index = gpcRid - 1;
			if (index >= gpcRidToOwnerRid.LongLength)
				return 0;
			return gpcRidToOwnerRid[index];
		}

		void InitializeInverseGenericParamConstraintOwnerRidList() {
			if (gpcRidToOwnerRid != null)
				return;
			var gpcTable = tablesStream.GenericParamConstraintTable;
			gpcRidToOwnerRid = new uint[gpcTable.Rows];

			var ownerCol = gpcTable.TableInfo.Columns[0];
			var ownersDict = new Dictionary<uint, bool>();
			for (uint rid = 1; rid <= gpcTable.Rows; rid++) {
				uint owner;
				if (!tablesStream.ReadColumn(gpcTable, rid, ownerCol, out owner))
					continue;
				ownersDict[owner] = true;
			}

			var owners = new List<uint>(ownersDict.Keys);
			owners.Sort();
			for (int i = 0; i < owners.Count; i++) {
				uint ownerToken = owners[i];
				var ridList = GetGenericParamConstraintRidList(ownerToken);
				for (uint j = 0; j < ridList.Length; j++) {
					uint ridIndex = ridList[j] - 1;
					if (gpcRidToOwnerRid[ridIndex] != 0)
						continue;
					gpcRidToOwnerRid[ridIndex] = ownerToken;
				}
			}
		}

		/// <inheritdoc/>
		public uint GetOwnerOfParam(uint paramRid) {
			if (paramRidToOwnerRid == null)
				InitializeInverseParamOwnerRidList();
			uint index = paramRid - 1;
			if (index >= paramRidToOwnerRid.LongLength)
				return 0;
			return paramRidToOwnerRid[index];
		}

		void InitializeInverseParamOwnerRidList() {
			if (paramRidToOwnerRid != null)
				return;

			paramRidToOwnerRid = new uint[tablesStream.ParamTable.Rows];
			var table = tablesStream.MethodTable;
			for (uint rid = 1; rid <= table.Rows; rid++) {
				var ridList = GetParamRidList(rid);
				for (uint j = 0; j < ridList.Length; j++) {
					uint ridIndex = ridList[j] - 1;
					if (paramRidToOwnerRid[ridIndex] != 0)
						continue;
					paramRidToOwnerRid[ridIndex] = rid;
				}
			}
		}

		/// <inheritdoc/>
		public RidList GetNestedClassRidList(uint typeDefRid) {
			if (typeDefRidToNestedClasses == null)
				InitializeNestedClassesDictionary();
			RandomRidList ridList;
			if (typeDefRidToNestedClasses.TryGetValue(typeDefRid, out ridList))
				return ridList;
			return ContiguousRidList.Empty;
		}

		void InitializeNestedClassesDictionary() {
			var table = tablesStream.NestedClassTable;
			var destTable = tablesStream.TypeDefTable;

			Dictionary<uint, bool> validTypeDefRids = null;
			var typeDefRidList = GetTypeDefRidList();
			if (typeDefRidList.Length != destTable.Rows) {
				validTypeDefRids = new Dictionary<uint, bool>((int)typeDefRidList.Length);
				for (uint i = 0; i < typeDefRidList.Length; i++)
					validTypeDefRids[typeDefRidList[i]] = true;
			}

			var nestedRidsDict = new Dictionary<uint, bool>((int)table.Rows);
			var nestedRids = new List<uint>((int)table.Rows);	// Need it so we add the rids in correct order
			for (uint rid = 1; rid <= table.Rows; rid++) {
				if (validTypeDefRids != null && !validTypeDefRids.ContainsKey(rid))
					continue;
				var row = tablesStream.ReadNestedClassRow(rid);
				if (row == null)
					continue;	// Should never happen since rid is valid
				if (!destTable.IsValidRID(row.NestedClass) || !destTable.IsValidRID(row.EnclosingClass))
					continue;
				if (nestedRidsDict.ContainsKey(row.NestedClass))
					continue;
				nestedRidsDict[row.NestedClass] = true;
				nestedRids.Add(row.NestedClass);
			}

			typeDefRidToNestedClasses = new Dictionary<uint, RandomRidList>();
			foreach (var nestedRid in nestedRids) {
				var row = tablesStream.ReadNestedClassRow(GetNestedClassRid(nestedRid));
				if (row == null)
					continue;
				RandomRidList ridList;
				if (!typeDefRidToNestedClasses.TryGetValue(row.EnclosingClass, out ridList))
					typeDefRidToNestedClasses[row.EnclosingClass] = ridList = new RandomRidList();
				ridList.Add(nestedRid);
			}

			nonNestedTypes = new RandomRidList((int)(destTable.Rows - nestedRidsDict.Count));
			for (uint rid = 1; rid <= destTable.Rows; rid++) {
				if (validTypeDefRids != null && !validTypeDefRids.ContainsKey(rid))
					continue;
				if (nestedRidsDict.ContainsKey(rid))
					continue;
				nonNestedTypes.Add(rid);
			}
		}

		public RidList GetNonNestedClassRidList() {
			if (nonNestedTypes == null)
				InitializeNestedClassesDictionary();
			return nonNestedTypes;
		}

		/// <inheritdoc/>
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Dispose method
		/// </summary>
		/// <param name="disposing"><c>true</c> if called by <see cref="Dispose()"/></param>
		protected virtual void Dispose(bool disposing) {
			if (!disposing)
				return;
			if (peImage != null)
				peImage.Dispose();
			if (stringsStream != null)
				stringsStream.Dispose();
			if (usStream != null)
				usStream.Dispose();
			if (blobStream != null)
				blobStream.Dispose();
			if (guidStream != null)
				guidStream.Dispose();
			if (tablesStream != null)
				tablesStream.Dispose();
			if (allStreams != null) {
				foreach (var stream in allStreams) {
					if (stream != null)
						stream.Dispose();
				}
			}
			peImage = null;
			cor20Header = null;
			mdHeader = null;
			stringsStream = null;
			usStream = null;
			blobStream = null;
			guidStream = null;
			tablesStream = null;
			allStreams = null;
			fieldRidToTypeDefRid = null;
			methodRidToTypeDefRid = null;
			typeDefRidToNestedClasses = null;
		}
	}
}

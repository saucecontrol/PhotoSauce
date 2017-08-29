// This file was originally part of WIC Tools, published under the Ms-PL license
// https://web.archive.org/web/20101223145710/http://code.msdn.microsoft.com/wictools/Project/License.aspx
// It has been modified from its original version.  Changes copyright Clinton Ingram.

//----------------------------------------------------------------------------------------
// THIS CODE AND INFORMATION IS PROVIDED "AS-IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//----------------------------------------------------------------------------------------

#if CUSTOM_MARSHAL
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace PhotoSauce.MagicScaler.Interop
{
	internal enum WICMetadataCreationOptions : uint
	{
		WICMetadataCreationDefault = 0x00000000,
		WICMetadataCreationAllowUnknown = WICMetadataCreationDefault,
		WICMetadataCreationFailUnknown = 0x00010000,
		WICMetadataCreationMask = 0xFFFF0000
	}

	internal enum WICPersistOptions : uint
	{
		WICPersistOptionDefault = 0x00000000,
		WICPersistOptionLittleEndian = 0x00000000,
		WICPersistOptionBigEndian = 0x00000001,
		WICPersistOptionStrictFormat = 0x00000002,
		WICPersistOptionNoCacheStream = 0x00000004,
		WICPersistOptionPreferUTF8 = 0x00000008,
		WICPersistOptionMask = 0x0000FFFF
	}

	[ComImport, Guid("FEAA2A8D-B3F3-43E4-B25C-D1DE990A1AE1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICMetadataBlockReader
	{
		Guid GetContainerFormat();

		uint GetCount();

		IWICMetadataReader GetReaderByIndex(
			uint nIndex
		);

		IEnumUnknown GetEnumerator();
	}

	[ComImport, Guid("08FB9676-B444-41E8-8DBE-6A53A542BFF1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICMetadataBlockWriter : IWICMetadataBlockReader
	{
#region IWICMetadataBlockReader
		new Guid GetContainerFormat();

		new uint GetCount();

		new IWICMetadataReader GetReaderByIndex(
			uint nIndex
		);

		new IEnumUnknown GetEnumerator();
#endregion

		void InitializeFromBlockReader(
			IWICMetadataBlockReader pIMDBlockReader
		);

		IWICMetadataWriter GetWriterByIndex(
			uint nIndex
		);

		void AddWriter(
			IWICMetadataWriter pIMetadataWriter
		);

		void SetWriterByIndex(
			uint nIndex, 
			IWICMetadataWriter pIMetadataWriter
		);

		void RemoveWriterByIndex(
			uint nIndex
		);
	}

	[ComImport, Guid("9204FE99-D8FC-4FD5-A001-9536B067A899"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICMetadataReader
	{
		Guid GetMetadataFormat();

		IWICMetadataHandlerInfo GetMetadataHandlerInfo();

		uint GetCount();

		void GetValueByIndex(
			uint nIndex,
			[In, Out, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarSchema,
			[In, Out, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarId,
			[In, Out, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarValue
		);

		void GetValue(
			[In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarSchema,
			[In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarId,
			[In, Out, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarValue
		);

		IWICEnumMetadataItem GetEnumerator();
	}

	[ComImport, Guid("F7836E16-3BE0-470B-86BB-160D0AECD7DE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICMetadataWriter : IWICMetadataReader
	{
#region IWICMetadataReader
		new Guid GetMetadataFormat();

		new IWICMetadataHandlerInfo GetMetadataHandlerInfo();

		new uint GetCount();

		new void GetValueByIndex(uint nIndex,
			[In, Out, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarSchema,
			[In, Out, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarId,
			[In, Out, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarValue
		);

		new void GetValue(
			[In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarSchema,
			[In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarId,
			[In, Out, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarValue
		);

		new IWICEnumMetadataItem GetEnumerator();
#endregion

		void SetValue(
			[In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarSchema,
			[In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarId,
			[In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarValue
		);

		void SetValueByIndex(
			uint nIndex,
			[In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarSchema,
			[In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarId,
			[In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarValue
		);

		void RemoveValue(
			[In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarSchema,
			[In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarId
		);

		void RemoveValueByIndex(
			uint nIndex
		);
	}

	[ComImport, Guid("449494BC-B468-4927-96D7-BA90D31AB505"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICStreamProvider
	{
		IStream GetStream();

		uint GetPersistOptions();

		Guid GetPreferredVendorGUID();

		void RefreshStream();
	}

	[ComImport, Guid("ABA958BF-C672-44D1-8D61-CE6DF2E682C2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICMetadataHandlerInfo : IWICComponentInfo
	{
#region IWICComponentInfo
		new WICComponentType GetComponentType();

		new Guid GetCLSID();

		new WICComponentSigning GetSigningStatus();

		new uint GetAuthor(
			uint cchAuthor,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)] 
			StringBuilder wzAuthor
		);

		new Guid GetVendorGUID();

		new uint GetVersion(
			uint cchVersion,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)] 
			StringBuilder wzVersion
		);

		new uint GetSpecVersion(
			uint cchSpecVersion,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)] 
			StringBuilder wzSpecVersion
		);

		new uint GetFriendlyName(
			uint cchFriendlyName,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)] 
			StringBuilder wzFriendlyName
		);
#endregion

		Guid GetMetadataFormat();

		uint GetContainerFormats(
			uint cContainerFormats,
			[Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] 
			Guid[] pguidContainerFormats
		);

		uint GetDeviceManufacturer(
			uint cchDeviceManufacturer,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)] 
			StringBuilder wzDeviceManufacturer
		);

		uint GetDeviceModels(
			uint cchDeviceModels,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]  
			StringBuilder wzDeviceModels
		);

		[return: MarshalAs(UnmanagedType.Bool)]
		bool DoesRequireFullStream();

		[return: MarshalAs(UnmanagedType.Bool)]
		bool DoesSupportPadding();

		[return: MarshalAs(UnmanagedType.Bool)]
		bool DoesRequireFixedSize();
	}

	internal struct WICMetadataPattern
	{
		public ulong Position;
		public uint Length;
		public IntPtr Pattern;
		public IntPtr Mask;
		public ulong DataOffset;
	}

	[ComImport, Guid("EEBF1F5B-07C1-4447-A3AB-22ACAF78A804"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICMetadataReaderInfo : IWICMetadataHandlerInfo
	{
#region IWICMetadataHandlerInfo
#region IWICComponentInfo
		new WICComponentType GetComponentType();

		new Guid GetCLSID();

		new WICComponentSigning GetSigningStatus();

		new uint GetAuthor(
			uint cchAuthor,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)] 
			StringBuilder wzAuthor
		);

		new Guid GetVendorGUID();

		new uint GetVersion(
			uint cchVersion,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)] 
			StringBuilder wzVersion
		);

		new uint GetSpecVersion(
			uint cchSpecVersion,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)] 
			StringBuilder wzSpecVersion
		);

		new uint GetFriendlyName(
			uint cchFriendlyName,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)] 
			StringBuilder wzFriendlyName
		);
#endregion

		new Guid GetMetadataFormat();

		new uint GetContainerFormats(
			uint cContainerFormats,
			[Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			Guid[] pguidContainerFormats
		);

		new uint GetDeviceManufacturer(
			uint cchDeviceManufacturer,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzDeviceManufacturer
		);

		new uint GetDeviceModels(
			uint cchDeviceModels,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzDeviceModels
		);

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool DoesRequireFullStream();

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool DoesSupportPadding();

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool DoesRequireFixedSize();
#endregion

		uint GetPatterns(
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid guidContainerFormat,
			uint cbSize,
			IntPtr pPattern,
			out uint pcCount
		);

		[return: MarshalAs(UnmanagedType.Bool)]
		bool MatchesPattern(
			[MarshalAs(UnmanagedType.LPStruct)] 
			Guid guidContainerFormat,
			IStream pIStream
		);

		IWICMetadataReader CreateInstance();
	}

	internal struct WICMetadataHeader
	{
		public ulong Position;
		public uint Length;
		public IntPtr Header;
		public ulong DataOffset;
	}

	[ComImport, Guid("B22E3FBA-3925-4323-B5C1-9EBFC430F236"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICMetadataWriterInfo : IWICMetadataHandlerInfo
	{
#region IWICMetadataHandlerInfo
#region IWICComponentInfo
		new WICComponentType GetComponentType();

		new Guid GetCLSID();

		new WICComponentSigning GetSigningStatus();

		new uint GetAuthor(
			uint cchAuthor,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)] 
			StringBuilder wzAuthor
		);

		new Guid GetVendorGUID();

		new uint GetVersion(
			uint cchVersion,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)] 
			StringBuilder wzVersion
		);

		new uint GetSpecVersion(
			uint cchSpecVersion,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)] 
			StringBuilder wzSpecVersion
		);

		new uint GetFriendlyName(
			uint cchFriendlyName,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)] 
			StringBuilder wzFriendlyName
		);
#endregion

		new Guid GetMetadataFormat();

		new uint GetContainerFormats(
			uint cContainerFormats,
			[Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			Guid[] pguidContainerFormats
		);

		new uint GetDeviceManufacturer(
			uint cchDeviceManufacturer,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzDeviceManufacturer
		);

		new uint GetDeviceModels(
			uint cchDeviceModels,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzDeviceModels
		);

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool DoesRequireFullStream();

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool DoesSupportPadding();

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool DoesRequireFixedSize();
#endregion

		uint GetHeader(
			[MarshalAs(UnmanagedType.LPStruct)] 
			Guid guidContainerFormat,
			uint cbSize,
			IntPtr pHeader
		);

		IWICMetadataWriter CreateInstance();
	}

	[ComImport, Guid("412D0C3A-9650-44FA-AF5B-DD2A06C8E8FB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICComponentFactory : IWICImagingFactory
	{
#region IWICImagingFactory
		new IWICBitmapDecoder CreateDecoderFromFilename(
			[MarshalAs(UnmanagedType.LPWStr)]
			string wzFilename,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor,
			GenericAccessRights dwDesiredAccess,
			WICDecodeOptions metadataOptions
		);

		new IWICBitmapDecoder CreateDecoderFromStream(
			IStream pIStream,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor,
			WICDecodeOptions metadataOptions
		);

		new IWICBitmapDecoder CreateDecoderFromFileHandle(
			IntPtr hFile,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor,
			WICDecodeOptions metadataOptions
		);

		new IWICComponentInfo CreateComponentInfo(
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid clsidComponent
		);

		new IWICBitmapDecoder CreateDecoder(
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid guidContainerFormat,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor
		);

		new IWICBitmapEncoder CreateEncoder(
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid guidContainerFormat,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor
		);

		new IWICPalette CreatePalette();

		new IWICFormatConverter CreateFormatConverter();

		new IWICBitmapScaler CreateBitmapScaler();

		new IWICBitmapClipper CreateBitmapClipper();

		new IWICBitmapFlipRotator CreateBitmapFlipRotator();

		new IWICStream CreateStream();

		new IWICColorContext CreateColorContext();

		new IWICColorTransform CreateColorTransform();

		new IWICBitmap CreateBitmap(
			uint uiWidth,
			uint uiHeight,
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid pixelFormat,
			WICBitmapCreateCacheOption option
		);

		new IWICBitmap CreateBitmapFromSource(
			IWICBitmapSource pIBitmapSource,
			WICBitmapCreateCacheOption option
		);

		new IWICBitmap CreateBitmapFromSourceRect(
			IWICBitmapSource pIBitmapSource,
			uint x,
			uint y,
			uint width,
			uint height
		);

		new IWICBitmap CreateBitmapFromMemory(
			uint uiWidth,
			uint uiHeight,
			[MarshalAs(UnmanagedType.LPStruct)] 
			Guid pixelFormat,
			uint cbStride,
			uint cbBufferSize,
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] 
			byte[] pbBuffer
		);

		new IWICBitmap CreateBitmapFromHBITMAP(
			IntPtr hBitmap,
			IntPtr hPalette,
			WICBitmapAlphaChannelOption options
		);

		new IWICBitmap CreateBitmapFromHICON(
			IntPtr hIcon
		);

		new IEnumUnknown CreateComponentEnumerator(
			WICComponentType componentTypes,           /* WICComponentType */
			WICComponentEnumerateOptions options       /* WICComponentEnumerateOptions */
		);

		new IWICFastMetadataEncoder CreateFastMetadataEncoderFromDecoder(
			IWICBitmapDecoder pIDecoder
		);

		new IWICFastMetadataEncoder CreateFastMetadataEncoderFromFrameDecode(
			IWICBitmapFrameDecode pIFrameDecoder
		);

		new IWICMetadataQueryWriter CreateQueryWriter(
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid guidMetadataFormat,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor
		);

		new IWICMetadataQueryWriter CreateQueryWriterFromReader(
			IWICMetadataQueryReader pIQueryReader,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor
		);
#endregion

		IWICMetadataReader CreateMetadataReader(
			[MarshalAs(UnmanagedType.LPStruct)] 
			Guid guidMetadataFormat,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor,
			uint dwOptions,
			IStream pIStream
		);

		IWICMetadataReader CreateMetadataReaderFromContainer(
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid guidContainerFormat,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor,
			uint dwOptions,
			IStream pIStream
		);

		IWICMetadataWriter CreateMetadataWriter(
			[MarshalAs(UnmanagedType.LPStruct)] 
			Guid guidMetadataFormat,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor,
			uint dwMetadataOptions
		);

		IWICMetadataWriter CreateMetadataWriterFromReader(
			IWICMetadataReader pIReader,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor
		);

		IWICMetadataQueryReader CreateQueryReaderFromBlockReader(
			IWICMetadataBlockReader pIBlockReader
		);

		IWICMetadataQueryWriter CreateQueryWriterFromBlockWriter(
			IWICMetadataBlockWriter pIBlockWriter
		);

		IPropertyBag2 CreateEncoderPropertyBag(
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] 
			PROPBAG2[] ppropOptions,
			uint cCount
		);
	}
}
#endif

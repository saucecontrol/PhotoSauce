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

#pragma warning disable 0618    // VarEnum is obsolete
#pragma warning disable IDE1006 // Naming Styles

using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

using EXCEPINFO = System.Runtime.InteropServices.ComTypes.EXCEPINFO;
using STATSTG = System.Runtime.InteropServices.ComTypes.STATSTG;

namespace PhotoSauce.MagicScaler.Interop
{
	internal enum WinCodecError
	{
		WINCODEC_ERR_GENERIC_ERROR = unchecked((int)0x80004005),
		WINCODEC_ERR_INVALIDPARAMETER = unchecked((int)0x80070057),
		WINCODEC_ERR_OUTOFMEMORY = unchecked((int)0x8007000E),
		WINCODEC_ERR_NOTIMPLEMENTED = unchecked((int)0x80004001),
		WINCODEC_ERR_ABORTED = unchecked((int)0x80004004),
		WINCODEC_ERR_ACCESSDENIED = unchecked((int)0x80070005),
		WINCODEC_ERR_VALUEOVERFLOW = unchecked((int)0x80070216),

		WINCODEC_ERR_WRONGSTATE = unchecked((int)0x88982f04),
		WINCODEC_ERR_VALUEOUTOFRANGE = unchecked((int)0x88982f05),
		WINCODEC_ERR_UNKNOWNIMAGEFORMAT = unchecked((int)0x88982f07),
		WINCODEC_ERR_UNSUPPORTEDVERSION = unchecked((int)0x88982f0B),
		WINCODEC_ERR_NOTINITIALIZED = unchecked((int)0x88982f0C),
		WINCODEC_ERR_ALREADYLOCKED = unchecked((int)0x88982f0D),

		WINCODEC_ERR_PROPERTYNOTFOUND = unchecked((int)0x88982f40),
		WINCODEC_ERR_PROPERTYNOTSUPPORTED = unchecked((int)0x88982f41),
		WINCODEC_ERR_PROPERTYSIZE = unchecked((int)0x88982f42),

		WINCODEC_ERR_CODECPRESENT = unchecked((int)0x88982f43),
		WINCODEC_ERR_CODECNOTHUMBNAIL = unchecked((int)0x88982f44),
		WINCODEC_ERR_PALETTEUNAVAILABLE = unchecked((int)0x88982f45),
		WINCODEC_ERR_CODECTOOMANYSCANLINES = unchecked((int)0x88982f46),
		WINCODEC_ERR_INTERNALERROR = unchecked((int)0x88982f48),
		WINCODEC_ERR_SOURCERECTDOESNOTMATCHDIMENSIONS = unchecked((int)0x88982f49),
		WINCODEC_ERR_COMPONENTNOTFOUND = unchecked((int)0x88982f50),
		WINCODEC_ERR_IMAGESIZEOUTOFRANGE = unchecked((int)0x88982f51),
		WINCODEC_ERR_TOOMUCHMETADATA = unchecked((int)0x88982f52),

		WINCODEC_ERR_BADIMAGE = unchecked((int)0x88982f60),
		WINCODEC_ERR_BADHEADER = unchecked((int)0x88982f61),
		WINCODEC_ERR_FRAMEMISSING = unchecked((int)0x88982f62),
		WINCODEC_ERR_BADMETADATAHEADER = unchecked((int)0x88982f63),
		WINCODEC_ERR_BADSTREAMDATA = unchecked((int)0x88982f70),
		WINCODEC_ERR_STREAMWRITE = unchecked((int)0x88982f71),
		WINCODEC_ERR_STREAMREAD = unchecked((int)0x88982f72),
		WINCODEC_ERR_STREAMNOTAVAILABLE = unchecked((int)0x88982f73),
		WINCODEC_ERR_UNSUPPORTEDPIXELFORMAT = unchecked((int)0x88982f80),
		WINCODEC_ERR_UNSUPPORTEDOPERATION = unchecked((int)0x88982f81),

		WINCODEC_ERR_INVALIDREGISTRATION = unchecked((int)0x88982f8A),
		WINCODEC_ERR_COMPONENTINITIALIZEFAILURE = unchecked((int)0x88982f8B),
		WINCODEC_ERR_INSUFFICIENTBUFFER = unchecked((int)0x88982f8C),
		WINCODEC_ERR_DUPLICATEMETADATAPRESENT = unchecked((int)0x88982f8D),
		WINCODEC_ERR_PROPERTYUNEXPECTEDTYPE = unchecked((int)0x88982f8E),
		WINCODEC_ERR_UNEXPECTEDSIZE = unchecked((int)0x88982f8F),

		WINCODEC_ERR_INVALIDQUERYREQUEST = unchecked((int)0x88982f90),
		WINCODEC_ERR_UNEXPECTEDMETADATATYPE = unchecked((int)0x88982f91),
		WINCODEC_ERR_REQUESTONLYVALIDATMETADATAROOT = unchecked((int)0x88982f92),
		WINCODEC_ERR_INVALIDQUERYCHARACTER = unchecked((int)0x88982f93),
		WINCODEC_ERR_WIN32ERROR = unchecked((int)0x88982f94),
		WINCODEC_ERR_INVALIDPROGRESSIVELEVEL = unchecked((int)0x88982f95),

		ERROR_INVALID_PROFILE = unchecked((int)0x800707DB)
	}

	[Flags]
	internal enum GenericAccessRights : uint
	{
		GENERIC_READ    = 0x80000000,
		GENERIC_WRITE   = 0x40000000,
		GENERIC_EXECUTE = 0x20000000,
		GENERIC_ALL     = 0x10000000,

		GENERIC_READ_WRITE = GENERIC_READ | GENERIC_WRITE
	}

	[ComImport, Guid("00000100-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IEnumUnknown
	{
		uint Next(
			uint celt, 
			[Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.IUnknown, SizeParamIndex = 0)]
			object[] rgelt
		);

		void Skip(
			uint celt
		);

		void Reset();

		void Clone();
	}

	internal enum CLIPFORMAT : short
	{
		CF_TEXT = 1,
		CF_BITMAP = 2,
		CF_METAFILEPICT = 3,
		CF_SYLK = 4,
		CF_DIF = 5,
		CF_TIFF = 6,
		CF_OEMTEXT = 7,
		CF_DIB = 8,
		CF_PALETTE = 9,
		CF_PENDATA = 10,
		CF_RIFF = 11,
		CF_WAVE = 12,
		CF_UNICODETEXT = 13,
		CF_ENHMETAFILE = 14,
		CF_HDROP = 15,
		CF_LOCALE = 16,
		CF_MAX = 17,
		CF_OWNERDISPLAY = 0x80,
		CF_DSPTEXT = 0x81,
		CF_DSPBITMAP = 0x82,
		CF_DSPMETAFILEPICT = 0x83,
		CF_DSPENHMETAFILE = 0x8E,
	}

	internal enum PROPBAG2_TYPE : uint
	{
		PROPBAG2_TYPE_UNDEFINED = 0,
		PROPBAG2_TYPE_DATA = 1,
		PROPBAG2_TYPE_URL = 2,
		PROPBAG2_TYPE_OBJECT = 3,
		PROPBAG2_TYPE_STREAM = 4,
		PROPBAG2_TYPE_STORAGE = 5,
		PROPBAG2_TYPE_MONIKER = 6
	}

	internal struct PROPBAG2
	{
		public PROPBAG2_TYPE dwType;        // Property type (from PROPBAG2_TYPE)
		private ushort _vt;                 // VARIANT property type
		public CLIPFORMAT cfType;           // Clipboard format (aka MIME-type)
		public uint dwHint;                 // Property name hint
		[MarshalAs(UnmanagedType.LPWStr)]
		public string pstrName;             // Property name
		public Guid clsid;                  // CLSID (for PROPBAG2_TYPE_OBJECT)

		public VarEnum vt { get => (VarEnum)_vt; set => _vt = (ushort)value; }
	}

	[ComImport, Guid("3127CA40-446E-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IErrorLog
	{
		void AddError(
			[MarshalAs(UnmanagedType.LPWStr)]
			string pszPropName, 
			[In] 
			ref EXCEPINFO pExcepInfo
		);
	}

	[ComImport, Guid("22F55882-280B-11d0-A8A9-00A0C90C2004"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IPropertyBag2
	{
		void Read(
			uint cProperties,
			[In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			PROPBAG2[] pPropBag,
			IErrorLog pErrLog,
			[Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			object[] pvarValue,
			[Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Error, SizeParamIndex = 0)]
			int[] phrError
		);

		void Write(
			uint cProperties,
			[In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			PROPBAG2[] pPropBag,
			[In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			object[] pvarValue
		);

		uint CountProperties();

		void GetPropertyInfo(
			uint iProperty,
			uint cProperties,
			[Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
			PROPBAG2[] pPropBag,
			out uint pcProperties
		);

		void LoadObject(
			[MarshalAs(UnmanagedType.LPWStr)]
			string pstrName,
			uint dwHint,
			[MarshalAs(UnmanagedType.IUnknown)]
			object pUnkObject,
			IErrorLog pErrLog
		);
	}

	internal static class Consts
	{
		public const int WINCODEC_SDK_VERSION = 0x0237;

		public static readonly Guid CATID_WICBitmapDecoders                            = new Guid(0x7ed96837, 0x96f0, 0x4812, 0xb2, 0x11, 0xf1, 0x3c, 0x24, 0x11, 0x7e, 0xd3);
		public static readonly Guid CATID_WICBitmapEncoders                            = new Guid(0xac757296, 0x3522, 0x4e11, 0x98, 0x62, 0xc1, 0x7b, 0xe5, 0xa1, 0x76, 0x7e);
		public static readonly Guid CATID_WICFormatConverters                          = new Guid(0x7835eae8, 0xbf14, 0x49d1, 0x93, 0xce, 0x53, 0x3a, 0x40, 0x7b, 0x22, 0x48);
		public static readonly Guid CATID_WICMetadataReader                            = new Guid(0x05af94d8, 0x7174, 0x4cd2, 0xbe, 0x4a, 0x41, 0x24, 0xb8, 0x0e, 0xe4, 0xb8);
		public static readonly Guid CATID_WICMetadataWriter                            = new Guid(0xabe3b9a4, 0x257d, 0x4b97, 0xbd, 0x1a, 0x29, 0x4a, 0xf4, 0x96, 0x22, 0x2e);
		public static readonly Guid CATID_WICPixelFormats                              = new Guid(0x2b46e70f, 0xcda7, 0x473e, 0x89, 0xf6, 0xdc, 0x96, 0x30, 0xa2, 0x39, 0x0b);

		public static readonly Guid CLSID_WIC8BIMIPTCMetadataReader                    = new Guid(0x0010668c, 0x0801, 0x4da6, 0xa4, 0xa4, 0x82, 0x65, 0x22, 0xb6, 0xd2, 0x8f);
		public static readonly Guid CLSID_WIC8BIMIPTCMetadataWriter                    = new Guid(0x00108226, 0xee41, 0x44a2, 0x9e, 0x9c, 0x4b, 0xe4, 0xd5, 0xb1, 0xd2, 0xcd);
		public static readonly Guid CLSID_WIC8BIMResolutionInfoMetadataReader          = new Guid(0x5805137A, 0xE348, 0x4F7C, 0xB3, 0xCC, 0x6D, 0xB9, 0x96, 0x5A, 0x05, 0x99);
		public static readonly Guid CLSID_WIC8BIMResolutionInfoMetadataWriter          = new Guid(0x4ff2fe0e, 0xe74a, 0x4b71, 0x98, 0xc4, 0xab, 0x7d, 0xc1, 0x67, 0x07, 0xba);
		public static readonly Guid CLSID_WICAPEMetadataReader                         = new Guid(0x1767B93A, 0xB021, 0x44EA, 0x92, 0x0F, 0x86, 0x3C, 0x11, 0xF4, 0xF7, 0x68);
		public static readonly Guid CLSID_WICAPEMetadataWriter                         = new Guid(0xBD6EDFCA, 0x2890, 0x482F, 0xB2, 0x33, 0x8D, 0x73, 0x39, 0xA1, 0xCF, 0x8D);
		public static readonly Guid CLSID_WICApp0MetadataReader                        = new Guid(0x43324B33, 0xA78F, 0x480f, 0x91, 0x11, 0x96, 0x38, 0xAA, 0xCC, 0xC8, 0x32);
		public static readonly Guid CLSID_WICApp0MetadataWriter                        = new Guid(0xF3C633A2, 0x46C8, 0x498e, 0x8F, 0xBB, 0xCC, 0x6F, 0x72, 0x1B, 0xBC, 0xDE);
		public static readonly Guid CLSID_WICApp13MetadataReader                       = new Guid(0xAA7E3C50, 0x864C, 0x4604, 0xBC, 0x04, 0x8B, 0x0B, 0x76, 0xE6, 0x37, 0xF6);
		public static readonly Guid CLSID_WICApp13MetadataWriter                       = new Guid(0x7B19A919, 0xA9D6, 0x49E5, 0xBD, 0x45, 0x02, 0xC3, 0x4E, 0x4E, 0x4C, 0xD5);
		public static readonly Guid CLSID_WICApp1MetadataReader                        = new Guid(0xdde33513, 0x774e, 0x4bcd, 0xae, 0x79, 0x02, 0xf4, 0xad, 0xfe, 0x62, 0xfc);
		public static readonly Guid CLSID_WICApp1MetadataWriter                        = new Guid(0xee366069, 0x1832, 0x420f, 0xb3, 0x81, 0x04, 0x79, 0xad, 0x06, 0x6f, 0x19);
		public static readonly Guid CLSID_WICBmpDecoder                                = new Guid(0x6b462062, 0x7cbf, 0x400d, 0x9f, 0xdb, 0x81, 0x3d, 0xd1, 0x0f, 0x27, 0x78);
		public static readonly Guid CLSID_WICBmpEncoder                                = new Guid(0x69be8bb4, 0xd66d, 0x47c8, 0x86, 0x5a, 0xed, 0x15, 0x89, 0x43, 0x37, 0x82);
		public static readonly Guid CLSID_WICExifMetadataReader                        = new Guid(0xd9403860, 0x297f, 0x4a49, 0xbf, 0x9b, 0x77, 0x89, 0x81, 0x50, 0xa4, 0x42);
		public static readonly Guid CLSID_WICExifMetadataWriter                        = new Guid(0xc9a14cda, 0xc339, 0x460b, 0x90, 0x78, 0xd4, 0xde, 0xbc, 0xfa, 0xbe, 0x91);
		public static readonly Guid CLSID_WICDefaultFormatConverter                    = new Guid(0x1a3f11dc, 0xb514, 0x4b17, 0x8c, 0x5f, 0x21, 0x54, 0x51, 0x38, 0x52, 0xf1);
		public static readonly Guid CLSID_WICFormatConverterHighColor                  = new Guid(0xac75d454, 0x9f37, 0x48f8, 0xb9, 0x72, 0x4e, 0x19, 0xbc, 0x85, 0x60, 0x11);
		public static readonly Guid CLSID_WICFormatConverterNChannel                   = new Guid(0xc17cabb2, 0xd4a3, 0x47d7, 0xa5, 0x57, 0x33, 0x9b, 0x2e, 0xfb, 0xd4, 0xf1);
		public static readonly Guid CLSID_WICFormatConverterWMPhoto                    = new Guid(0x9cb5172b, 0xd600, 0x46ba, 0xab, 0x77, 0x77, 0xbb, 0x7e, 0x3a, 0x00, 0xd9);
		public static readonly Guid CLSID_WICPlanarFormatConverter                     = new Guid(0x184132b8, 0x32f8, 0x4784, 0x91, 0x31, 0xdd, 0x72, 0x24, 0xb2, 0x34, 0x38);
		public static readonly Guid CLSID_WICGCEMetadataReader                         = new Guid(0xB92E345D, 0xF52D, 0x41F3, 0xB5, 0x62, 0x08, 0x1B, 0xC7, 0x72, 0xE3, 0xB9);
		public static readonly Guid CLSID_WICGCEMetadataWriter                         = new Guid(0xAF95DC76, 0x16B2, 0x47F4, 0xB3, 0xEA, 0x3C, 0x31, 0x79, 0x66, 0x93, 0xE7);
		public static readonly Guid CLSID_WICGifCommentMetadataReader                  = new Guid(0x32557D3B, 0x69DC, 0x4F95, 0x83, 0x6E, 0xF5, 0x97, 0x2B, 0x2F, 0x61, 0x59);
		public static readonly Guid CLSID_WICGifCommentMetadataWriter                  = new Guid(0xA02797fC, 0xC4AE, 0x418C, 0xAF, 0x95, 0xE6, 0x37, 0xC7, 0xEA, 0xD2, 0xA1);
		public static readonly Guid CLSID_WICGifDecoder                                = new Guid(0x381dda3c, 0x9ce9, 0x4834, 0xa2, 0x3e, 0x1f, 0x98, 0xf8, 0xfc, 0x52, 0xbe);
		public static readonly Guid CLSID_WICGifEncoder                                = new Guid(0x114f5598, 0x0b22, 0x40a0, 0x86, 0xa1, 0xc8, 0x3e, 0xa4, 0x95, 0xad, 0xbd);
		public static readonly Guid CLSID_WICGpsMetadataReader                         = new Guid(0x3697790B, 0x223B, 0x484E, 0x99, 0x25, 0xC4, 0x86, 0x92, 0x18, 0xF1, 0x7A);
		public static readonly Guid CLSID_WICGpsMetadataWriter                         = new Guid(0xCB8C13E4, 0x62B5, 0x4C96, 0xA4, 0x8B, 0x6B, 0xA6, 0xAC, 0xE3, 0x9C, 0x76);
		public static readonly Guid CLSID_WICIcoDecoder                                = new Guid(0xc61bfcdf, 0x2e0f, 0x4aad, 0xa8, 0xd7, 0xe0, 0x6b, 0xaf, 0xeb, 0xcd, 0xfe);
		public static readonly Guid CLSID_WICIfdMetadataReader                         = new Guid(0x8f914656, 0x9d0a, 0x4eb2, 0x90, 0x19, 0x0b, 0xf9, 0x6d, 0x8a, 0x9e, 0xe6);
		public static readonly Guid CLSID_WICIfdMetadataWriter                         = new Guid(0xb1ebfc28, 0xc9bd, 0x47a2, 0x8d, 0x33, 0xb9, 0x48, 0x76, 0x97, 0x77, 0xa7);
		public static readonly Guid CLSID_WICImagingCategories                         = new Guid(0xfae3d380, 0xfea4, 0x4623, 0x8c, 0x75, 0xc6, 0xb6, 0x11, 0x10, 0xb6, 0x81);
		public static readonly Guid CLSID_WICImagingFactory                            = new Guid(0xcacaf262, 0x9370, 0x4615, 0xa1, 0x3b, 0x9f, 0x55, 0x39, 0xda, 0x4c, 0x0a);
		public static readonly Guid CLSID_WICImagingFactory1                           = new Guid(0xcacaf262, 0x9370, 0x4615, 0xa1, 0x3b, 0x9f, 0x55, 0x39, 0xda, 0x4c, 0x0a);
		public static readonly Guid CLSID_WICImagingFactory2                           = new Guid(0x317d06e8, 0x5f24, 0x433d, 0xbd, 0xf7, 0x79, 0xce, 0x68, 0xd8, 0xab, 0xc2);
		public static readonly Guid CLSID_WICIMDMetadataReader                         = new Guid(0x7447A267, 0x0015, 0x42C8, 0xA8, 0xF1, 0xFB, 0x3B, 0x94, 0xC6, 0x83, 0x61);
		public static readonly Guid CLSID_WICIMDMetadataWriter                         = new Guid(0x8C89071F, 0x452E, 0x4E95, 0x96, 0x82, 0x9D, 0x10, 0x24, 0x62, 0x71, 0x72);
		public static readonly Guid CLSID_WICInteropMetadataReader                     = new Guid(0xB5C8B898, 0x0074, 0x459F, 0xB7, 0x00, 0x86, 0x0D, 0x46, 0x51, 0xEA, 0x14);
		public static readonly Guid CLSID_WICInteropMetadataWriter                     = new Guid(0x122EC645, 0xCD7E, 0x44D8, 0xB1, 0x86, 0x2C, 0x8C, 0x20, 0xC3, 0xB5, 0x0F);
		public static readonly Guid CLSID_WICIPTCMetadataReader                        = new Guid(0x03012959, 0xf4f6, 0x44d7, 0x9d, 0x09, 0xda, 0xa0, 0x87, 0xa9, 0xdb, 0x57);
		public static readonly Guid CLSID_WICIPTCMetadataWriter                        = new Guid(0x1249b20c, 0x5dd0, 0x44fe, 0xb0, 0xb3, 0x8f, 0x92, 0xc8, 0xe6, 0xd0, 0x80);
		public static readonly Guid CLSID_WICIRBMetadataReader                         = new Guid(0xD4DCD3D7, 0xB4C2, 0x47D9, 0xA6, 0xBF, 0xB8, 0x9B, 0xA3, 0x96, 0xA4, 0xA3);
		public static readonly Guid CLSID_WICIRBMetadataWriter                         = new Guid(0x5C5C1935, 0x0235, 0x4434, 0x80, 0xBC, 0x25, 0x1B, 0xC1, 0xEC, 0x39, 0xC6);
		public static readonly Guid CLSID_WICJpegChrominanceMetadataReader             = new Guid(0x50B1904B, 0xF28F, 0x4574, 0x93, 0xF4, 0x0B, 0xAD, 0xE8, 0x2C, 0x69, 0xE9);
		public static readonly Guid CLSID_WICJpegChrominanceMetadataWriter             = new Guid(0x3FF566F0, 0x6E6B, 0x49D4, 0x96, 0xE6, 0xB7, 0x88, 0x86, 0x69, 0x2C, 0x62);
		public static readonly Guid CLSID_WICJpegCommentMetadataReader                 = new Guid(0x9f66347C, 0x60C4, 0x4C4D, 0xAB, 0x58, 0xD2, 0x35, 0x86, 0x85, 0xf6, 0x07);
		public static readonly Guid CLSID_WICJpegCommentMetadataWriter                 = new Guid(0xE573236F, 0x55B1, 0x4EDA, 0x81, 0xEA, 0x9F, 0x65, 0xDB, 0x02, 0x90, 0xD3);
		public static readonly Guid CLSID_WICJpegDecoder                               = new Guid(0x9456a480, 0xe88b, 0x43ea, 0x9e, 0x73, 0x0b, 0x2d, 0x9b, 0x71, 0xb1, 0xca);
		public static readonly Guid CLSID_WICJpegEncoder                               = new Guid(0x1a34f5c1, 0x4a5a, 0x46dc, 0xb6, 0x44, 0x1f, 0x45, 0x67, 0xe7, 0xa6, 0x76);
		public static readonly Guid CLSID_WICJpegLuminanceMetadataReader               = new Guid(0x356F2F88, 0x05A6, 0x4728, 0xB9, 0xA4, 0x1B, 0xFB, 0xCE, 0x04, 0xD8, 0x38);
		public static readonly Guid CLSID_WICJpegLuminanceMetadataWriter               = new Guid(0x1D583ABC, 0x8A0E, 0x4657, 0x99, 0x82, 0xA3, 0x80, 0xCA, 0x58, 0xFB, 0x4B);
		public static readonly Guid CLSID_WICLSDMetadataReader                         = new Guid(0x41070793, 0x59E4, 0x479A, 0xA1, 0xF7, 0x95, 0x4A, 0xDC, 0x2E, 0xF5, 0xFC);
		public static readonly Guid CLSID_WICLSDMetadataWriter                         = new Guid(0x73C037E7, 0xE5D9, 0x4954, 0x87, 0x6A, 0x6D, 0xA8, 0x1D, 0x6E, 0x57, 0x68);
		public static readonly Guid CLSID_WICPngBkgdMetadataReader                     = new Guid(0x0CE7A4A6, 0x03E8, 0x4A60, 0x9D, 0x15, 0x28, 0x2E, 0xF3, 0x2E, 0xE7, 0xDA);
		public static readonly Guid CLSID_WICPngBkgdMetadataWriter                     = new Guid(0x68E3F2FD, 0x31AE, 0x4441, 0xBB, 0x6A, 0xFD, 0x70, 0x47, 0x52, 0x5F, 0x90);
		public static readonly Guid CLSID_WICPngChrmMetadataReader                     = new Guid(0xF90B5F36, 0x367B, 0x402A, 0x9D, 0xD1, 0xBC, 0x0F, 0xD5, 0x9D, 0x8F, 0x62);
		public static readonly Guid CLSID_WICPngChrmMetadataWriter                     = new Guid(0xE23CE3EB, 0x5608, 0x4E83, 0xBC, 0xEF, 0x27, 0xB1, 0x98, 0x7E, 0x51, 0xD7);
		public static readonly Guid CLSID_WICPngDecoder                                = new Guid(0x389ea17b, 0x5078, 0x4cde, 0xb6, 0xef, 0x25, 0xc1, 0x51, 0x75, 0xc7, 0x51);
		public static readonly Guid CLSID_WICPngDecoder1                               = new Guid(0x389ea17b, 0x5078, 0x4cde, 0xb6, 0xef, 0x25, 0xc1, 0x51, 0x75, 0xc7, 0x51);
		public static readonly Guid CLSID_WICPngDecoder2                               = new Guid(0xe018945b, 0xaa86, 0x4008, 0x9b, 0xd4, 0x67, 0x77, 0xa1, 0xe4, 0x0c, 0x11);
		public static readonly Guid CLSID_WICPngEncoder                                = new Guid(0x27949969, 0x876a, 0x41d7, 0x94, 0x47, 0x56, 0x8f, 0x6a, 0x35, 0xa4, 0xdc);
		public static readonly Guid CLSID_WICPngGamaMetadataReader                     = new Guid(0x3692CA39, 0xE082, 0x4350, 0x9E, 0x1F, 0x37, 0x04, 0xCB, 0x08, 0x3C, 0xD5);
		public static readonly Guid CLSID_WICPngGamaMetadataWriter                     = new Guid(0xFF036D13, 0x5D4B, 0x46DD, 0xB1, 0x0F, 0x10, 0x66, 0x93, 0xD9, 0xFE, 0x4F);
		public static readonly Guid CLSID_WICPngHistMetadataReader                     = new Guid(0x877A0BB7, 0xA313, 0x4491, 0x87, 0xB5, 0x2E, 0x6D, 0x05, 0x94, 0xF5, 0x20);
		public static readonly Guid CLSID_WICPngHistMetadataWriter                     = new Guid(0x8A03E749, 0x672E, 0x446E, 0xBF, 0x1F, 0x2C, 0x11, 0xD2, 0x33, 0xB6, 0xFF);
		public static readonly Guid CLSID_WICPngIccpMetadataReader                     = new Guid(0xF5D3E63B, 0xCB0F, 0x4628, 0xA4, 0x78, 0x6D, 0x82, 0x44, 0xBE, 0x36, 0xB1);
		public static readonly Guid CLSID_WICPngIccpMetadataWriter                     = new Guid(0x16671E5F, 0x0CE6, 0x4CC4, 0x97, 0x68, 0xE8, 0x9F, 0xE5, 0x01, 0x8A, 0xDE);
		public static readonly Guid CLSID_WICPngItxtMetadataReader                     = new Guid(0xAABFB2FA, 0x3E1E, 0x4A8F, 0x89, 0x77, 0x55, 0x56, 0xFB, 0x94, 0xEA, 0x23);
		public static readonly Guid CLSID_WICPngItxtMetadataWriter                     = new Guid(0x31879719, 0xE751, 0x4DF8, 0x98, 0x1D, 0x68, 0xDF, 0xF6, 0x77, 0x04, 0xED);
		public static readonly Guid CLSID_WICPngSrgbMetadataReader                     = new Guid(0xFB40360C, 0x547E, 0x4956, 0xA3, 0xB9, 0xD4, 0x41, 0x88, 0x59, 0xBA, 0x66);
		public static readonly Guid CLSID_WICPngSrgbMetadataWriter                     = new Guid(0xA6EE35C6, 0x87EC, 0x47DF, 0x9F, 0x22, 0x1D, 0x5A, 0xAD, 0x84, 0x0C, 0x82);
		public static readonly Guid CLSID_WICPngTextMetadataReader                     = new Guid(0x4b59afcc, 0xb8c3, 0x408a, 0xb6, 0x70, 0x89, 0xe5, 0xfa, 0xb6, 0xfd, 0xa7);
		public static readonly Guid CLSID_WICPngTextMetadataWriter                     = new Guid(0xb5ebafb9, 0x253e, 0x4a72, 0xa7, 0x44, 0x07, 0x62, 0xd2, 0x68, 0x56, 0x83);
		public static readonly Guid CLSID_WICPngTimeMetadataReader                     = new Guid(0xD94EDF02, 0xEFE5, 0x4F0D, 0x85, 0xC8, 0xF5, 0xA6, 0x8B, 0x30, 0x00, 0xB1);
		public static readonly Guid CLSID_WICPngTimeMetadataWriter                     = new Guid(0x1AB78400, 0xB5A3, 0x4D91, 0x8A, 0xCE, 0x33, 0xFC, 0xD1, 0x49, 0x9B, 0xE6);
		public static readonly Guid CLSID_WICSubIfdMetadataReader                      = new Guid(0x50D42F09, 0xECD1, 0x4B41, 0xB6, 0x5D, 0xDA, 0x1F, 0xDA, 0xA7, 0x56, 0x63);
		public static readonly Guid CLSID_WICSubIfdMetadataWriter                      = new Guid(0x8ADE5386, 0x8E9B, 0x4F4C, 0xAC, 0xF2, 0xF0, 0x00, 0x87, 0x06, 0xB2, 0x38);
		public static readonly Guid CLSID_WICThumbnailMetadataReader                   = new Guid(0xfb012959, 0xf4f6, 0x44d7, 0x9d, 0x09, 0xda, 0xa0, 0x87, 0xa9, 0xdb, 0x57);
		public static readonly Guid CLSID_WICThumbnailMetadataWriter                   = new Guid(0xd049b20c, 0x5dd0, 0x44fe, 0xb0, 0xb3, 0x8f, 0x92, 0xc8, 0xe6, 0xd0, 0x80);
		public static readonly Guid CLSID_WICTiffDecoder                               = new Guid(0xb54e85d9, 0xfe23, 0x499f, 0x8b, 0x88, 0x6a, 0xce, 0xa7, 0x13, 0x75, 0x2b);
		public static readonly Guid CLSID_WICTiffEncoder                               = new Guid(0x0131be10, 0x2001, 0x4c5f, 0xa9, 0xb0, 0xcc, 0x88, 0xfa, 0xb6, 0x4c, 0xe8);
		public static readonly Guid CLSID_WICUnknownMetadataReader                     = new Guid(0x699745c2, 0x5066, 0x4b82, 0xa8, 0xe3, 0xd4, 0x04, 0x78, 0xdb, 0xec, 0x8c);
		public static readonly Guid CLSID_WICUnknownMetadataWriter                     = new Guid(0xa09cca86, 0x27ba, 0x4f39, 0x90, 0x53, 0x12, 0x1f, 0xa4, 0xdc, 0x08, 0xfc);
		public static readonly Guid CLSID_WICWmpDecoder                                = new Guid(0xa26cec36, 0x234c, 0x4950, 0xae, 0x16, 0xe3, 0x4a, 0xac, 0xe7, 0x1d, 0x0d);
		public static readonly Guid CLSID_WICWmpEncoder                                = new Guid(0xac4ce3cb, 0xe1c1, 0x44cd, 0x82, 0x15, 0x5a, 0x16, 0x65, 0x50, 0x9e, 0xc2);
		public static readonly Guid CLSID_WICXMPAltMetadataReader                      = new Guid(0xAA94DCC2, 0xB8B0, 0x4898, 0xB8, 0x35, 0x00, 0x0A, 0xAB, 0xD7, 0x43, 0x93);
		public static readonly Guid CLSID_WICXMPAltMetadataWriter                      = new Guid(0x076C2A6C, 0xF78F, 0x4C46, 0xA7, 0x23, 0x35, 0x83, 0xE7, 0x08, 0x76, 0xEA);
		public static readonly Guid CLSID_WICXMPBagMetadataReader                      = new Guid(0xE7E79A30, 0x4F2C, 0x4FAB, 0x8D, 0x00, 0x39, 0x4F, 0x2D, 0x6B, 0xBE, 0xBE);
		public static readonly Guid CLSID_WICXMPBagMetadataWriter                      = new Guid(0xED822C8C, 0xD6BE, 0x4301, 0xA6, 0x31, 0x0E, 0x14, 0x16, 0xBA, 0xD2, 0x8F);
		public static readonly Guid CLSID_WICXMPMetadataReader                         = new Guid(0x72B624DF, 0xAE11, 0x4948, 0xA6, 0x5C, 0x35, 0x1E, 0xB0, 0x82, 0x94, 0x19);
		public static readonly Guid CLSID_WICXMPMetadataWriter                         = new Guid(0x1765E14E, 0x1BD4, 0x462E, 0xB6, 0xB1, 0x59, 0x0B, 0xF1, 0x26, 0x2A, 0xC6);
		public static readonly Guid CLSID_WICXMPSeqMetadataReader                      = new Guid(0x7F12E753, 0xFC71, 0x43D7, 0xA5, 0x1D, 0x92, 0xF3, 0x59, 0x77, 0xAB, 0xB5);
		public static readonly Guid CLSID_WICXMPSeqMetadataWriter                      = new Guid(0x6D68D1DE, 0xD432, 0x4B0F, 0x92, 0x3A, 0x09, 0x11, 0x83, 0xA9, 0xBD, 0xA7);
		public static readonly Guid CLSID_WICXMPStructMetadataReader                   = new Guid(0x01B90D9A, 0x8209, 0x47F7, 0x9C, 0x52, 0xE1, 0x24, 0x4B, 0xF5, 0x0C, 0xED);
		public static readonly Guid CLSID_WICXMPStructMetadataWriter                   = new Guid(0x22C21F93, 0x7DDB, 0x411C, 0x9B, 0x17, 0xC5, 0xB7, 0xBD, 0x06, 0x4A, 0xBC);
		public static readonly Guid CLSID_WICDdsDecoder                                = new Guid(0x9053699f, 0xa341, 0x429d, 0x9e, 0x90, 0xee, 0x43, 0x7c, 0xf8, 0x0c, 0x73);
		public static readonly Guid CLSID_WICDdsEncoder                                = new Guid(0xa61dde94, 0x66ce, 0x4ac1, 0x88, 0x1b, 0x71, 0x68, 0x05, 0x88, 0x89, 0x5e);
		public static readonly Guid CLSID_WICDdsMetadataReader                         = new Guid(0x276c88ca, 0x7533, 0x4a86, 0xb6, 0x76, 0x66, 0xb3, 0x60, 0x80, 0xd4, 0x84);
		public static readonly Guid CLSID_WICDdsMetadataWriter                         = new Guid(0xfd688bbd, 0x31ed, 0x4db7, 0xa7, 0x23, 0x93, 0x49, 0x27, 0xd3, 0x83, 0x67);
		public static readonly Guid CLSID_WICAdngDecoder                               = new Guid(0x981d9411, 0x909e, 0x42a7, 0x8f, 0x5d, 0xa7, 0x47, 0xff, 0x05, 0x2e, 0xdb);
		public static readonly Guid CLSID_WICJpegQualcommPhoneEncoder                  = new Guid(0x68ed5c62, 0xf534, 0x4979, 0xb2, 0xb3, 0x68, 0x6a, 0x12, 0xb2, 0xb3, 0x4c);

		public static readonly Guid GUID_ContainerFormatBmp                            = new Guid(0x0af1d87e, 0xfcfe, 0x4188, 0xbd, 0xeb, 0xa7, 0x90, 0x64, 0x71, 0xcb, 0xe3);
		public static readonly Guid GUID_ContainerFormatGif                            = new Guid(0x1f8a5601, 0x7d4d, 0x4cbd, 0x9c, 0x82, 0x1b, 0xc8, 0xd4, 0xee, 0xb9, 0xa5);
		public static readonly Guid GUID_ContainerFormatIco                            = new Guid(0xa3a860c4, 0x338f, 0x4c17, 0x91, 0x9a, 0xfb, 0xa4, 0xb5, 0x62, 0x8f, 0x21);
		public static readonly Guid GUID_ContainerFormatJpeg                           = new Guid(0x19e4a5aa, 0x5662, 0x4fc5, 0xa0, 0xc0, 0x17, 0x58, 0x02, 0x8e, 0x10, 0x57);
		public static readonly Guid GUID_ContainerFormatPng                            = new Guid(0x1b7cfaf4, 0x713f, 0x473c, 0xbb, 0xcd, 0x61, 0x37, 0x42, 0x5f, 0xae, 0xaf);
		public static readonly Guid GUID_ContainerFormatTiff                           = new Guid(0x163bcc30, 0xe2e9, 0x4f0b, 0x96, 0x1d, 0xa3, 0xe9, 0xfd, 0xb7, 0x88, 0xa3);
		public static readonly Guid GUID_ContainerFormatWmp                            = new Guid(0x57a37caa, 0x367a, 0x4540, 0x91, 0x6b, 0xf1, 0x83, 0xc5, 0x09, 0x3a, 0x4b);
		public static readonly Guid GUID_ContainerFormatRaw                            = new Guid(0xc1fc85cb, 0xd64f, 0x478b, 0xa4, 0xec, 0x69, 0xad, 0xc9, 0xee, 0x13, 0x92);
		public static readonly Guid GUID_ContainerFormatDds                            = new Guid(0x9967cb95, 0x2e85, 0x4ac8, 0x8c, 0xa2, 0x83, 0xd7, 0xcc, 0xd4, 0x25, 0xc9);
		public static readonly Guid GUID_ContainerFormatAdng                           = new Guid(0xf3ff6d0d, 0x38c0, 0x41c4, 0xb1, 0xfe, 0x1f, 0x38, 0x24, 0xf1, 0x7b, 0x84);

		public static readonly Guid GUID_MetadataFormat8BIMIPTC                        = new Guid(0x0010568c, 0x0852, 0x4e6a, 0xb1, 0x91, 0x5c, 0x33, 0xac, 0x5b, 0x04, 0x30);
		public static readonly Guid GUID_MetadataFormat8BIMResolutionInfo              = new Guid(0x739F305D, 0x81DB, 0x43CB, 0xAC, 0x5E, 0x55, 0x01, 0x3E, 0xF9, 0xF0, 0x03);
		public static readonly Guid GUID_MetadataFormat8BIMIPTCDigest                  = new Guid(0x1CA32285, 0x9CCD, 0x4786, 0x8B, 0xD8, 0x79, 0x53, 0x9D, 0xB6, 0xA0, 0x06);
		public static readonly Guid GUID_MetadataFormatAPE                             = new Guid(0x2e043dc2, 0xC967, 0x4E05, 0x87, 0x5E, 0x61, 0x8B, 0xF6, 0x7E, 0x85, 0xC3);
		public static readonly Guid GUID_MetadataFormatApp0                            = new Guid(0x79007028, 0x268D, 0x45d6, 0xA3, 0xC2, 0x35, 0x4E, 0x6A, 0x50, 0x4B, 0xC9);
		public static readonly Guid GUID_MetadataFormatApp1                            = new Guid(0x8FD3DFC3, 0xF951, 0x492B, 0x81, 0x7F, 0x69, 0xC2, 0xE6, 0xD9, 0xA5, 0xB0);
		public static readonly Guid GUID_MetadataFormatApp13                           = new Guid(0x326556A2, 0xF502, 0x4354, 0x9C, 0xC0, 0x8E, 0x3F, 0x48, 0xEA, 0xF6, 0xB5);
		public static readonly Guid GUID_MetadataFormatChunkbKGD                       = new Guid(0xE14D3571, 0x6B47, 0x4DEA, 0xB6, 0x0A, 0x87, 0xCE, 0x0A, 0x78, 0xDF, 0xB7);
		public static readonly Guid GUID_MetadataFormatChunkcHRM                       = new Guid(0x9DB3655B, 0x2842, 0x44B3, 0x80, 0x67, 0x12, 0xE9, 0xB3, 0x75, 0x55, 0x6A);
		public static readonly Guid GUID_MetadataFormatChunkgAMA                       = new Guid(0xF00935A5, 0x1D5D, 0x4CD1, 0x81, 0xB2, 0x93, 0x24, 0xD7, 0xEC, 0xA7, 0x81);
		public static readonly Guid GUID_MetadataFormatChunkhIST                       = new Guid(0xC59A82DA, 0xDB74, 0x48A4, 0xBD, 0x6A, 0xB6, 0x9C, 0x49, 0x31, 0xEF, 0x95);
		public static readonly Guid GUID_MetadataFormatChunkiCCP                       = new Guid(0xEB4349AB, 0xB685, 0x450F, 0x91, 0xB5, 0xE8, 0x02, 0xE8, 0x92, 0x53, 0x6C);
		public static readonly Guid GUID_MetadataFormatChunkiTXt                       = new Guid(0xC2BEC729, 0x0B68, 0x4B77, 0xAA, 0x0E, 0x62, 0x95, 0xA6, 0xAC, 0x18, 0x14);
		public static readonly Guid GUID_MetadataFormatChunksRGB                       = new Guid(0xC115FD36, 0xCC6F, 0x4E3F, 0x83, 0x63, 0x52, 0x4B, 0x87, 0xC6, 0xB0, 0xD9);
		public static readonly Guid GUID_MetadataFormatChunktEXt                       = new Guid(0x568d8936, 0xc0a9, 0x4923, 0x90, 0x5d, 0xdf, 0x2b, 0x38, 0x23, 0x8f, 0xbc);
		public static readonly Guid GUID_MetadataFormatChunktIME                       = new Guid(0x6B00AE2D, 0xE24B, 0x460A, 0x98, 0xB6, 0x87, 0x8B, 0xD0, 0x30, 0x72, 0xFD);
		public static readonly Guid GUID_MetadataFormatExif                            = new Guid(0x1C3C4F9D, 0xB84A, 0x467D, 0x94, 0x93, 0x36, 0xCF, 0xBD, 0x59, 0xEA, 0x57);
		public static readonly Guid GUID_MetadataFormatGCE                             = new Guid(0x2A25CAD8, 0xDEEB, 0x4C69, 0xA7, 0x88, 0x0E, 0xC2, 0x26, 0x6D, 0xCA, 0xFD);
		public static readonly Guid GUID_MetadataFormatGifComment                      = new Guid(0xc4b6e0e0, 0xcfb4, 0x4ad3, 0xab, 0x33, 0x9a, 0xad, 0x23, 0x55, 0xa3, 0x4a);
		public static readonly Guid GUID_MetadataFormatGps                             = new Guid(0x7134AB8A, 0x9351, 0x44AD, 0xAF, 0x62, 0x44, 0x8D, 0xB6, 0xB5, 0x02, 0xEC);
		public static readonly Guid GUID_MetadataFormatIfd                             = new Guid(0x537396C6, 0x2D8A, 0x4BB6, 0x9B, 0xF8, 0x2F, 0x0A, 0x8E, 0x2A, 0x3A, 0xDF);
		public static readonly Guid GUID_MetadataFormatIMD                             = new Guid(0xBD2BB086, 0x4D52, 0x48DD, 0x96, 0x77, 0xDB, 0x48, 0x3E, 0x85, 0xAE, 0x8F);
		public static readonly Guid GUID_MetadataFormatInterop                         = new Guid(0xED686F8E, 0x681F, 0x4C8B, 0xBD, 0x41, 0xA8, 0xAD, 0xDB, 0xF6, 0xB3, 0xFC);
		public static readonly Guid GUID_MetadataFormatIPTC                            = new Guid(0x4FAB0914, 0xE129, 0x4087, 0xA1, 0xD1, 0xBC, 0x81, 0x2D, 0x45, 0xA7, 0xB5);
		public static readonly Guid GUID_MetadataFormatIPTCDigest                      = new Guid(0x1CA32285, 0x9CCD, 0x4786, 0x8B, 0xD8, 0x79, 0x53, 0x9D, 0xB6, 0xA0, 0x06);
		public static readonly Guid GUID_MetadataFormatIPTCDigestReader                = new Guid(0x02805F1E, 0xD5AA, 0x415b, 0x82, 0xC5, 0x61, 0xC0, 0x33, 0xA9, 0x88, 0xA6);
		public static readonly Guid GUID_MetadataFormatIPTCDigestWriter                = new Guid(0x2DB5E62B, 0x0D67, 0x495f, 0x8F, 0x9D, 0xC2, 0xF0, 0x18, 0x86, 0x47, 0xAC);
		public static readonly Guid GUID_MetadataFormatIRB                             = new Guid(0x16100D66, 0x8570, 0x4BB9, 0xB9, 0x2D, 0xFD, 0xA4, 0xB2, 0x3E, 0xCE, 0x67);
		public static readonly Guid GUID_MetadataFormatJpegChrominance                 = new Guid(0xF73D0DCF, 0xCEC6, 0x4F85, 0x9B, 0x0E, 0x1C, 0x39, 0x56, 0xB1, 0xBE, 0xF7);
		public static readonly Guid GUID_MetadataFormatJpegComment                     = new Guid(0x220E5F33, 0xAFD3, 0x474E, 0x9D, 0x31, 0x7D, 0x4F, 0xE7, 0x30, 0xF5, 0x57);
		public static readonly Guid GUID_MetadataFormatJpegLuminance                   = new Guid(0x86908007, 0xEDFC, 0x4860, 0x8D, 0x4B, 0x4E, 0xE6, 0xE8, 0x3E, 0x60, 0x58);
		public static readonly Guid GUID_MetadataFormatLSD                             = new Guid(0xE256031E, 0x6299, 0x4929, 0xB9, 0x8D, 0x5A, 0xC8, 0x84, 0xAF, 0xBA, 0x92);
		public static readonly Guid GUID_MetadataFormatSubIfd                          = new Guid(0x58A2E128, 0x2DB9, 0x4E57, 0xBB, 0x14, 0x51, 0x77, 0x89, 0x1E, 0xD3, 0x31);
		public static readonly Guid GUID_MetadataFormatThumbnail                       = new Guid(0x243dcee9, 0x8703, 0x40ee, 0x8e, 0xf0, 0x22, 0xa6, 0x00, 0xb8, 0x05, 0x8c);
		public static readonly Guid GUID_MetadataFormatUnknown                         = new Guid(0xA45E592F, 0x9078, 0x4A7C, 0xAD, 0xB5, 0x4E, 0xDC, 0x4F, 0xD6, 0x1B, 0x1F);
		public static readonly Guid GUID_MetadataFormatXMP                             = new Guid(0xBB5ACC38, 0xF216, 0x4CEC, 0xA6, 0xC5, 0x5F, 0x6E, 0x73, 0x97, 0x63, 0xA9);
		public static readonly Guid GUID_MetadataFormatXMPAlt                          = new Guid(0x7B08A675, 0x91AA, 0x481B, 0xA7, 0x98, 0x4D, 0xA9, 0x49, 0x08, 0x61, 0x3B);
		public static readonly Guid GUID_MetadataFormatXMPBag                          = new Guid(0x833CCA5F, 0xDCB7, 0x4516, 0x80, 0x6F, 0x65, 0x96, 0xAB, 0x26, 0xDC, 0xE4);
		public static readonly Guid GUID_MetadataFormatXMPSeq                          = new Guid(0x63E8DF02, 0xEB6C, 0x456C, 0xA2, 0x24, 0xB2, 0x5E, 0x79, 0x4F, 0xD6, 0x48);
		public static readonly Guid GUID_MetadataFormatXMPStruct                       = new Guid(0x22383CF1, 0xED17, 0x4E2E, 0xAF, 0x17, 0xD8, 0x5B, 0x8F, 0x6B, 0x30, 0xD0);
		public static readonly Guid GUID_MetadataFormatDds                             = new Guid(0x4a064603, 0x8c33, 0x4e60, 0x9c, 0x29, 0x13, 0x62, 0x31, 0x70, 0x2d, 0x08);

		public static readonly Guid GUID_VendorMicrosoft                               = new Guid(0xf0e749ca, 0xedef, 0x4589, 0xa7, 0x3a, 0xee, 0xe, 0x62, 0x6a, 0x2a, 0x2b);
		public static readonly Guid GUID_VendorMicrosoftBuiltIn                        = new Guid(0x257a30fd, 0x06b6, 0x462b, 0xae, 0xa4, 0x63, 0xf7, 0xb, 0x86, 0xe5, 0x33);

		public static readonly Guid GUID_WICPixelFormat1bppIndexed                     = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x01);
		public static readonly Guid GUID_WICPixelFormat2bppIndexed                     = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x02);
		public static readonly Guid GUID_WICPixelFormat4bppIndexed                     = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x03);
		public static readonly Guid GUID_WICPixelFormat8bppIndexed                     = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x04);

		public static readonly Guid GUID_WICPixelFormatBlackWhite                      = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x05);
		public static readonly Guid GUID_WICPixelFormat2bppGray                        = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x06);
		public static readonly Guid GUID_WICPixelFormat4bppGray                        = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x07);
		public static readonly Guid GUID_WICPixelFormat8bppGray                        = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x08);
		public static readonly Guid GUID_WICPixelFormat16bppGray                       = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x0b);
		public static readonly Guid GUID_WICPixelFormat16bppGrayFixedPoint             = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x13);
		public static readonly Guid GUID_WICPixelFormat16bppGrayHalf                   = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x3e);
		public static readonly Guid GUID_WICPixelFormat32bppGrayFixedPoint             = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x3f);
		public static readonly Guid GUID_WICPixelFormat32bppGrayFloat                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x11);

		public static readonly Guid GUID_WICPixelFormat16bppBGR555                     = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x09);
		public static readonly Guid GUID_WICPixelFormat16bppBGR565                     = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x0a);
		public static readonly Guid GUID_WICPixelFormat24bppBGR                        = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x0c);
		public static readonly Guid GUID_WICPixelFormat32bppBGR                        = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x0e);
		public static readonly Guid GUID_WICPixelFormat32bppBGR101010                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x14);
		public static readonly Guid GUID_WICPixelFormat48bppBGR                        = new Guid(0xe605a384, 0xb468, 0x46ce, 0xbb, 0x2e, 0x36, 0xf1, 0x80, 0xe6, 0x43, 0x13);
		public static readonly Guid GUID_WICPixelFormat48bppBGRFixedPoint              = new Guid(0x49ca140e, 0xcab6, 0x493b, 0x9d, 0xdf, 0x60, 0x18, 0x7c, 0x37, 0x53, 0x2a);

		public static readonly Guid GUID_WICPixelFormat16bppBGRA5551                   = new Guid(0x05ec7c2b, 0xf1e6, 0x4961, 0xad, 0x46, 0xe1, 0xcc, 0x81, 0x0a, 0x87, 0xd2);
		public static readonly Guid GUID_WICPixelFormat32bppBGRA                       = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x0f);
		public static readonly Guid GUID_WICPixelFormat64bppBGRA                       = new Guid(0x1562ff7c, 0xd352, 0x46f9, 0x97, 0x9e, 0x42, 0x97, 0x6b, 0x79, 0x22, 0x46);
		public static readonly Guid GUID_WICPixelFormat64bppBGRAFixedPoint             = new Guid(0x356de33c, 0x54d2, 0x4a23, 0xbb, 0x04, 0x9b, 0x7b, 0xf9, 0xb1, 0xd4, 0x2d);

		public static readonly Guid GUID_WICPixelFormat32bppPBGRA                      = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x10);
		public static readonly Guid GUID_WICPixelFormat64bppPBGRA                      = new Guid(0x8c518e8e, 0xa4ec, 0x468b, 0xae, 0x70, 0xc9, 0xa3, 0x5a, 0x9c, 0x55, 0x30);

		public static readonly Guid GUID_WICPixelFormat24bppRGB                        = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x0d);
		public static readonly Guid GUID_WICPixelFormat32bppRGB                        = new Guid(0xd98c6b95, 0x3efe, 0x47d6, 0xbb, 0x25, 0xeb, 0x17, 0x48, 0xab, 0x0c, 0xf1);
		public static readonly Guid GUID_WICPixelFormat32bppRGBE                       = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x3d);
		public static readonly Guid GUID_WICPixelFormat48bppRGB                        = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x15);
		public static readonly Guid GUID_WICPixelFormat48bppRGBFixedPoint              = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x12);
		public static readonly Guid GUID_WICPixelFormat48bppRGBHalf                    = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x3b);
		public static readonly Guid GUID_WICPixelFormat64bppRGB                        = new Guid(0xa1182111, 0x186d, 0x4d42, 0xbc, 0x6a, 0x9c, 0x83, 0x03, 0xa8, 0xdf, 0xf9);
		public static readonly Guid GUID_WICPixelFormat64bppRGBFixedPoint              = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x40);
		public static readonly Guid GUID_WICPixelFormat64bppRGBHalf                    = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x42);
		public static readonly Guid GUID_WICPixelFormat96bppRGBFixedPoint              = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x18);
		public static readonly Guid GUID_WICPixelFormat96bppRGBFloat                   = new Guid(0xe3fed78f, 0xe8db, 0x4acf, 0x84, 0xc1, 0xe9, 0x7f, 0x61, 0x36, 0xb3, 0x27);
		public static readonly Guid GUID_WICPixelFormat128bppRGBFixedPoint             = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x41);
		public static readonly Guid GUID_WICPixelFormat128bppRGBFloat                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x1b);

		public static readonly Guid GUID_WICPixelFormat32bppRGBA                       = new Guid(0xf5c7ad2d, 0x6a8d, 0x43dd, 0xa7, 0xa8, 0xa2, 0x99, 0x35, 0x26, 0x1a, 0xe9);
		public static readonly Guid GUID_WICPixelFormat32bppRGBA1010102                = new Guid(0x25238D72, 0xFCF9, 0x4522, 0xb5, 0x14, 0x55, 0x78, 0xe5, 0xad, 0x55, 0xe0);
		public static readonly Guid GUID_WICPixelFormat32bppRGBA1010102XR              = new Guid(0x00DE6B9A, 0xC101, 0x434b, 0xb5, 0x02, 0xd0, 0x16, 0x5e, 0xe1, 0x12, 0x2c);
		public static readonly Guid GUID_WICPixelFormat64bppRGBA                       = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x16);
		public static readonly Guid GUID_WICPixelFormat64bppRGBAFixedPoint             = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x1d);
		public static readonly Guid GUID_WICPixelFormat64bppRGBAHalf                   = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x3a);
		public static readonly Guid GUID_WICPixelFormat128bppRGBAFixedPoint            = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x1e);
		public static readonly Guid GUID_WICPixelFormat128bppRGBAFloat                 = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x19);

		public static readonly Guid GUID_WICPixelFormat32bppPRGBA                      = new Guid(0x3cc4a650, 0xa527, 0x4d37, 0xa9, 0x16, 0x31, 0x42, 0xc7, 0xeb, 0xed, 0xba);
		public static readonly Guid GUID_WICPixelFormat64bppPRGBA                      = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x17);
		public static readonly Guid GUID_WICPixelFormat64bppPRGBAHalf                  = new Guid(0x58ad26c2, 0xc623, 0x4d9d, 0xb3, 0x20, 0x38, 0x7e, 0x49, 0xf8, 0xc4, 0x42);
		public static readonly Guid GUID_WICPixelFormat128bppPRGBAFloat                = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x1a);

		public static readonly Guid GUID_WICPixelFormat8bppAlpha                       = new Guid(0xe6cd0116, 0xeeba, 0x4161, 0xaa, 0x85, 0x27, 0xdd, 0x9f, 0xb3, 0xa8, 0x95);

		public static readonly Guid GUID_WICPixelFormat32bppCMYK                       = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x1c);
		public static readonly Guid GUID_WICPixelFormat40bppCMYKAlpha                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x2c);
		public static readonly Guid GUID_WICPixelFormat64bppCMYK                       = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x1f);
		public static readonly Guid GUID_WICPixelFormat80bppCMYKAlpha                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x2d);

		public static readonly Guid GUID_WICPixelFormat8bppY                           = new Guid(0x91B4DB54, 0x2DF9, 0x42F0, 0xB4, 0x49, 0x29, 0x09, 0xBB, 0x3D, 0xF8, 0x8E);
		public static readonly Guid GUID_WICPixelFormat8bppCb                          = new Guid(0x1339F224, 0x6BFE, 0x4C3E, 0x93, 0x02, 0xE4, 0xF3, 0xA6, 0xD0, 0xCA, 0x2A);
		public static readonly Guid GUID_WICPixelFormat8bppCr                          = new Guid(0xB8145053, 0x2116, 0x49F0, 0x88, 0x35, 0xED, 0x84, 0x4B, 0x20, 0x5C, 0x51);
		public static readonly Guid GUID_WICPixelFormat16bppCbCr                       = new Guid(0xFF95BA6E, 0x11E0, 0x4263, 0xBB, 0x45, 0x01, 0x72, 0x1F, 0x34, 0x60, 0xA4);
		public static readonly Guid GUID_WICPixelFormat16bppYQuantizedDctCoefficients  = new Guid(0xA355F433, 0x48E8, 0x4A42, 0x84, 0xD8, 0xE2, 0xAA, 0x26, 0xCA, 0x80, 0xA4);
		public static readonly Guid GUID_WICPixelFormat16bppCbQuantizedDctCoefficients = new Guid(0xD2C4FF61, 0x56A5, 0x49C2, 0x8B, 0x5C, 0x4C, 0x19, 0x25, 0x96, 0x48, 0x37);
		public static readonly Guid GUID_WICPixelFormat16bppCrQuantizedDctCoefficients = new Guid(0x2FE354F0, 0x1680, 0x42D8, 0x92, 0x31, 0xE7, 0x3C, 0x05, 0x65, 0xBF, 0xC1);

		public static readonly Guid GUID_WICPixelFormat24bpp3Channels                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x20);
		public static readonly Guid GUID_WICPixelFormat48bpp3Channels                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x26);
		public static readonly Guid GUID_WICPixelFormat32bpp4Channels                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x21);
		public static readonly Guid GUID_WICPixelFormat64bpp4Channels                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x27);
		public static readonly Guid GUID_WICPixelFormat40bpp5Channels                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x22);
		public static readonly Guid GUID_WICPixelFormat80bpp5Channels                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x28);
		public static readonly Guid GUID_WICPixelFormat48bpp6Channels                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x23);
		public static readonly Guid GUID_WICPixelFormat96bpp6Channels                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x29);
		public static readonly Guid GUID_WICPixelFormat56bpp7Channels                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x24);
		public static readonly Guid GUID_WICPixelFormat112bpp7Channels                 = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x2a);
		public static readonly Guid GUID_WICPixelFormat64bpp8Channels                  = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x25);
		public static readonly Guid GUID_WICPixelFormat128bpp8Channels                 = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x2b);

		public static readonly Guid GUID_WICPixelFormat32bpp3ChannelsAlpha             = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x2e);
		public static readonly Guid GUID_WICPixelFormat64bpp3ChannelsAlpha             = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x34);
		public static readonly Guid GUID_WICPixelFormat40bpp4ChannelsAlpha             = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x2f);
		public static readonly Guid GUID_WICPixelFormat80bpp4ChannelsAlpha             = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x35);
		public static readonly Guid GUID_WICPixelFormat48bpp5ChannelsAlpha             = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x30);
		public static readonly Guid GUID_WICPixelFormat96bpp5ChannelsAlpha             = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x36);
		public static readonly Guid GUID_WICPixelFormat56bpp6ChannelsAlpha             = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x31);
		public static readonly Guid GUID_WICPixelFormat112bpp6ChannelsAlpha            = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x37);
		public static readonly Guid GUID_WICPixelFormat64bpp7ChannelsAlpha             = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x32);
		public static readonly Guid GUID_WICPixelFormat128bpp7ChannelsAlpha            = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x38);
		public static readonly Guid GUID_WICPixelFormat72bpp8ChannelsAlpha             = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x33);
		public static readonly Guid GUID_WICPixelFormat144bpp8ChannelsAlpha            = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x39);

		public static readonly Guid GUID_WICPixelFormatDontCare                        = new Guid(0x6fddc324, 0x4e03, 0x4bfe, 0xb1, 0x85, 0x3d, 0x77, 0x76, 0x8d, 0xc9, 0x00);
		public static readonly Guid GUID_WICPixelFormatUndefined = GUID_WICPixelFormatDontCare;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal class WICRect
	{
		public int X;
		public int Y;
		public int Width;
		public int Height;
	}

	internal enum WICColorContextType : uint
	{
		WICColorContextUninitialized = 0x00000000,
		WICColorContextProfile = 0x00000001,
		WICColorContextExifColorSpace = 0x00000002,
	}

	internal enum WICBitmapCreateCacheOption : uint
	{
		WICBitmapNoCache = 0x00000000,
		WICBitmapCacheOnDemand = 0x00000001,
		WICBitmapCacheOnLoad = 0x00000002,
	}

	internal enum WICDecodeOptions : uint
	{
		WICDecodeMetadataCacheOnDemand = 0x00000000,
		WICDecodeMetadataCacheOnLoad = 0x00000001,
	}

	internal enum WICBitmapEncoderCacheOption : uint
	{
		WICBitmapEncoderCacheInMemory = 0x00000000,
		WICBitmapEncoderCacheTempFile = 0x00000001,
		WICBitmapEncoderNoCache = 0x00000002,
	}

	[Flags]
	internal enum WICComponentType : uint
	{
		WICDecoder = 0x00000001,
		WICEncoder = 0x00000002,
		WICPixelFormatConverter = 0x00000004,
		WICMetadataReader = 0x00000008,
		WICMetadataWriter = 0x00000010,
		WICPixelFormat = 0x00000020,
		WICAllComponents = 0x0000003F,
	}

	internal enum WICComponentEnumerateOptions : uint
	{
		WICComponentEnumerateDefault = 0x00000000,
		WICComponentEnumerateRefresh = 0x00000001,
		WICComponentEnumerateDisabled = 0x80000000,
		WICComponentEnumerateUnsigned = 0x40000000,
	}

	internal struct WICBitmapPattern
	{
		public ulong Position;
		public uint Length;
		public IntPtr Pattern;
		public IntPtr Mask;
		[MarshalAs(UnmanagedType.Bool)]
		public bool EndOfStream;
	}

	internal enum WICBitmapInterpolationMode : uint
	{
		WICBitmapInterpolationModeNearestNeighbor = 0x00000000,
		WICBitmapInterpolationModeLinear = 0x00000001,
		WICBitmapInterpolationModeCubic = 0x00000002,
		WICBitmapInterpolationModeFant = 0x00000003,
		WICBitmapInterpolationModeHighQualityCubic = 0x00000004,
	}

	internal enum WICBitmapPaletteType : uint
	{
		WICBitmapPaletteTypeCustom = 0x00000000,
		WICBitmapPaletteTypeMedianCut = 0x00000001,
		WICBitmapPaletteTypeFixedBW = 0x00000002,
		WICBitmapPaletteTypeFixedHalftone8 = 0x00000003,
		WICBitmapPaletteTypeFixedHalftone27 = 0x00000004,
		WICBitmapPaletteTypeFixedHalftone64 = 0x00000005,
		WICBitmapPaletteTypeFixedHalftone125 = 0x00000006,
		WICBitmapPaletteTypeFixedHalftone216 = 0x00000007,
		WICBitmapPaletteTypeFixedWebPalette = WICBitmapPaletteTypeFixedHalftone216,
		WICBitmapPaletteTypeFixedHalftone252 = 0x00000008,
		WICBitmapPaletteTypeFixedHalftone256 = 0x00000009,
		WICBitmapPaletteTypeFixedGray4 = 0x0000000A,
		WICBitmapPaletteTypeFixedGray16 = 0x0000000B,
		WICBitmapPaletteTypeFixedGray256 = 0x0000000C
	}

	internal enum WICBitmapDitherType : uint
	{
		WICBitmapDitherTypeNone = 0x00000000,
		WICBitmapDitherTypeSolid = 0x00000000,
		WICBitmapDitherTypeOrdered4x4 = 0x00000001,
		WICBitmapDitherTypeOrdered8x8 = 0x00000002,
		WICBitmapDitherTypeOrdered16x16 = 0x00000003,
		WICBitmapDitherTypeSpiral4x4 = 0x00000004,
		WICBitmapDitherTypeSpiral8x8 = 0x00000005,
		WICBitmapDitherTypeDualSpiral4x4 = 0x00000006,
		WICBitmapDitherTypeDualSpiral8x8 = 0x00000007,
		WICBitmapDitherTypeErrorDiffusion = 0x00000008,
	}

	internal enum WICBitmapAlphaChannelOption : uint
	{
		WICBitmapUseAlpha = 0x00000000,
		WICBitmapUsePremultipliedAlpha = 0x00000001,
		WICBitmapIgnoreAlpha = 0x00000002,
	}

	[Flags]
	internal enum WICBitmapTransformOptions : uint
	{
		WICBitmapTransformRotate0 = 0x00000000,
		WICBitmapTransformRotate90 = 0x00000001,
		WICBitmapTransformRotate180 = 0x00000002,
		WICBitmapTransformRotate270 = 0x00000003,
		WICBitmapTransformFlipHorizontal = 0x00000008,
		WICBitmapTransformFlipVertical = 0x00000010,
	}

	[Flags]
	internal enum WICBitmapLockFlags : uint
	{
		WICBitmapLockRead = 0x00000001,
		WICBitmapLockWrite = 0x00000002,
	}

	[Flags]
	internal enum WICBitmapDecoderCapabilities : uint
	{
		WICBitmapDecoderCapabilitySameEncoder = 0x00000001,
		WICBitmapDecoderCapabilityCanDecodeAllImages = 0x00000002,
		WICBitmapDecoderCapabilityCanDecodeSomeImages = 0x00000004,
		WICBitmapDecoderCapabilityCanEnumerateMetadata = 0x00000008,
		WICBitmapDecoderCapabilityCanDecodeThumbnail = 0x00000010,
	}

	internal enum WICProgressOperation : uint
	{
		WICProgressOperationCopyPixels = 0x00000001,
		WICProgressOperationWritePixels = 0x00000002,
		WICProgressOperationAll = 0x0000FFFF,
	}

	internal enum WICProgressNotification : uint
	{
		WICProgressNotificationBegin = 0x00010000,
		WICProgressNotificationEnd = 0x00020000,
		WICProgressNotificationFrequent = 0x00040000,
		WICProgressNotificationAll = 0xFFFF0000,
	}

	[Flags]
	internal enum WICComponentSigning : uint
	{
		WICComponentSigned = 0x00000001,
		WICComponentUnsigned = 0x00000002,
		WICComponentSafe = 0x00000004,
		WICComponentDisabled = 0x80000000,
	}

	[ComImport, Guid("00000040-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICPalette
	{
		void InitializePredefined(
			WICBitmapPaletteType ePaletteType,
			[MarshalAs(UnmanagedType.Bool)]
			bool fAddTransparentColor
		);

		void InitializeCustom(
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
			uint[] pColors,
			uint cCount
		);

		void InitializeFromBitmap(
			IWICBitmapSource pISurface,
			uint cCount,
			[MarshalAs(UnmanagedType.Bool)]
			bool fAddTransparentColor
		);

		void InitializeFromPalette(
			IWICPalette pIPalette
		);

		WICBitmapPaletteType GetType();

		uint GetColorCount();

		uint GetColors(
			uint cCount,
			[Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			uint[] pColors
		);

		[return: MarshalAs(UnmanagedType.Bool)]
		bool IsBlackWhite();

		[return: MarshalAs(UnmanagedType.Bool)]
		bool IsGrayscale();

		[return: MarshalAs(UnmanagedType.Bool)]
		bool HasAlpha();
	}

	[ComImport, Guid("00000120-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmapSource
	{
		void GetSize(
			out uint puiWidth,
			out uint puiHeight
		);

		Guid GetPixelFormat();

		void GetResolution(
			out double pDpiX,
			out double pDpiY
		);

		void CopyPalette(
			IWICPalette pIPalette
		);

		void CopyPixels(
			WICRect prc,
			uint cbStride,
			uint cbBufferSize,
			IntPtr pbBuffer
		);
	}

	[ComImport, Guid("00000301-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICFormatConverter : IWICBitmapSource
	{
		#region IWICBitmapSource
		new void GetSize(
			out uint puiWidth,
			out uint puiHeight
		);

		new Guid GetPixelFormat();

		new void GetResolution(
			out double pDpiX,
			out double pDpiY
		);

		new void CopyPalette(
			IWICPalette pIPalette
		);

		new void CopyPixels(
			WICRect prc,
			uint cbStride,
			uint cbBufferSize,
			IntPtr pbBuffer
		);
		#endregion IWICBitmapSource

		void Initialize(
			IWICBitmapSource pISource,
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid dstFormat,
			WICBitmapDitherType dither,
			IWICPalette pIPalette,
			double alphaThresholdPercent,
			WICBitmapPaletteType paletteTranslate
		);

		[return: MarshalAs(UnmanagedType.Bool)]
		bool CanConvert(
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid srcPixelFormat,
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid dstPixelFormat
		);
	}

	[ComImport, Guid("00000302-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmapScaler : IWICBitmapSource
	{
		#region IWICBitmapSource
		new void GetSize(
			out uint puiWidth,
			out uint puiHeight
		);

		new Guid GetPixelFormat();

		new void GetResolution(
			out double pDpiX,
			out double pDpiY
		);

		new void CopyPalette(
			IWICPalette pIPalette
		);

		new void CopyPixels(
			WICRect prc,
			uint cbStride,
			uint cbBufferSize,
			IntPtr pbBuffer
		);
		#endregion IWICBitmapSource

		void Initialize(
			IWICBitmapSource pISource,
			uint uiWidth,
			uint uiHeight,
			WICBitmapInterpolationMode mode
		);
	}

	[ComImport, Guid("E4FBCF03-223D-4e81-9333-D635556DD1B5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmapClipper : IWICBitmapSource
	{
		#region IWICBitmapSource
		new void GetSize(
			out uint puiWidth,
			out uint puiHeight
		);

		new Guid GetPixelFormat();

		new void GetResolution(
			out double pDpiX,
			out double pDpiY
		);

		new void CopyPalette(
			IWICPalette pIPalette
		);

		new void CopyPixels(
			WICRect prc,
			uint cbStride,
			uint cbBufferSize,
			IntPtr pbBuffer
		);
		#endregion IWICBitmapSource

		void Initialize(
			IWICBitmapSource pISource,
			WICRect prc
		);
	}

	[ComImport, Guid("5009834F-2D6A-41ce-9E1B-17C5AFF7A782"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmapFlipRotator : IWICBitmapSource
	{
		#region IWICBitmapSource
		new void GetSize(
			out uint puiWidth,
			out uint puiHeight
		);

		new Guid GetPixelFormat();

		new void GetResolution(
			out double pDpiX,
			out double pDpiY
		);

		new void CopyPalette(
			IWICPalette pIPalette
		);

		new void CopyPixels(
			WICRect prc,
			uint cbStride,
			uint cbBufferSize,
			IntPtr pbBuffer
		);
		#endregion IWICBitmapSource

		void Initialize(
			IWICBitmapSource pISource,
			WICBitmapTransformOptions options
		);
	}

	[ComImport, Guid("00000123-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmapLock
	{
		void GetSize(
			out uint puiWidth,
			out uint puiHeight
		);

		uint GetStride();

		void GetDataPointer(
			out uint pcbBufferSize,
			out IntPtr ppbData
		);

		Guid GetPixelFormat();
	}

	[ComImport, Guid("00000121-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmap : IWICBitmapSource
	{
		#region IWICBitmapSource
		new void GetSize(
			out uint puiWidth,
			out uint puiHeight
		);

		new Guid GetPixelFormat();

		new void GetResolution(
			out double pDpiX,
			out double pDpiY
		);

		new void CopyPalette(
			IWICPalette pIPalette
		);

		new void CopyPixels(
			WICRect prc,
			uint cbStride,
			uint cbBufferSize,
			IntPtr pbBuffer
		);
		#endregion IWICBitmapSource

		IWICBitmapLock Lock(
			WICRect prcLock,
			WICBitmapLockFlags flags
		);

		void SetPalette(
			IWICPalette pIPalette
		);

		void SetResolution(
			double dpiX,
			double dpiY
		);
	}

	internal enum ExifColorSpace : uint
	{
		sRGB = 0x00000001,
		AdobeRGB = 0x00000002,
		Uncalibrated = 0x0000ffff,
	}

	[ComImport, Guid("3C613A02-34B2-44ea-9A7C-45AEA9C6FD6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICColorContext
	{
		void InitializeFromFilename(
			[MarshalAs(UnmanagedType.LPWStr)]
			string wzFilename
		);

		void InitializeFromMemory(
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
			byte[] pbBuffer,
			uint cbBufferSize
		);

		void InitializeFromExifColorSpace(
			ExifColorSpace value
		);

		WICColorContextType GetType();

		uint GetProfileBytes(
			uint cbBuffer,
			[In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] 
			byte[] pbBuffer
		);

		ExifColorSpace GetExifColorSpace();
	}

	[ComImport, Guid("B66F034F-D0E2-40ab-B436-6DE39E321A94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICColorTransform : IWICBitmapSource
	{
		#region IWICBitmapSource
		new void GetSize(
			out uint puiWidth,
			out uint puiHeight
		);

		new Guid GetPixelFormat();

		new void GetResolution(
			out double pDpiX,
			out double pDpiY
		);

		new void CopyPalette(
			IWICPalette pIPalette
		);

		new void CopyPixels(
			WICRect prc,
			uint cbStride,
			uint cbBufferSize,
			IntPtr pbBuffer
		);
		#endregion IWICBitmapSource

		void Initialize(
			IWICBitmapSource pIBitmapSource,
			IWICColorContext pIContextSource,
			IWICColorContext pIContextDest,
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid pixelFmtDest
		);
	}

	[ComImport, Guid("B84E2C09-78C9-4AC4-8BD3-524AE1663A2F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICFastMetadataEncoder
	{
		void Commit();

		IWICMetadataQueryWriter GetMetadataQueryWriter();
	}

	[ComImport, Guid("135FF860-22B7-4ddf-B0F6-218F4F299A43"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICStream : IStream
	{
		#region IStream
		new void Read(
			[Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] 
			byte[] pv, 
			int cb, 
			IntPtr pcbRead
		);

		new void Write(
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] 
			byte[] pv, 
			int cb, 
			IntPtr pcbWritten
		);

		new void Seek(
			long dlibMove, 
			int dwOrigin, 
			IntPtr plibNewPosition
		);

		new void SetSize(
			long libNewSize
		);

		new void CopyTo(
			IStream pstm, 
			long cb, 
			IntPtr pcbRead, 
			IntPtr pcbWritten
		);

		new void Commit(
			int grfCommitFlags
		);

		new void Revert();

		new void LockRegion(
			long libOffset, 
			long cb, 
			int dwLockType
		);

		new void UnlockRegion(
			long libOffset, 
			long cb, 
			int dwLockType
		);

		new void Stat(
			out STATSTG pstatstg, 
			int grfStatFlag
		);

		new void Clone(
			out IStream ppstm
		);
		#endregion IStream

		void InitializeFromIStream(
			IStream pIStream
		);

		void InitializeFromFilename(
			[MarshalAs(UnmanagedType.LPWStr)]
			string wzFileName,
			GenericAccessRights dwDesiredAccess
		);

		void InitializeFromMemory(
			IntPtr pbBuffer,
			uint cbBufferSize
		);

		void InitializeFromIStreamRegion(
			IStream pIStream,
			ulong ulOffset,
			ulong ulMaxSize
		);
	}

	[ComImport, Guid("DC2BB46D-3F07-481E-8625-220C4AEDBB33"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICEnumMetadataItem
	{
		uint Next(
			uint celt,
			[In, Out, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant rgeltSchema,
			[In, Out, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant rgeltId,
			[In, Out, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant rgeltValue
		);

		void Skip(
			uint celt
		);

		void Reset();

		IWICEnumMetadataItem Clone();
	}

	[ComImport, Guid("30989668-E1C9-4597-B395-458EEDB808DF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICMetadataQueryReader
	{
		Guid GetContainerFormat();

		uint GetLocation(
			uint cchMaxLength,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzNamespace
		);

		void GetMetadataByName(
			[MarshalAs(UnmanagedType.LPWStr)]
			string wzName,
			[In, Out, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarValue
		);

		IEnumString GetEnumerator();
	}

	[ComImport, Guid("A721791A-0DEF-4d06-BD91-2118BF1DB10B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICMetadataQueryWriter : IWICMetadataQueryReader
	{
		#region IWICMetadataQueryReader
		new Guid GetContainerFormat();

		new uint GetLocation(
			uint cchMaxLength,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzNamespace
		);

		new void GetMetadataByName(
			[MarshalAs(UnmanagedType.LPWStr)]
			string wzName,
			[In, Out, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarValue
		);

		new IEnumString GetEnumerator();
		#endregion

		void SetMetadataByName(
			[MarshalAs(UnmanagedType.LPWStr)]
			string wzName,
			[In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarValue
		);

		void RemoveMetadataByName(
			[MarshalAs(UnmanagedType.LPWStr)]
			string wzName
		);
	}

	[ComImport, Guid("00000103-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmapEncoder
	{
		void Initialize(
			IStream pIStream,
			WICBitmapEncoderCacheOption cacheOption
		);

		Guid GetContainerFormat();

		IWICBitmapEncoderInfo GetEncoderInfo();

		void SetColorContexts(
			uint cCount,
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			IWICColorContext[] ppIColorContext
		);

		void SetPalette(
			IWICPalette pIPalette
		);

		void SetThumbnail(
			IWICBitmapSource pIThumbnail
		);

		void SetPreview(
			IWICBitmapSource pIPreview
		);

		void CreateNewFrame(
			out IWICBitmapFrameEncode ppIFrameEncode,
			ref IPropertyBag2 ppIEncoderOptions
		);

		void Commit();

		IWICMetadataQueryWriter GetMetadataQueryWriter();
	}

	[ComImport, Guid("00000105-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmapFrameEncode
	{
		void Initialize(
			IPropertyBag2 pIEncoderOptions
		);

		void SetSize(
			uint uiWidth,
			uint uiHeight
		);

		void SetResolution(
			double dpiX,
			double dpiY
		);

		void SetPixelFormat(
			ref Guid pPixelFormat
		);

		void SetColorContexts(
			uint cCount,
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			IWICColorContext[] ppIColorContext
		);

		void SetPalette(
			IWICPalette pIPalette
		);

		void SetThumbnail(
			IWICBitmapSource pIThumbnail
		);

		void WritePixels(
			uint lineCount,
			uint cbStride,
			uint cbBufferSize,
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
			byte[] pbPixels
		);

		void WriteSource(
			IWICBitmapSource pIBitmapSource,
			WICRect prc
		);

		void Commit();

		IWICMetadataQueryWriter GetMetadataQueryWriter();
	}

	[ComImport, Guid("9EDDE9E7-8DEE-47ea-99DF-E6FAF2ED44BF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmapDecoder
	{
		WICBitmapDecoderCapabilities QueryCapability(
			IStream pIStream
		);

		void Initialize(
			IStream pIStream,
			WICDecodeOptions cacheOptions
		);

		Guid GetContainerFormat();

		IWICBitmapDecoderInfo GetDecoderInfo();

		void CopyPalette(
			IWICPalette pIPalette
		);

		IWICMetadataQueryReader GetMetadataQueryReader();

		IWICBitmapSource GetPreview();

		uint GetColorContexts(
			uint cCount,
			[In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			IWICColorContext[] ppIColorContexts
		);

		IWICBitmapSource GetThumbnail();

		uint GetFrameCount();

		IWICBitmapFrameDecode GetFrame(
			uint index
		);
	}

	[ComImport, Guid("3B16811B-6A43-4ec9-B713-3D5A0C13B940"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmapSourceTransform
	{
		void CopyPixels(
			WICRect prcSrc,
			uint uiWidth,
			uint uiHeight,
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid pguidDstFormat,
			WICBitmapTransformOptions dstTransform,
			uint nStride,
			uint cbBufferSize,
			IntPtr pbBuffer
		);

		void GetClosestSize(
			ref uint puiWidth,
			ref uint puiHeight
		);

		void GetClosestPixelFormat(
			ref Guid pguidDstFormat
		);

		[return: MarshalAs(UnmanagedType.Bool)]
		bool DoesSupportTransform(
			WICBitmapTransformOptions dstTransform
		);
	}

	[ComImport, Guid("3B16811B-6A43-4ec9-A813-3D930C13B940"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmapFrameDecode : IWICBitmapSource
	{
		#region IWICBitmapSource
		new void GetSize(
			out uint puiWidth,
			out uint puiHeight
		);

		new Guid GetPixelFormat();

		new void GetResolution(
			out double pDpiX,
			out double pDpiY
		);

		new void CopyPalette(
			IWICPalette pIPalette
		);

		new void CopyPixels(
			WICRect prc,
			uint cbStride,
			uint cbBufferSize,
			IntPtr pbBuffer
		);
		#endregion IWICBitmapSource

		IWICMetadataQueryReader GetMetadataQueryReader();

		uint GetColorContexts(
			uint cCount,
			[In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			IWICColorContext[] pIColorContexts
		);

		IWICBitmapSource GetThumbnail();
	}

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	[return: MarshalAs(UnmanagedType.Error)]
	internal delegate int PFNProgressNotification(
		IntPtr pvData,
		uint uFrameNum,
		WICProgressOperation operation,
		double dblProgress
	);

	[ComImport, Guid("64C1024E-C3CF-4462-8078-88C2B11C46D9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmapCodecProgressNotification
	{
		void RegisterProgressNotification(
			[MarshalAs(UnmanagedType.FunctionPtr)]
			PFNProgressNotification pfnProgressNotification,
			IntPtr pvData,
			uint dwProgressFlags /* WICProgressOperation | WICProgressNotification */
		);
	}

	[ComImport, Guid("23BC3F0A-698B-4357-886B-F24D50671334"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICComponentInfo
{
		WICComponentType GetComponentType();

		Guid GetCLSID();

		WICComponentSigning GetSigningStatus();

		uint GetAuthor(
			uint cchAuthor,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzAuthor
		);

		Guid GetVendorGUID();

		uint GetVersion(
			uint cchVersion,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzVersion
		);

		uint GetSpecVersion(
			uint cchSpecVersion,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzSpecVersion
		);

		uint GetFriendlyName(
			uint cchFriendlyName,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzFriendlyName
		);
	}

	[ComImport, Guid("9F34FB65-13F4-4f15-BC57-3726B5E53D9F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICFormatConverterInfo : IWICComponentInfo
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
		#endregion IWICComponentInfo

		uint GetPixelFormats(
			uint cFormats,
			[In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			Guid[] pPixelFormatGUIDs
		);

		IWICFormatConverter CreateInstance();
	}

	[ComImport, Guid("E87A44C4-B76E-4c47-8B09-298EB12A2714"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmapCodecInfo : IWICComponentInfo
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
		#endregion IWICComponentInfo

		Guid GetContainerFormat();

		uint GetPixelFormats(
			uint cFormats,
			[In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			Guid[] pguidPixelFormats
		);

		uint GetColorManagementVersion(
			uint cchColorManagementVersion,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzColorManagementVersion
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

		uint GetMimeTypes(
			uint cchMimeTypes,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzMimeTypes
		);

		uint GetFileExtensions(
			uint cchFileExtensions,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzFileExtensions
		);

		[return: MarshalAs(UnmanagedType.Bool)]
		bool DoesSupportAnimation();

		[return: MarshalAs(UnmanagedType.Bool)]
		bool DoesSupportChromakey();

		[return: MarshalAs(UnmanagedType.Bool)]
		bool DoesSupportLossless();

		[return: MarshalAs(UnmanagedType.Bool)]
		bool DoesSupportMultiframe();

		[return: MarshalAs(UnmanagedType.Bool)]
		bool MatchesMimeType(
			[MarshalAs(UnmanagedType.LPWStr)]
			string wzMimeType
		);
	}

	[ComImport, Guid("94C9B4EE-A09F-4f92-8A1E-4A9BCE7E76FB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmapEncoderInfo : IWICBitmapCodecInfo
	{
		#region IWICBitmapCodecInfo
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
		#endregion IWICComponentInfo

		new Guid GetContainerFormat();

		new uint GetPixelFormats(
			uint cFormats,
			[In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			Guid[] pguidPixelFormats
		);

		new uint GetColorManagementVersion(
			uint cchColorManagementVersion,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzColorManagementVersion
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

		new uint GetMimeTypes(
			uint cchMimeTypes,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzMimeTypes
		);

		new uint GetFileExtensions(
			uint cchFileExtensions,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzFileExtensions
		);

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool DoesSupportAnimation();

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool DoesSupportChromakey();

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool DoesSupportLossless();

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool DoesSupportMultiframe();

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool MatchesMimeType(
			[MarshalAs(UnmanagedType.LPWStr)]
			string wzMimeType
		);
		#endregion IWICBitmapCodecInfo

		IWICBitmapEncoder CreateInstance();
	}

	[ComImport, Guid("D8CD007F-D08F-4191-9BFC-236EA7F0E4B5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICBitmapDecoderInfo : IWICBitmapCodecInfo
	{
		#region IWICBitmapCodecInfo
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
		#endregion IWICComponentInfo

		new Guid GetContainerFormat();

		new uint GetPixelFormats(
			uint cFormats,
			[In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			Guid[] pguidPixelFormats
		);

		new uint GetColorManagementVersion(
			uint cchColorManagementVersion,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzColorManagementVersion
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

		new uint GetMimeTypes(
			uint cchMimeTypes,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzMimeTypes
		);

		new uint GetFileExtensions(
			uint cchFileExtensions,
			[MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 0)]
			StringBuilder wzFileExtensions
		);

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool DoesSupportAnimation();

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool DoesSupportChromakey();

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool DoesSupportLossless();

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool DoesSupportMultiframe();

		[return: MarshalAs(UnmanagedType.Bool)]
		new bool MatchesMimeType(
			[MarshalAs(UnmanagedType.LPWStr)]
			string wzMimeType
		);
		#endregion IWICBitmapCodecInfo

		uint GetPatterns(
			uint cbSizePatterns,
			IntPtr pPatterns,
			out uint pcPatterns
		);

		[return: MarshalAs(UnmanagedType.Bool)]
		bool MatchesPattern(
			IStream pIStream
		);

		IWICBitmapDecoder CreateInstance();
	}

	[ComImport, Guid("E8EDA601-3D48-431a-AB44-69059BE88BBE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICPixelFormatInfo : IWICComponentInfo
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
		#endregion IWICComponentInfo

		Guid GetFormatGUID();

		IWICColorContext GetColorContext();

		uint GetBitsPerPixel();

		uint GetChannelCount();

		uint GetChannelMask(
			uint uiChannelIndex,
			uint cbMaskBuffer,
			[In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
			byte[] pbMaskBuffer
		);
	}

	[ComImport, Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICImagingFactory
	{
		IWICBitmapDecoder CreateDecoderFromFilename(
			[MarshalAs(UnmanagedType.LPWStr)]
			string wzFilename,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor,
			GenericAccessRights dwDesiredAccess,
			WICDecodeOptions metadataOptions
		);

		IWICBitmapDecoder CreateDecoderFromStream(
			IStream pIStream,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor,
			WICDecodeOptions metadataOptions
		);

		IWICBitmapDecoder CreateDecoderFromFileHandle(
			IntPtr hFile,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor,
			WICDecodeOptions metadataOptions
		);

		IWICComponentInfo CreateComponentInfo(
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid clsidComponent
		);

		IWICBitmapDecoder CreateDecoder(
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid guidContainerFormat,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor
		);

		IWICBitmapEncoder CreateEncoder(
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid guidContainerFormat,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor
		);

		IWICPalette CreatePalette();

		IWICFormatConverter CreateFormatConverter();

		IWICBitmapScaler CreateBitmapScaler();

		IWICBitmapClipper CreateBitmapClipper();

		IWICBitmapFlipRotator CreateBitmapFlipRotator();

		IWICStream CreateStream();

		IWICColorContext CreateColorContext();

		IWICColorTransform CreateColorTransformer();

		IWICBitmap CreateBitmap(
			uint uiWidth,
			uint uiHeight,
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid pixelFormat,
			WICBitmapCreateCacheOption option
		);

		IWICBitmap CreateBitmapFromSource(
			IWICBitmapSource pIBitmapSource,
			WICBitmapCreateCacheOption option
		);

		IWICBitmap CreateBitmapFromSourceRect(
			IWICBitmapSource pIBitmapSource,
			uint x,
			uint y,
			uint width,
			uint height
		);

		IWICBitmap CreateBitmapFromMemory(
			uint uiWidth,
			uint uiHeight,
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid pixelFormat,
			uint cbStride,
			uint cbBufferSize,
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]
			byte[] pbBuffer
		);

		IWICBitmap CreateBitmapFromHBITMAP(
			IntPtr hBitmap,
			IntPtr hPalette,
			WICBitmapAlphaChannelOption options
		);

		IWICBitmap CreateBitmapFromHICON(
			IntPtr hIcon
		);

		IEnumUnknown CreateComponentEnumerator(
			WICComponentType componentTypes,
			WICComponentEnumerateOptions options
		);

		IWICFastMetadataEncoder CreateFastMetadataEncoderFromDecoder(
			IWICBitmapDecoder pIDecoder
		);

		IWICFastMetadataEncoder CreateFastMetadataEncoderFromFrameDecode(
			IWICBitmapFrameDecode pIFrameDecoder
		);

		IWICMetadataQueryWriter CreateQueryWriter(
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid guidMetadataFormat,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor
		);

		IWICMetadataQueryWriter CreateQueryWriterFromReader(
			IWICMetadataQueryReader pIQueryReader,
			[MarshalAs(UnmanagedType.LPArray, SizeConst = 1)]
			Guid[] pguidVendor
		);
	}

	internal enum WICTiffCompressionOption : uint
	{
		WICTiffCompressionDontCare = 0x00000000,
		WICTiffCompressionNone = 0x00000001,
		WICTiffCompressionCCITT3 = 0x00000002,
		WICTiffCompressionCCITT4 = 0x00000003,
		WICTiffCompressionLZW = 0x00000004,
		WICTiffCompressionRLE = 0x00000005,
		WICTiffCompressionZIP = 0x00000006,
		WICTiffCompressionLZWHDifferencing = 0x00000007,
	}

	internal enum WICJpegYCrCbSubsamplingOption : uint
	{
		WICJpegYCrCbSubsamplingDefault = 0x00000000,
		WICJpegYCrCbSubsampling420 = 0x00000001,
		WICJpegYCrCbSubsampling422 = 0x00000002,
		WICJpegYCrCbSubsampling444 = 0x00000003,
		WICJpegYCrCbSubsampling440 = 0x00000004,
	}

	[Flags]
	internal enum WICNamedWhitePoint : uint
	{
		WICWhitePointDefault = 0x00000001,
		WICWhitePointDaylight = 0x00000002,
		WICWhitePointCloudy = 0x00000004,
		WICWhitePointShade = 0x00000008,
		WICWhitePointTungsten = 0x00000010,
		WICWhitePointFluorescent = 0x00000020,
		WICWhitePointFlash = 0x00000040,
		WICWhitePointUnderwater = 0x00000080,
		WICWhitePointCustom = 0x00000100,
		WICWhitePointAutoWhiteBalance = 0x00000200,
		WICWhitePointAsShot = WICWhitePointDefault,
	}

	internal enum WICRawCapabilities : uint
	{
		WICRawCapabilityNotSupported = 0x00000000,
		WICRawCapabilityGetSupported = 0x00000001,
		WICRawCapabilityFullySupported = 0x00000002
	}

	internal enum WICRawRotationCapabilities : uint
	{
		WICRawRotationCapabilityNotSupported = 0x00000000,
		WICRawRotationCapabilityGetSupported = 0x00000001,
		WICRawRotationCapabilityNinetyDegreesSupported = 0x00000002,
		WICRawRotationCapabilityFullySupported = 0x00000003
	}

	internal struct WICRawCapabilitiesInfo
	{
		public uint cbSize;
		public uint CodecMajorVersion;
		public uint CodecMinorVersion;
		public WICRawCapabilities ExposureCompensationSupport;
		public WICRawCapabilities ContrastSupport;
		public WICRawCapabilities RGBWhitePointSupport;
		public WICRawCapabilities NamedWhitePointSupport;
		public WICNamedWhitePoint NamedWhitePointSupportMask;
		public WICRawCapabilities KelvinWhitePointSupport;
		public WICRawCapabilities GammaSupport;
		public WICRawCapabilities TintSupport;
		public WICRawCapabilities SaturationSupport;
		public WICRawCapabilities SharpnessSupport;
		public WICRawCapabilities NoiseReductionSupport;
		public WICRawCapabilities DestinationColorProfileSupport;
		public WICRawCapabilities ToneCurveSupport;
		public WICRawRotationCapabilities RotationSupport;
		public WICRawCapabilities RenderModeSupport;
	}

	internal enum WICRawParameterSet : uint
	{
		WICAsShotParameterSet = 0x00000001,
		WICUserAdjustedParameterSet = 0x00000002,
		WICAutoAdjustedParameterSet = 0x00000003,
	}

	internal enum WICRawRenderMode : uint
	{
		WICRawRenderModeDraft = 0x00000001,
		WICRawRenderModeNormal = 0x00000002,
		WICRawRenderModeBestQuality = 0x00000003
	}

	internal struct WICRawToneCurvePoint
	{
		public double Input;
		public double Output;
	}

	internal struct WICRawToneCurve
	{
		public uint cPoints;
		public WICRawToneCurvePoint[] aPoints;
	}

	[Flags]
	internal enum WICRawChangeNotification : uint
	{
		WICRawChangeNotification_ExposureCompensation = 0x00000001,
		WICRawChangeNotification_NamedWhitePoint = 0x00000002,
		WICRawChangeNotification_KelvinWhitePoint = 0x00000004,
		WICRawChangeNotification_RGBWhitePoint = 0x00000008,
		WICRawChangeNotification_Contrast = 0x00000010,
		WICRawChangeNotification_Gamma = 0x00000020,
		WICRawChangeNotification_Sharpness = 0x00000040,
		WICRawChangeNotification_Saturation = 0x00000080,
		WICRawChangeNotification_Tint = 0x00000100,
		WICRawChangeNotification_NoiseReduction = 0x00000200,
		WICRawChangeNotification_DestinationColorContext = 0x00000400,
		WICRawChangeNotification_ToneCurve = 0x00000800,
		WICRawChangeNotification_Rotation = 0x00001000,
		WICRawChangeNotification_RenderMode = 0x00002000
	}

	[ComImport, Guid("95c75a6e-3e8c-4ec2-85a8-aebcc551e59b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICDevelopRawNotificationCallback
	{
		void Notify(WICRawChangeNotification NotificationMask);
	}

	[ComImport, Guid("fbec5e44-f7be-4b65-b7f8-c0c81fef026d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICDevelopRaw : IWICBitmapFrameDecode
	{
		#region IWICBitmapFrameDecode
		#region IWICBitmapSource
		new void GetSize(
			out uint puiWidth,
			out uint puiHeight
		);

		new Guid GetPixelFormat();

		new void GetResolution(
			out double pDpiX,
			out double pDpiY
		);

		new void CopyPalette(
			IWICPalette pIPalette
		);

		new void CopyPixels(
			WICRect prc,
			uint cbStride,
			uint cbBufferSize,
			IntPtr pbBuffer
		);
		#endregion IWICBitmapSource

		new IWICMetadataQueryReader GetMetadataQueryReader();

		new uint GetColorContexts(
			uint cCount,
			[In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			IWICColorContext[] pIColorContexts
		);

		new IWICBitmapSource GetThumbnail();
		#endregion  IWICBitmapFrameDecode

		void QueryRawCapabilitiesInfo(
			ref WICRawCapabilitiesInfo pInfo
		);

		void LoadParameterSet(
			WICRawParameterSet ParameterSet
		);

		IPropertyBag2 GetCurrentParameterSet();

		void SetExposureCompensation(
			double ev
		);

		double GetExposureCompensation();

		void SetWhitePointRGB(
			uint Red, 
			uint Green, 
			uint Blue
		);

		void GetWhitePointRGB(
			out uint pRed, 
			out uint pGreen, 
			out uint pBlue
		);

		void SetNamedWhitePoint(
			WICNamedWhitePoint WhitePoint
		);

		WICNamedWhitePoint GetNamedWhitePoint();

		void SetWhitePointKelvin(
			uint WhitePointKelvin
		);

		uint GetWhitePointKelvin();

		void GetKelvinRangeInfo(
			out uint pMinKelvinTemp,
			out uint pMaxKelvinTemp, 
			out uint pKelvinTempStepValue
		);

		void SetContrast(
			double Contrast
		);

		double GetContrast();

		void SetGamma(
			double Gamma
		);

		double GetGamma();

		void SetSharpness(
			double Sharpness
		);

		double GetSharpness();

		void SetSaturation(
			double Saturation
		);

		double GetSaturation();

		void SetTint(
			double Tint
		);

		double GetTint();

		void SetNoiseReduction(
			double NoiseReduction
		);

		double GetNoiseReduction();

		void SetDestinationColorContext(
			IWICColorContext pColorContext
		);

		void SetToneCurve(
			uint cbToneCurveSize, 
			IntPtr pToneCurve
		);

		uint GetToneCurve(
			uint cbToneCurveBufferSize, 
			IntPtr pToneCurve
		);

		void SetRotation(
			double Rotation
		);

		double GetRotation();

		void SetRenderMode(
			WICRawRenderMode RenderMode
		);

		WICRawRenderMode GetRenderMode();

		void SetNotificationCallback(
			IWICDevelopRawNotificationCallback pCallback
		);
	}

	[ComImport, Guid("CACAF262-9370-4615-A13B-9F5539DA4C0A")]
	internal class WICImagingFactory { }

	[ComImport, Guid("317D06E8-5F24-433D-BDF7-79CE68D8ABC2")]
	internal class WICImagingFactory2 { }

	internal enum WIC8BIMIPTCProperties : uint
	{
		WIC8BIMIPTCPString = 0x00000001,
		WIC8BIMIPTCEmbeddedIPTC = 0x00000002
	}

	internal enum WIC8BIMResolutionInfoProperties : uint
	{
		WIC8BIMResolutionInfoPString = 0x00000001,
		WIC8BIMResolutionInfoHResolution = 0x00000002,
		WIC8BIMResolutionInfoHResolutionUnit = 0x00000003,
		WIC8BIMResolutionInfoWidthUnit = 0x00000004,
		WIC8BIMResolutionInfoVResolution = 0x00000005,
		WIC8BIMResolutionInfoVResolutionUnit = 0x00000006,
		WIC8BIMResolutionInfoHeightUnit = 0x00000007
	}

	internal enum WICPngFilterOption : uint
	{
		WICPngFilterUnspecified = 0x00000000,
		WICPngFilterNone,
		WICPngFilterSub,
		WICPngFilterUp,
		WICPngFilterAverage,
		WICPngFilterPaeth,
		WICPngFilterAdaptive
	}

	internal enum WICGifLogicalScreenDescriptorProperties : uint
	{
		WICGifLogicalScreenSignature = 0x0001,
		WICGifLogicalScreenDescriptorWidth,
		WICGifLogicalScreenDescriptorHeight,
		WICGifLogicalScreenDescriptorGlobalColorTableFlag,
		WICGifLogicalScreenDescriptorColorResolution,
		WICGifLogicalScreenDescriptorSortFlag,
		WICGifLogicalScreenDescriptorGlobalColorTableSize,
		WICGifLogicalScreenDescriptorBackgroundColorIndex,
		WICGifLogicalScreenDescriptorPixelAspectRatio,
		WICGifLogicalScreenDescriptorMax
	}

	internal enum WICGifImageDescriptorProperties : uint
	{
		WICGifImageDescriptorLeft = 0x0001,
		WICGifImageDescriptorTop,
		WICGifImageDescriptorWidth,
		WICGifImageDescriptorHeight,
		WICGifImageDescriptorLocalColorTableFlag,
		WICGifImageDescriptorInterlaceFlag,
		WICGifImageDescriptorSortFlag,
		WICGifImageDescriptorLocalColorTableSize,
		WICGifImageDescriptorMax
	}

	internal enum WICGifGraphicControlExtensionProperties : uint
	{
		WICGifGraphicControlExtensionDisposal = 0x0001,
		WICGifGraphicControlExtensionUserInputFlag,
		WICGifGraphicControlExtensionTransparencyFlag,
		WICGifGraphicControlExtensionDelay,
		WICGifGraphicControlExtensionTransparentColorIndex,
		WICGifGraphicControlExtensionMax
	}

	internal enum WICGifApplicationExtensionProperties : uint
	{
		WICGifApplicationExtensionApplication = 0x0001,
		WICGifApplciationExtensionData,
		WICGifApplciationExtensionMax
	}

	[ComImport, Guid("DAAC296F-7AA5-4dbf-8D15-225C5976F891"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICProgressiveLevelControl
	{
		uint GetLevelCount();
		uint GetCurrentLevel();
		void SetCurrentLevel(uint nLevel);
	}

	internal enum WICSectionAccessLevel : uint
	{
		WICSectionAccessLevelRead = 0x00000001,
		WICSectionAccessLevelReadWrite = 0x00000003
	}

	internal enum WICPixelFormatNumericRepresentation : int
	{
		WICPixelFormatNumericRepresentationUnspecified = 0x00000000,
		WICPixelFormatNumericRepresentationIndexed = 0x00000001,
		WICPixelFormatNumericRepresentationUnsignedInteger = 0x00000002,
		WICPixelFormatNumericRepresentationSignedInteger = 0x00000003,
		WICPixelFormatNumericRepresentationFixed = 0x00000004,
		WICPixelFormatNumericRepresentationFloat = 0x00000005
	}

	[ComImport, Guid("A9DB33A2-AF5F-43C7-B679-74F5984B5AA4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICPixelFormatInfo2 : IWICPixelFormatInfo
	{
		#region IWICPixelFormatInfo
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
		#endregion IWICComponentInfo

		new Guid GetFormatGUID();

		new IWICColorContext GetColorContext();

		new uint GetBitsPerPixel();

		new uint GetChannelCount();

		new uint GetChannelMask(
			uint uiChannelIndex,
			uint cbMaskBuffer,
			[In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
			byte[] pbMaskBuffer
		);
		#endregion IWICPixelFormatInfo

		[return: MarshalAs(UnmanagedType.Bool)]
		bool SupportsTransparency();

		WICPixelFormatNumericRepresentation GetNumericRepresentation();
	}

	internal enum WICPlanarOptions : uint
	{
		WICPlanarOptionsDefault = 0x00000000,
		WICPlanarOptionsPreserveSubsampling = 0x00000001,
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct WICBitmapPlaneDescription
	{
		public Guid Format;
		public uint Width;
		public uint Height;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct WICBitmapPlane
	{
		public Guid Format;
		public IntPtr pbBuffer;
		public uint cbStride;
		public uint cbBufferSize;
	}

	[ComImport, Guid("BEBEE9CB-83B0-4DCC-8132-B0AAA55EAC96"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICPlanarFormatConverter : IWICBitmapSource
	{
		#region IWICBitmapSource
		new void GetSize(
			out uint puiWidth,
			out uint puiHeight
		);

		new Guid GetPixelFormat();

		new void GetResolution(
			out double pDpiX,
			out double pDpiY
		);

		new void CopyPalette(
			IWICPalette pIPalette
		);

		new void CopyPixels(
			WICRect prc,
			uint cbStride,
			uint cbBufferSize,
			IntPtr pbBuffer
		);
		#endregion IWICBitmapSource

		void Initialize( 
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
			IWICBitmapSource[] ppPlanes,
			uint cPlanes,
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid dstFormat,
			WICBitmapDitherType dither,
			IWICPalette pIPalette,
			double alphaThresholdPercent,
			WICBitmapPaletteType paletteTranslate
		);

		[return: MarshalAs(UnmanagedType.Bool)]
		bool CanConvert(
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
			Guid[] pSrcPixelFormats,
			uint cSrcPlanes,
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid dstPixelFormat
		);
	}

	[ComImport, Guid("F928B7B8-2221-40C1-B72E-7E82F1974D1A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICPlanarBitmapFrameEncode
	{
		void WritePixels(
			uint lineCount,
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
			WICBitmapPlane[] pPlanes,
			uint cPlanes
		);

		void WriteSource(
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
			IWICBitmapSource[] ppPlanes,
			uint cPlanes,
			WICRect prcSource
		);
	}

	[ComImport, Guid("3AFF9CCE-BE95-4303-B927-E7D16FF4A613"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICPlanarBitmapSourceTransform
	{
		[return: MarshalAs(UnmanagedType.Bool)]
		bool DoesSupportTransform(
			ref uint puiWidth,
			ref uint puiHeight,
			WICBitmapTransformOptions dstTransform,
			WICPlanarOptions dstPlanarOptions,
			[In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 6)]
			Guid[] pguidDstFormats,
			[Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 6)]
			WICBitmapPlaneDescription[] pPlaneDescriptions,
			uint cPlanes
		);

		void CopyPixels(
			WICRect prcSource,
			uint uiWidth,
			uint uiHeight,
			WICBitmapTransformOptions dstTransform,
			WICPlanarOptions dstPlanarOptions,
			[In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 6)]
			WICBitmapPlane[] pDstPlanes,
			uint cPlanes
		);
	}

	internal enum WICJpegIndexingOptions : uint
	{
		WICJpegIndexingOptionsGenerateOnDemand = 0x00000000,
		WICJpegIndexingOptionsGenerateOnLoad= 0x00000001,
	}

	internal enum WICJpegTransferMatrix : uint
	{
		WICJpegTransferMatrixIdentity = 0x00000000,
		WICJpegTransferMatrixBT601 = 0x00000001,
	}

	internal enum WICJpegScanType : uint
	{
		WICJpegScanTypeInterleaved = 0x00000000,
		WICJpegScanTypePlanarComponents = 0x00000001,
		WICJpegScanTypeProgressive = 0x00000002,
	}

	internal enum WICJpegSampleFactors : uint
	{
		WICJpegSampleFactorsOne = 0x00000011,
		WICJpegSampleFactorsThree420 = 0x00111122,
		WICJpegSampleFactorsThree422 = 0x00111121,
		WICJpegSampleFactorsThree440 = 0x00111112,
		WICJpegSampleFactorsThree444 = 0x00111111,
	}

	internal enum WICJpegQuantizationTableIndices : uint
	{
		WICJpegQuantizationBaselineOne = 0x00000000,
		WICJpegQuantizationBaselineThree = 0x00010100,
	}

	internal enum WICJpegHuffmanTableIndices : uint
	{
		WICJpegHuffmanBaselineOne = 0x00000000,
		WICJpegHuffmanBaselineThree = 0x00111100,
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct WICBitmapPlanes
	{
		public Guid Format;
		public IntPtr pbBuffer;
		public uint cbStride;
		public uint cbBufferSize;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct WICJpegFrameHeader
	{
		public uint Width;
		public uint Height;
		public WICJpegTransferMatrix TransferMatrix;
		public WICJpegScanType ScanType;
		public uint cComponents;
		public uint ComponentIdentifiers;
		public WICJpegSampleFactors SampleFactors;
		public WICJpegQuantizationTableIndices QuantizationTableIndices;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct WICJpegScanHeader
	{
		public uint cComponents;
		public uint RestartInterval;
		public uint ComponentSelectors;
		public WICJpegHuffmanTableIndices HuffmanTableIndices;
		public byte StartSpectralSelection;
		public byte EndSpectralSelection;
		public byte SuccessiveApproximationHigh;
		public byte SuccessiveApproximationLow;
	}

	[StructLayout(LayoutKind.Sequential)]
	unsafe internal struct WICJpegAcHuffmanTable
	{
		public fixed byte CodeCounts[16];
		public fixed byte CodeValues[162];
	}

	[StructLayout(LayoutKind.Sequential)]
	unsafe internal struct WICJpegDcHuffmanTable
	{
		public fixed byte CodeCounts[12];
		public fixed byte CodeValues[12];
	}

	[StructLayout(LayoutKind.Sequential)]
	unsafe internal struct WICJpegQuantizationTable
	{
		public fixed byte Elements[64];
	}

	[ComImport, Guid("8939F66E-C46A-4c21-A9D1-98B327CE1679"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICJpegFrameDecode
	{
		[return: MarshalAs(UnmanagedType.Bool)]
		bool DoesSupportIndexing();

		void SetIndexing( 
			WICJpegIndexingOptions options,
			uint horizontalIntervalSize
		);

		void ClearIndexing();

		WICJpegAcHuffmanTable GetAcHuffmanTable(
			uint scanIndex,
			uint tableIndex
		);

		WICJpegDcHuffmanTable GetDcHuffmanTable(
			uint scanIndex,
			uint tableIndex
		);

		WICJpegQuantizationTable GetQuantizationTable(
			uint scanIndex,
			uint tableIndex
		);

		WICJpegFrameHeader GetFrameHeader();

		WICJpegScanHeader GetScanHeader(
			uint scanIndex
		);

		uint CopyScan(
			uint scanIndex,
			uint scanOffset,
			uint cbScanData,
			[Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
			byte[] pbScanData
		);

		uint CopyMinimalStream(
			uint streamOffset,
			uint cbStreamData,
			[Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
			byte[] pbStreamData
		);
	}

	[ComImport, Guid("2F0C601F-D2C6-468C-ABFA-49495D983ED1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IWICJpegFrameEncode
	{
		WICJpegAcHuffmanTable GetAcHuffmanTable(
			uint scanIndex,
			uint tableIndex
		);

		WICJpegDcHuffmanTable GetDcHuffmanTable(
			uint scanIndex,
			uint tableIndex
		);

		WICJpegQuantizationTable GetQuantizationTable( 
			uint scanIndex,
			uint tableIndex
		);

		void WriteScan( 
			uint cbScanData,
			[In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			byte[] pbScanData
		);
	}

	internal static class ProxyFunctions
	{
		[DllImport("WindowsCodecs", EntryPoint = "IWICBitmapDecoder_GetPreview_Proxy")]
		public extern static int GetPreview(
			IWICBitmapDecoder THIS_PTR,
			out IWICBitmapSource ppIBitmapSource
		);

		[DllImport("WindowsCodecs", EntryPoint = "IWICBitmapFrameDecode_GetColorContexts_Proxy")]
		public extern static int GetColorContexts(
			IWICBitmapFrameDecode THIS_PTR,
			uint cCount,
			IntPtr[] ppIColorContexts,
			out uint pcActualCount
		);

		[DllImport("WindowsCodecs", EntryPoint = "IWICBitmapFrameDecode_GetMetadataQueryReader_Proxy")]
		public extern static int GetMetadataQueryReader(
			IWICBitmapFrameDecode THIS_PTR,
			out IWICMetadataQueryReader ppIMetadataQueryReader
		);

		[DllImport("WindowsCodecs", EntryPoint = "IWICBitmapFrameEncode_GetMetadataQueryWriter_Proxy")]
		public extern static int GetMetadataQueryWriter(
			IWICBitmapFrameEncode THIS_PTR,
			out IWICMetadataQueryWriter ppIMetadataQueryWriter
		);

		[DllImport("WindowsCodecs", EntryPoint = "IWICBitmapFrameEncode_SetColorContexts_Proxy")]
		public extern static int SetColorContexts(
			IWICBitmapFrameEncode THIS_PTR,
			uint cCount,
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
			IWICColorContext[] ppIColorContext
		);

		[DllImport("WindowsCodecs", EntryPoint = "IWICMetadataQueryReader_GetMetadataByName_Proxy")]
		public extern static int GetMetadataByName(
			IWICMetadataQueryReader THIS_PTR,
			[MarshalAs(UnmanagedType.LPWStr)]
			string wzName,
			[In, Out, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarValue
		);

		[DllImport("WindowsCodecs", EntryPoint = "IWICMetadataQueryWriter_SetMetadataByName_Proxy")]
		public extern static int SetMetadataByName(
			IWICMetadataQueryWriter THIS_PTR,
			[MarshalAs(UnmanagedType.LPWStr)]
			string wzName,
			[In, MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(PropVariant.Marshaler))]
			PropVariant pvarValue
		);

		[DllImport("WindowsCodecsExt", EntryPoint = "IWICColorTransform_Initialize_Proxy")]
		public extern static int InitializeColorTransform(
			IWICColorTransform THIS_PTR,
			IWICBitmapSource pIBitmapSource,
			IWICColorContext pIContextSource,
			IWICColorContext pIContextDest,
			[MarshalAs(UnmanagedType.LPStruct)]
			Guid pixelFmtDest
		);
	}
}

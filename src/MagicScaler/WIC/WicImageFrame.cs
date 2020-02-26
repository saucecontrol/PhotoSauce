using System;
using System.Buffers;
using System.Runtime.InteropServices;

using PhotoSauce.Interop.Wic;

namespace PhotoSauce.MagicScaler
{
	internal class WicImageFrame : IImageFrame
	{
		private readonly WicImageContainer container;
		private readonly ComHandleCollection comHandles = new ComHandleCollection(4);

		private PixelSource? source;
		private IPixelSource? isource;
		private WicColorProfile? colorProfile;

		public double DpiX { get; }
		public double DpiY { get; }
		public Orientation ExifOrientation { get; } = Orientation.Normal;
		public ReadOnlySpan<byte> IccProfile => ColorProfileSource.ParsedProfile.ProfileBytes;

		public bool SupportsNativeScale { get; }
		//public bool SupportsNativeTransform { get; }
		public bool SupportsPlanarProcessing { get; }

		public WICJpegYCrCbSubsamplingOption ChromaSubsampling { get; }

		public IWICBitmapFrameDecode WicFrame { get; }
		public IWICBitmapSource WicSource { get; }
		public IWICMetadataQueryReader? WicMetadataReader { get; }

		public PixelSource Source => source ??= WicSource.AsPixelSource(nameof(IWICBitmapFrameDecode), false);

		public IPixelSource PixelSource => isource ??= Source.AsIPixelSource();

		public WicColorProfile ColorProfileSource => colorProfile ??= getColorProfile();

		public WicImageFrame(WicImageContainer decoder, uint index)
		{
			if (index >= (uint)decoder.FrameCount) throw new IndexOutOfRangeException("Frame index does not exist");

			WicFrame = comHandles.AddRef(decoder.WicDecoder.GetFrame(index));
			WicSource = WicFrame;
			WicFrame.GetSize(out uint frameWidth, out uint frameHeight);
			container = decoder;

			if (decoder.IsRawContainer && index == 0 && decoder.WicDecoder.TryGetPreview(out var preview))
			{
				using var pvwSource = new ComHandle<IWICBitmapSource>(preview);
				preview.GetSize(out uint pw, out uint ph);

				if (pw == frameWidth && ph == frameHeight)
					WicSource = comHandles.AddOwnRef(preview);
			}

			WicFrame.GetResolution(out double dpix, out double dpiy);
			DpiX = dpix;
			DpiY = dpiy;

			if (PixelFormat.FromGuid(WicSource.GetPixelFormat()).NumericRepresentation == PixelNumericRepresentation.Indexed)
			{
				using var wicpal = new ComHandle<IWICPalette>(Wic.Factory.CreatePalette());
				var pal = wicpal.ComObject;
				WicSource.CopyPalette(pal);

				var newFormat = Consts.GUID_WICPixelFormat24bppBGR;
				if (pal.HasAlpha())
					newFormat = Consts.GUID_WICPixelFormat32bppBGRA;
				else if (pal.IsGrayscale() || pal.IsBlackWhite())
					newFormat = Consts.GUID_WICPixelFormat8bppGray;

				var conv = comHandles.AddRef(Wic.Factory.CreateFormatConverter());
				conv.Initialize(WicSource, newFormat, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
				WicSource = conv;
			}

			if (WicSource is IWICBitmapSourceTransform trans)
			{
				uint pw = 1, ph = 1;
				trans.GetClosestSize(ref pw, ref ph);

				SupportsNativeScale = pw < frameWidth || ph < frameHeight;
				//SupportsNativeTransform = trans.DoesSupportTransform(WICBitmapTransformOptions.WICBitmapTransformRotate270);
			}

			if (WicSource is IWICPlanarBitmapSourceTransform ptrans)
			{
				var desc = ArrayPool<WICBitmapPlaneDescription>.Shared.Rent(WicTransforms.PlanarPixelFormats.Length);

				SupportsPlanarProcessing = ptrans.DoesSupportTransform(ref frameWidth, ref frameHeight, WICBitmapTransformOptions.WICBitmapTransformRotate0, WICPlanarOptions.WICPlanarOptionsDefault, WicTransforms.PlanarPixelFormats, desc, (uint)WicTransforms.PlanarPixelFormats.Length);
				ChromaSubsampling =
					desc[1].Width < desc[0].Width && desc[1].Height < desc[0].Height ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling420 :
					desc[1].Width < desc[0].Width ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling422 :
					desc[1].Height < desc[0].Height ? WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling440 :
					WICJpegYCrCbSubsamplingOption.WICJpegYCrCbSubsampling444;

				ArrayPool<WICBitmapPlaneDescription>.Shared.Return(desc);
			}

			if (WicFrame.TryGetMetadataQueryReader(out var metareader))
			{
				WicMetadataReader = comHandles.AddRef(metareader);

				string orientationPath =
					MagicImageProcessor.EnableXmpOrientation ? Wic.Metadata.OrientationWindowsPolicy :
					decoder.ContainerFormat == FileFormat.Jpeg ? Wic.Metadata.OrientationJpegPath :
					Wic.Metadata.OrientationExifPath;

				if (metareader.TryGetMetadataByName(orientationPath, out var pvorient) && pvorient.UnmanagedType == VarEnum.VT_UI2)
					ExifOrientation = (Orientation)Math.Min(Math.Max((ushort)Orientation.Normal, (ushort)pvorient.Value!), (ushort)Orientation.Rotate270);
			}
		}

		public void Dispose() => comHandles.Dispose();

		private WicColorProfile getColorProfile()
		{
			var fmt = PixelFormat.FromGuid(WicSource.GetPixelFormat());
			uint ccc = WicFrame.GetColorContextCount();
			if (ccc == 0)
				return WicColorProfile.GetDefaultFor(fmt);

			var profiles = ArrayPool<IWICColorContext>.Shared.Rent((int)ccc);

			for (int i = 0; i < (int)ccc; i++)
				profiles[i] = comHandles.AddRef(Wic.Factory.CreateColorContext());

			WicFrame.GetColorContexts(ccc, profiles);
			var match = matchProfile(profiles.AsSpan(0, (int)ccc), fmt);

			ArrayPool<IWICColorContext>.Shared.Return(profiles);

			return match ?? WicColorProfile.GetDefaultFor(fmt);
		}

		private WicColorProfile? matchProfile(ReadOnlySpan<IWICColorContext> profiles, PixelFormat fmt)
		{
			foreach (var cc in profiles)
			{
				var cct = cc.GetType();
				if (cct == WICColorContextType.WICColorContextProfile)
				{
					uint cb = cc.GetProfileBytes(0, null);

					// Don't try to read giant profiles. 4MiB should be enough, and more might indicate corrupt metadata.
					if (cb > 1024 * 1024 * 4)
						continue;

					var buff = ArrayPool<byte>.Shared.Rent((int)cb);

					cc.GetProfileBytes(cb, buff);
					var cpi = ColorProfile.Cache.GetOrAdd(new ReadOnlySpan<byte>(buff, 0, (int)cb));

					ArrayPool<byte>.Shared.Return(buff);

					// Use the profile only if it matches the frame's pixel format.  Ignore embedded sRGB-compatible profiles -- they will be upgraded to the internal sRGB/sGrey definintion.
					if (cpi.IsValid && cpi.IsCompatibleWith(fmt) && !cpi.IsSrgb)
						return new WicColorProfile(cc, cpi);
				}
				else if (cct == WICColorContextType.WICColorContextExifColorSpace && WicMetadataReader != null)
				{
					// Although WIC defines the non-standard AdobeRGB ExifColorSpace value, most software (including Adobe's) only supports the Uncalibrated/InteropIndex=R03 convention.
					// http://ninedegreesbelow.com/photography/embedded-color-space-information.html
					var ecs = cc.GetExifColorSpace();
					if (
						ecs == ExifColorSpace.AdobeRGB || (
						ecs == ExifColorSpace.Uncalibrated
						&& WicMetadataReader.TryGetMetadataByName(container.ContainerFormat == FileFormat.Jpeg ? Wic.Metadata.InteropIndexJpegPath : Wic.Metadata.InteropIndexExifPath, out var interopIdx)
						&& interopIdx.UnmanagedType == VarEnum.VT_LPSTR
						&& (string)interopIdx.Value! == "R03")
					) return WicColorProfile.AdobeRgb.Value;
				}
			}

			return null;
		}
	}
}
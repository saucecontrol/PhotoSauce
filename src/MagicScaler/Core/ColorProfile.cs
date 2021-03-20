// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Buffers;
using System.Numerics;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Blake2Fast;

using static PhotoSauce.MagicScaler.MathUtil;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PhotoSauce.MagicScaler
{
	internal enum ColorProfileType { Unknown, Curve, Matrix, Table }

	//http://www.color.org/specification/ICC1v43_2010-12.pdf
	internal class ColorProfile
	{
		internal static class Cache
		{
			private static readonly ConcurrentDictionary<Guid, WeakReference<ColorProfile>> dic = new();

			private static ColorProfile addOrUpdate(Guid guid, ReadOnlySpan<byte> bytes)
			{
				var prof = Parse(bytes);
				dic.AddOrUpdate(guid, (g) => new WeakReference<ColorProfile>(prof), (g, r) => { r.SetTarget(prof); return r; });

				return prof;
			}

			public static ColorProfile GetOrAdd(ReadOnlySpan<byte> bytes)
			{
				var hash = (Span<byte>)stackalloc byte[Unsafe.SizeOf<Guid>()];
				Blake2b.ComputeAndWriteHash(Unsafe.SizeOf<Guid>(), bytes, hash);

				var guid = MemoryMarshal.Read<Guid>(hash);

				return (dic.TryGetValue(guid, out var wref) && wref.TryGetTarget(out var prof)) ? prof : addOrUpdate(guid, bytes);
			}
		}

		private readonly struct TagEntry
		{
			public readonly uint tag;
			public readonly int pos;
			public readonly int cb;

			public TagEntry(uint t, int p, int c)
			{
				tag = t;
				pos = p;
				cb = c;
			}
		}

		private static class IccStrings
		{
			public const uint acsp = 'a' << 24 | 'c' << 16 | 's' << 8 | 'p';
			public const uint RGB  = 'R' << 24 | 'G' << 16 | 'B' << 8 | ' ';
			public const uint GRAY = 'G' << 24 | 'R' << 16 | 'A' << 8 | 'Y';
			public const uint CMYK = 'C' << 24 | 'M' << 16 | 'Y' << 8 | 'K';
			public const uint XYZ  = 'X' << 24 | 'Y' << 16 | 'Z' << 8 | ' ';
			public const uint Lab  = 'L' << 24 | 'a' << 16 | 'b' << 8 | ' ';
		}

		private static class IccTags
		{
			public const uint bXYZ = 'b' << 24 | 'X' << 16 | 'Y' << 8 | 'Z';
			public const uint gXYZ = 'g' << 24 | 'X' << 16 | 'Y' << 8 | 'Z';
			public const uint rXYZ = 'r' << 24 | 'X' << 16 | 'Y' << 8 | 'Z';
			public const uint bTRC = 'b' << 24 | 'T' << 16 | 'R' << 8 | 'C';
			public const uint gTRC = 'g' << 24 | 'T' << 16 | 'R' << 8 | 'C';
			public const uint rTRC = 'r' << 24 | 'T' << 16 | 'R' << 8 | 'C';
			public const uint kTRC = 'k' << 24 | 'T' << 16 | 'R' << 8 | 'C';
			public const uint A2B0 = 'A' << 24 | '2' << 16 | 'B' << 8 | '0';
			public const uint B2A0 = 'B' << 24 | '2' << 16 | 'A' << 8 | '0';
		}

		private static class IccTypes
		{
			public const uint XYZ  = 'X' << 24 | 'Y' << 16 | 'Z' << 8 | ' ';
			public const uint curv = 'c' << 24 | 'u' << 16 | 'r' << 8 | 'v';
			public const uint para = 'p' << 24 | 'a' << 16 | 'r' << 8 | 'a';
		}

		internal enum ProfileColorSpace
		{
			Other,
			Rgb,
			Grey,
			Cmyk,
			Xyz,
			Lab
		}

		private static readonly ColorProfile invalidProfile = new();

		private static readonly Lazy<MatrixProfile> srgb = new(() => {
			var m = new Matrix4x4(
				0.43602939f, 0.22243797f, 0.01389754f, 0,
				0.38510027f, 0.71694100f, 0.09707674f, 0,
				0.14307328f, 0.06062103f, 0.71393112f, 0,
				0,           0,           0,           1
			);
			var im = m.InvertPrecise();
			var curve = new ProfileCurve(LookupTables.SrgbGamma, LookupTables.SrgbInverseGamma);

			return new MatrixProfile(IccProfiles.sRgbV4.Value, m, im, curve, ProfileColorSpace.Rgb, ProfileColorSpace.Xyz);
		});

		private static readonly Lazy<CurveProfile> sgrey = new(() =>
			new CurveProfile(IccProfiles.sGreyV4.Value, sRGB.Curve, ProfileColorSpace.Grey, ProfileColorSpace.Xyz)
		);

		private static readonly Lazy<MatrixProfile> adobeRgb = new(() => {
			var m = new Matrix4x4(
				0.60974189f, 0.31111293f, 0.01946551f, 0,
				0.20527343f, 0.62567449f, 0.06087462f, 0,
				0.14918756f, 0.06321258f, 0.74456527f, 0,
				0,           0,           0,           1
			);
			var im = m.InvertPrecise();
			var curve = curveFromPower(2.2);

			return new MatrixProfile(IccProfiles.AdobeRgb.Value, m, im, curve, ProfileColorSpace.Rgb, ProfileColorSpace.Xyz);
		});

		private static readonly Lazy<MatrixProfile> displayP3 = new(() => {
			var m = new Matrix4x4(
				0.51511960f, 0.24118953f, -0.00105045f, 0,
				0.29197886f, 0.69224341f,  0.04187909f, 0,
				0.15710442f, 0.06656706f,  0.78407676f, 0,
				0,           0,            0,           1
			);
			var im = m.InvertPrecise();

			return new MatrixProfile(IccProfiles.DisplayP3V4.Value, m, im, sRGB.Curve, ProfileColorSpace.Rgb, ProfileColorSpace.Xyz);
		});

		private static ProfileCurve curveFromPower(double gamma)
		{
			var igt = new float[LookupTables.InverseGammaLength];
			for (int i = 0; i < igt.Length; i++)
				igt[i] = (float)Math.Pow((double)i / LookupTables.InverseGammaScale, gamma);

			gamma = 1d / gamma;

			var gt = new float[LookupTables.GammaLengthFloat];
			for (int i = 0; i < gt.Length; i++)
				gt[i] = (float)Math.Pow((double)i / LookupTables.GammaScaleFloat, gamma);

			LookupTables.Fixup(gt, LookupTables.GammaScaleFloat);
			LookupTables.Fixup(igt, LookupTables.InverseGammaScale);

			return new ProfileCurve(gt, igt);
		}

		private static ProfileCurve curveFromPoints(ReadOnlySpan<ushort> points, bool inverse)
		{
			int buffLen = points.Length * sizeof(double);
			using var buff = MemoryPool<byte>.Shared.Rent(buffLen);

			var curve = MemoryMarshal.Cast<byte, double>(buff.Memory.Span.Slice(0, buffLen));
			double cscal = curve.Length - 1;

			for (int i = 0; i < curve.Length; i++)
				curve[i] = (double)points[i] / ushort.MaxValue;

			var igt = new float[LookupTables.InverseGammaLength];
			for (int i = 0; i <= LookupTables.InverseGammaScale; i++)
			{
				double val = (double)i / LookupTables.InverseGammaScale;
				double pos = val * cscal;

				int idx = Math.Min((int)pos, curve.Length - 2);
				igt[i] = (float)Lerp(curve[idx], curve[idx + 1], pos - idx);
			}

			if (lutInvertsTo(igt, LookupTables.SrgbGamma))
				return sRGB.Curve;

			if (inverse)
				curve.Reverse();

			var gt = new float[LookupTables.GammaLengthFloat];
			for (int i = 0; i <= LookupTables.GammaScaleFloat; i++)
			{
				double val = (double)i / LookupTables.GammaScaleFloat;
				int idx = curve.BinarySearch(val);

				double pos;
				if (idx >= 0)
					pos = idx;
				else
				{
					idx = ~idx;
					if (idx == 0)
						pos = 0;
					else if (idx == curve.Length)
						pos = curve.Length - 1;
					else
					{
						double vh = curve[idx];
						double vl = curve[idx - 1];
						if (vl == vh)
							pos = idx;
						else
							pos = idx - 1 + (val - vl) / (vh - vl);
					}
				}

				gt[i] = (float)(pos / (curve.Length - 1));
			}

			if (inverse)
				Array.Reverse(gt, 0, LookupTables.GammaScaleFloat + 1);

			LookupTables.Fixup(gt, LookupTables.GammaScaleFloat);
			LookupTables.Fixup(igt, LookupTables.InverseGammaScale);

			return new ProfileCurve(gt, igt);
		}

		private static ProfileCurve curveFromParameters(double a, double b, double c, double d, double e, double f, double g)
		{
			if (
				g.IsRoughlyEqualTo(2.4) &&
				d.IsRoughlyEqualTo(0.04045) &&
				a.IsRoughlyEqualTo(1.000/1.055) &&
				b.IsRoughlyEqualTo(0.055/1.055) &&
				c.IsRoughlyEqualTo(1.000/12.92)
			) return sRGB.Curve;

			var igt = new float[LookupTables.InverseGammaLength];
			for (int i = 0; i < igt.Length; i++)
			{
				double val = (double)i / LookupTables.InverseGammaScale;
				if (val >= d)
					val = Math.Pow(val * a + b, g) + c + e;
				else
					val = val * c + f;

				igt[i] = (float)val;
			}

			g = 1d / g;

			var gt = new float[LookupTables.GammaLengthFloat];
			for (int i = 0; i < gt.Length; i++)
			{
				double val = (double)i / LookupTables.GammaScaleFloat;
				if (val > (c * d + f))
					val = (Math.Pow(val - c - e, g) - b) / a;
				else
					val = c == 0f ? 0f : ((val - f) / c);

				gt[i] = (float)val;
			}

			LookupTables.Fixup(gt, LookupTables.GammaScaleFloat);
			LookupTables.Fixup(igt, LookupTables.InverseGammaScale);

			return new ProfileCurve(gt, igt);
		}

		private static bool lutInvertsTo(float[] igt, float[] gt)
		{
			for (int i = 0; i <= LookupTables.InverseGammaScale; i++)
			{
				float pos = igt[i] * LookupTables.GammaScaleFloat;
				int idx = (int)pos;

				if (i != FixToByte(Lerp(gt[idx], gt[idx + 1], pos - idx)))
					return false;
			}

			return true;
		}

		private static bool tryGetTagEntry(TagEntry[] entries, uint tag, out TagEntry entry)
		{
			for (int i = 0; i < entries.Length; i++)
			if (entries[i].tag == tag)
			{
				entry = entries[i];
				return true;
			}

			entry = default;
			return false;
		}

		private static bool tryGetMatrix(ReadOnlySpan<byte> bXYZ, ReadOnlySpan<byte> gXYZ, ReadOnlySpan<byte> rXYZ, out Matrix4x4 matrix)
		{
			matrix = default;

			var hdr = bXYZ.Slice(0, 8);
			uint tag = ReadUInt32BigEndian(hdr);
			if (tag != IccTypes.XYZ || !hdr.SequenceEqual(gXYZ.Slice(0, 8)) || !hdr.SequenceEqual(rXYZ.Slice(0, 8)))
				return false;

			int bx = ReadInt32BigEndian(bXYZ.Slice(8));
			int gx = ReadInt32BigEndian(gXYZ.Slice(8));
			int rx = ReadInt32BigEndian(rXYZ.Slice(8));

			int by = ReadInt32BigEndian(bXYZ.Slice(12));
			int gy = ReadInt32BigEndian(gXYZ.Slice(12));
			int ry = ReadInt32BigEndian(rXYZ.Slice(12));

			int bz = ReadInt32BigEndian(bXYZ.Slice(16));
			int gz = ReadInt32BigEndian(gXYZ.Slice(16));
			int rz = ReadInt32BigEndian(rXYZ.Slice(16));

			float div = 1 / 65536f;
			matrix = new Matrix4x4(
				rx * div, ry * div, rz * div, 0,
				gx * div, gy * div, gz * div, 0,
				bx * div, by * div, bz * div, 0,
				0,        0,        0,        1
			);

			return true;
		}

		private static bool tryGetCurve(ReadOnlySpan<byte> trc, out ProfileCurve? curve)
		{
			// true return with null curve indicates linear curve
			curve = default;

			uint tag = ReadUInt32BigEndian(trc);
			if (trc.Length < 12 || (tag != IccTypes.curv && tag != IccTypes.para))
				return false;

			if (tag == IccTypes.curv)
			{
				uint pcnt = ReadUInt32BigEndian(trc.Slice(8));
				if (trc.Length < (12 + pcnt * sizeof(ushort)))
					return false;

				if (pcnt == 0)
					return true;

				if (pcnt == 1)
				{
					ushort gi = ReadUInt16BigEndian(trc.Slice(12));
					double gd;
					switch (gi)
					{
						case 0:
							return false;
						case 256:
							return true;
						case 461:
							gd = 1.8;
							break;
						case 563:
							curve = AdobeRgb.Curve;
							return true;
						default:
							gd = gi / 256d;
							break;
					}

					curve = curveFromPower(gd);
				}
				else
				{
					if (pcnt == 2 && ReadUInt16BigEndian(trc.Slice(12)) == ushort.MinValue && ReadUInt16BigEndian(trc.Slice(14)) == ushort.MaxValue)
						return true;

					int buffLen = (int)pcnt * sizeof(ushort);
					using var buff = MemoryPool<byte>.Shared.Rent(buffLen);
					var points = MemoryMarshal.Cast<byte, ushort>(buff.Memory.Span.Slice(0, buffLen));

					ushort pp = 0;
					bool inc = true, dec = true;
					for (int i = 0; i < points.Length; i++)
					{
						ushort p = ReadUInt16BigEndian(trc.Slice(12 + i * sizeof(ushort)));

						if (i > 0 && p < pp)
							inc = false;
						if (i > 0 && p > pp)
							dec = false;
						if (!inc && !dec)
							return false;

						points[i] = pp = p;
					}

					curve = curveFromPoints(points, dec);
				}
			}
			else // (tag == IccTypes.para)
			{
				ushort func = ReadUInt16BigEndian(trc.Slice(8));
				if (trc.Length < 16 || func > 4)
					return false;

				var param = trc.Slice(12);
				int g = ReadInt32BigEndian(param);

				if (func == 0 && g == 0x10000)
					return true;

				if (
					(g == 0) ||
					(func == 1 && param.Length < 12) ||
					(func == 2 && param.Length < 16) ||
					(func == 3 && param.Length < 20) ||
					(func == 4 && param.Length < 28)
				) return false;

				int a = 0, b = 0, c = 0, d = 0, e = 0, f = 0;
				switch (func)
				{
					case 1:
						a = ReadInt32BigEndian(param.Slice(4));
						b = ReadInt32BigEndian(param.Slice(8));
						break;
					case 2:
						c = ReadInt32BigEndian(param.Slice(12));
						goto case 1;
					case 3:
						d = ReadInt32BigEndian(param.Slice(16));
						goto case 2;
					case 4:
						e = ReadInt32BigEndian(param.Slice(20));
						f = ReadInt32BigEndian(param.Slice(24));
						goto case 3;
				}

				// prevent divide by 0 and some uninvertible curves.
				if (
					(a == 0) ||
					((uint)c > 0x10000u && func >= 2) ||
					((uint)d > 0x10000u && func >= 3) ||
					((uint)e > 0x10000u && func == 4)
				) return false;

				double div = 1 / 65536d;
				double da = a * div, db = b * div, dc = c * div, dd = d * div, de = e * div, df = f * div, dg = g * div;
				switch (func)
				{
					case 1:
						dd = -db / da;
						break;
					case 2:
						df = de = dc;
						dc = 0d;
						goto case 1;
					case 3:
					case 4:
						de -= dc;
						break;
				}

				curve = func == 0 ? curveFromPower(dg) : curveFromParameters(da, db, dc, dd, de, df, dg);
			}

			return true;
		}

		public static ColorProfile Parse(ReadOnlySpan<byte> prof)
		{
			const int headerLength = 128;
			const int headerPlusTagCountLength = 132;

			if (prof.Length < headerPlusTagCountLength)
				return invalidProfile;

			uint len = ReadUInt32BigEndian(prof.Slice(0, sizeof(uint)));
			if (len != prof.Length)
				return invalidProfile;

			uint acsp = ReadUInt32BigEndian(prof.Slice(36));
			var ver = prof.Slice(8, 4);
			if ((ver[0] != 2 && ver[0] != 4) || acsp != IccStrings.acsp)
				return invalidProfile;

			var dataColorSpace = ReadUInt32BigEndian(prof.Slice(16)) switch {
				IccStrings.RGB  => ProfileColorSpace.Rgb,
				IccStrings.GRAY => ProfileColorSpace.Grey,
				IccStrings.CMYK => ProfileColorSpace.Cmyk,
				_               => ProfileColorSpace.Other
			};

			var pcsColorSpace = ReadUInt32BigEndian(prof.Slice(20)) switch {
				IccStrings.XYZ => ProfileColorSpace.Xyz,
				IccStrings.Lab => ProfileColorSpace.Lab,
				_              => ProfileColorSpace.Other
			};

			if (pcsColorSpace != ProfileColorSpace.Xyz || (dataColorSpace != ProfileColorSpace.Rgb && dataColorSpace != ProfileColorSpace.Grey))
				return new ColorProfile(prof.ToArray(), dataColorSpace, pcsColorSpace, ColorProfileType.Unknown);

			uint tagCount = ReadUInt32BigEndian(prof.Slice(headerLength));
			if (len < (headerPlusTagCountLength + tagCount * Unsafe.SizeOf<TagEntry>()))
				return invalidProfile;

			var tagEntries = new TagEntry[((int)tagCount)];
			for (int i = 0; i < tagCount; i++)
			{
				int entryStart = headerPlusTagCountLength + i * Unsafe.SizeOf<TagEntry>();

				uint tag = ReadUInt32BigEndian(prof.Slice(entryStart));
				uint pos = ReadUInt32BigEndian(prof.Slice(entryStart + 4));
				uint cb = ReadUInt32BigEndian(prof.Slice(entryStart + 8));

				if (len < (pos + cb))
					return invalidProfile;

				// not handling these yet, so we'll hand off to WCS
				if (tag == IccTags.A2B0 || tag == IccTags.B2A0)
					return new ColorProfile(prof.ToArray(), dataColorSpace, pcsColorSpace, ColorProfileType.Table);

				tagEntries[i] = new TagEntry(tag, (int)pos, (int)cb);
			}

			if (dataColorSpace == ProfileColorSpace.Grey
				&& tryGetTagEntry(tagEntries, IccTags.kTRC, out var kTRC)
				&& tryGetCurve(prof.Slice(kTRC.pos, kTRC.cb), out var curve)
			) return new CurveProfile(prof.ToArray(), curve, dataColorSpace, pcsColorSpace);

			if (dataColorSpace == ProfileColorSpace.Rgb
				&& tryGetTagEntry(tagEntries, IccTags.bTRC, out var bTRC)
				&& tryGetTagEntry(tagEntries, IccTags.gTRC, out var gTRC)
				&& tryGetTagEntry(tagEntries, IccTags.rTRC, out var rTRC)
				&& tryGetTagEntry(tagEntries, IccTags.bXYZ, out var bXYZ)
				&& tryGetTagEntry(tagEntries, IccTags.gXYZ, out var gXYZ)
				&& tryGetTagEntry(tagEntries, IccTags.rXYZ, out var rXYZ)
			)
			{
				var bTRCData = prof.Slice(bTRC.pos, bTRC.cb);
				var gTRCData = prof.Slice(gTRC.pos, gTRC.cb);
				var rTRCData = prof.Slice(rTRC.pos, rTRC.cb);

				if (bTRCData.SequenceEqual(gTRCData) && bTRCData.SequenceEqual(rTRCData)
					&& tryGetCurve(bTRCData, out var rgbcurve)
					&& tryGetMatrix(prof.Slice(bXYZ.pos, bXYZ.cb), prof.Slice(gXYZ.pos, gXYZ.cb), prof.Slice(rXYZ.pos, rXYZ.cb), out var matrix)
				)
				{
					var imatrix = matrix.InvertPrecise();
					if (!imatrix.IsNaN())
						return new MatrixProfile(prof.ToArray(), matrix, imatrix, rgbcurve, dataColorSpace, pcsColorSpace);
				}
			}

			return invalidProfile;
		}

		public static CurveProfile sGrey => sgrey.Value;
		public static MatrixProfile sRGB => srgb.Value;
		public static MatrixProfile AdobeRgb => adobeRgb.Value;
		public static MatrixProfile DisplayP3 => displayP3.Value;

		public static ColorProfile GetDefaultFor(PixelFormat fmt) => fmt.ColorRepresentation == PixelColorRepresentation.Grey ? sGrey : sRGB;

		public static ColorProfile GetSourceProfile(ColorProfile prof, ColorProfileMode mode)
		{
			if (mode == ColorProfileMode.Preserve)
				return prof;

			if (prof.ProfileType == ColorProfileType.Curve && prof is CurveProfile cp && cp.Curve == sGrey.Curve)
				return sGrey;

			if (prof is MatrixProfile mp)
			{
				if (mp.Matrix.IsRouglyEqualTo(sRGB.Matrix) && mp.Curve == sRGB.Curve)
					return sRGB;

				if (mp.Matrix.IsRouglyEqualTo(DisplayP3.Matrix) && mp.Curve == DisplayP3.Curve)
					return DisplayP3;

				if (mp.Matrix.IsRouglyEqualTo(AdobeRgb.Matrix) && mp.Curve == AdobeRgb.Curve)
					return AdobeRgb;
			}

			return prof;
		}

		public static ColorProfile GetDestProfile(ColorProfile prof, ColorProfileMode mode)
		{
			if (mode == ColorProfileMode.Preserve)
				return prof;

			if (mode <= ColorProfileMode.NormalizeAndEmbed)
			{
				if (prof == AdobeRgb || prof.DataColorSpace == ProfileColorSpace.Cmyk)
					return AdobeRgb;

				if (prof.ProfileType == ColorProfileType.Curve)
					return sGrey;

				if (prof is not MatrixProfile mp || isWideGamut(mp.Matrix))
					return DisplayP3;
			}

			return prof.ProfileType == ColorProfileType.Curve ? sGrey : sRGB;

			// check for red or green x,y coordinates outside sRGB gamut
			static bool isWideGamut(Matrix4x4 m) =>
				m.M11 / (m.M11 + m.M12 + m.M13) > 0.67f ||
				m.M22 / (m.M21 + m.M22 + m.M23) > 0.62f;
		}

		public bool IsValid { get; }
		public byte[] ProfileBytes { get; }
		public ProfileColorSpace DataColorSpace { get; }
		public ProfileColorSpace PcsColorSpace { get; }

		public ColorProfileType ProfileType { get; protected set; }

		public bool IsCompatibleWith(PixelFormat fmt) =>
			(DataColorSpace == ProfileColorSpace.Cmyk && fmt.ColorRepresentation == PixelColorRepresentation.Cmyk) ||
			(DataColorSpace == ProfileColorSpace.Grey && fmt.ColorRepresentation == PixelColorRepresentation.Grey) ||
			(DataColorSpace == ProfileColorSpace.Rgb && (fmt.ColorRepresentation == PixelColorRepresentation.Rgb || fmt.ColorRepresentation == PixelColorRepresentation.Bgr));

		private ColorProfile() => ProfileBytes = Array.Empty<byte>();

		protected ColorProfile(byte[] bytes, ProfileColorSpace dataSpace, ProfileColorSpace pcsSpace, ColorProfileType profileType)
		{
			IsValid = true;
			ProfileBytes = bytes;
			DataColorSpace = dataSpace;
			PcsColorSpace = pcsSpace;
			ProfileType = profileType;
		}

		public class ProfileCurve
		{
			public float[] Gamma { get; }
			public float[] InverseGamma { get; }

			public ProfileCurve(float[] gamma, float[] inverseGamma)
			{
				Gamma = gamma;
				InverseGamma = inverseGamma;
			}
		}
	}

	internal class CurveProfile : ColorProfile
	{
		private readonly ConcurrentDictionary<(Type, Type, ConverterDirection, bool), IConverter> converterCache = new();

		public bool IsLinear { get; }
		public ProfileCurve Curve { get; }

		public CurveProfile(byte[] bytes, ProfileCurve? curve, ProfileColorSpace dataSpace, ProfileColorSpace pcsSpace) : base(bytes, dataSpace, pcsSpace, ColorProfileType.Curve)
		{
			IsLinear = curve is null;
			Curve = curve ?? new ProfileCurve(null!, LookupTables.Alpha);
		}

		private IConverter addConverter((Type tfrom, Type tto, ConverterDirection direction, bool videoLevels) cacheKey) => converterCache.GetOrAdd(cacheKey, _ => {
			if (IsLinear)
			{
				if (cacheKey.tfrom == typeof(float) && cacheKey.tto == typeof(float))
					return NoopConverter.Instance;
				if (cacheKey.tfrom == typeof(byte) && cacheKey.tto == typeof(float))
					return cacheKey.videoLevels ? FloatConverter.Widening.InstanceVideoRange : FloatConverter.Widening.InstanceFullRange;
				if (cacheKey.tfrom == typeof(float) && cacheKey.tto == typeof(byte))
					return FloatConverter.Narrowing.Instance;
				if (cacheKey.tfrom == typeof(ushort) && cacheKey.tto == typeof(byte))
					return UQ15Converter.Instance;
			}

			if (cacheKey.direction == ConverterDirection.FromLinear)
			{
				var gt = Curve.Gamma;

				if (cacheKey.tfrom == typeof(float) && cacheKey.tto == typeof(float))
					return new ConverterFromLinear<float, float>(gt);
				if (cacheKey.tfrom == typeof(ushort) && cacheKey.tto == typeof(byte))
					return new ConverterFromLinear<ushort, byte>(gt == LookupTables.SrgbGamma ? LookupTables.SrgbGammaUQ15 : LookupTables.MakeUQ15Gamma(gt));
				if (cacheKey.tfrom == typeof(float) && cacheKey.tto == typeof(byte))
					return new ConverterFromLinear<float, byte>(gt == LookupTables.SrgbGamma ? LookupTables.SrgbGammaUQ15 : LookupTables.MakeUQ15Gamma(gt));
			}

			if (cacheKey.direction == ConverterDirection.ToLinear)
			{
				var igt = Curve.InverseGamma;

				if (cacheKey.tfrom == typeof(float) && cacheKey.tto == typeof(float))
					return new ConverterToLinear<float, float>(igt);
				if (cacheKey.tfrom == typeof(byte) && cacheKey.tto == typeof(float))
					return new ConverterToLinear<byte, float>(cacheKey.videoLevels ? LookupTables.MakeVideoInverseGamma(igt) : igt);
				if (cacheKey.tfrom == typeof(byte) && cacheKey.tto == typeof(ushort))
					return new ConverterToLinear<byte, ushort>(LookupTables.MakeUQ15InverseGamma(cacheKey.videoLevels ? LookupTables.MakeVideoInverseGamma(igt) : igt));
			}

			throw new ArgumentException("Invalid Type combination", nameof(cacheKey));
		});

		public IConverter<TFrom, TTo> GetConverter<TFrom, TTo>(ConverterDirection direction, bool videoRange = false) where TFrom : unmanaged where TTo : unmanaged
		{
			var cacheKey = (typeof(TFrom), typeof(TTo), direction, videoRange);

			return (IConverter<TFrom, TTo>)(converterCache.TryGetValue(cacheKey, out var converter) ? converter : addConverter(cacheKey));
		}
	}

	internal class MatrixProfile : CurveProfile
	{
		public Matrix4x4 Matrix { get; }
		public Matrix4x4 InverseMatrix { get; }

		public MatrixProfile(byte[] bytes, Matrix4x4 matrix, Matrix4x4 imatrix, ProfileCurve? curve, ProfileColorSpace dataSpace, ProfileColorSpace pcsSpace) : base(bytes, curve, dataSpace, pcsSpace)
		{
			ProfileType = ColorProfileType.Matrix;
			Matrix = matrix;
			InverseMatrix = imatrix;
		}
	}

	internal static class IccProfiles
	{
		private static byte[] getResourceBinary(string name)
		{
			string resName = nameof(PhotoSauce) + "." + nameof(MagicScaler) + ".Resources." + name;
			using var stm = typeof(IccProfiles).Assembly.GetManifestResourceStream(resName)!;

			var buff = new byte[(int)stm.Length];
			stm.Read(buff, 0, buff.Length);

			return buff;
		}

		public static readonly Lazy<byte[]> sRgbV4 = new(() => getResourceBinary("sRGB-v4.icc"));
		public static readonly Lazy<byte[]> sRgbCompact = new(() => getResourceBinary("sRGB-v2-micro.icc"));
		public static readonly Lazy<byte[]> sGreyV4 = new(() => getResourceBinary("sGrey-v4.icc"));
		public static readonly Lazy<byte[]> sGreyCompact = new(() => getResourceBinary("sRGB-v2-micro.icc"));
		public static readonly Lazy<byte[]> AdobeRgb = new(() => getResourceBinary("AdobeCompat-v2.icc"));
		public static readonly Lazy<byte[]> DisplayP3V4 = new(() => getResourceBinary("DisplayP3Compat-v4.icc"));
		public static readonly Lazy<byte[]> DisplayP3Compact = new(() => getResourceBinary("DisplayP3Compat-v2-micro.icc"));
	}
}
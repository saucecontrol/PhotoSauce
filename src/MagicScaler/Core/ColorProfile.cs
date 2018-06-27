using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using static PhotoSauce.MagicScaler.MathUtil;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PhotoSauce.MagicScaler
{
	//http://www.color.org/specification/ICC1v43_2010-12.pdf
	internal class ColorProfile
	{
		internal static class Cache
		{
			private static ConcurrentDictionary<Guid, WeakReference<ColorProfile>> dic = new ConcurrentDictionary<Guid, WeakReference<ColorProfile>>();

			public static ColorProfile GetOrAdd(ArraySegment<byte> bytes)
			{
				using (var md5 = MD5.Create())
				{
					var hash = md5.ComputeHash(bytes.Array, bytes.Offset, bytes.Count);
					var guid = new Guid(hash);
					if (dic.TryGetValue(guid, out var wref) && wref.TryGetTarget(out var prof))
						return prof;

					prof = new ColorProfile(bytes.AsSpan());
					dic.AddOrUpdate(guid, (g) => new WeakReference<ColorProfile>(prof), (g, r) => { r.SetTarget(prof); return r; });

					return prof;
				}
			}
		}

		private static Lazy<ColorProfile> srgb = new Lazy<ColorProfile>(() => {
			var m = new Matrix4x4(
				0.71393112f, 0.06062103f, 0.14307328f, 0f,
				0.09707674f, 0.71694100f, 0.38510027f, 0f,
				0.01389754f, 0.22243797f, 0.43602939f, 0f,
				0f, 0f, 0f, 1f
			);
			Matrix4x4.Invert(m, out var im);

			return new ColorProfile {
				Matrix = m,
				InverseMatrix = im,
				Gamma = LookupTables.SrgbGamma,
				InverseGammaFloat = LookupTables.SrgbInverseGammaFloat,
				InverseGammaUQ15 = LookupTables.SrgbInverseGammaUQ15
			};
		});

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
			public const uint acsp = 0x61637370u;
			public const uint RGB  = 0x52474220u;
			public const uint GRAY = 0x47524159u;
			public const uint CMYK = 0x434d594bu;
			public const uint XYZ  = 0x58595a20u;
			public const uint Lab  = 0x4C616220u;
		}

		private static class IccTags
		{
			public const uint bXYZ = 0x6258595au;
			public const uint gXYZ = 0x6758595au;
			public const uint rXYZ = 0x7258595au;
			public const uint bTRC = 0x62545243u;
			public const uint gTRC = 0x67545243u;
			public const uint rTRC = 0x72545243u;
			public const uint kTRC = 0x6b545243u;
			public const uint A2B0 = 0x41324230u;
			public const uint B2A0 = 0x42324130u;
		}

		private static class IccTypes
		{
			public const uint XYZ  = 0x58595a20u;
			public const uint curv = 0x63757276u;
			public const uint para = 0x70617261u;
		}

		private static class IccD50
		{
			public const uint X = 0x0f6d6u;
			public const uint Y = 0x10000u;
			public const uint Z = 0x0d32du;
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

		public ProfileColorSpace DataColorSpace { get; private set; }
		public ProfileColorSpace PcsColorSpace { get; private set; }

		public bool IsValid { get; private set; }
		public bool IsGreyTrc { get; private set; }
		public bool IsRgbMatrix { get; private set; }
		public bool IsLinear { get; private set; }
		public bool IsSrgb { get; private set; }

		public byte[] Gamma { get; private set; }
		public float[] InverseGammaFloat { get; private set; }
		public ushort[] InverseGammaUQ15 { get; private set; }

		public Matrix4x4 Matrix { get; private set; }
		public Matrix4x4 InverseMatrix { get; private set; }

		private void lutsFromPower(float gamma, float[] igtf, ushort[] igtq, byte[] gt)
		{
			for (int i = 0; i < igtf.Length; i++)
			{
				float f = PowF((float)i / byte.MaxValue, gamma);
				igtf[i] = f;
				igtq[i] = FixToUQ15(f);
			}

			gamma = 1f / gamma;
			for (int i = 0; i < gt.Length; i++)
				gt[i] = FixToByte(PowF(UnFix15ToFloat(i), gamma));
		}

		private void lutsFromPoints(ushort[] points, float[] igtf, ushort[] igtq, byte[] gt)
		{
			float div = 1f / ushort.MaxValue;
			var curve = new float[points.Length];
			for (int i = 0; i < curve.Length; i++)
				curve[i] = points[i] * div;

			float cscal = curve.Length - 1;
			float cstep = 1f / cscal;
			float vstep = 1f / (igtf.Length - 1);

			for (int i = 0; i < igtf.Length; i++)
			{
				float val = i * vstep;
				float pos = (val * cscal).Floor();
				float rem = val - (pos * cstep);
				int idx = (int)pos;

				val = curve[idx];
				if (rem > 0.000001f)
					val = Lerp(val, curve[idx + 1], rem * cscal);

				igtf[i] = val;
				igtq[i] = FixToUQ15(val);
			}

			float scale = (float)ushort.MaxValue / UQ15One;
			for (int i = 0; i < gt.Length; i++)
			{
				ushort val = (ushort)(i * scale + FloatRound);
				float pos = 0f;
				int idx = Array.BinarySearch(points, val);
				if (idx >= 0)
					pos = idx;
				else
				{
					idx = ~idx;
					if (idx == points.Length)
						pos = points.Length - 1;
					else
					{
						int vh = points[idx];
						int vl = points[idx - 1];
						if (vl == vh)
							pos = idx;
						else
							pos = (idx - 1) + ((float)(val - vl) / (vh - vl));
					}
				}

				gt[i] = FixToByte(pos / (points.Length - 1));
			}
		}

		private void lutsFromParameters(float a, float b, float c, float d, float e, float f, float g, float[] igtf, ushort[] igtq, byte[] gt)
		{
			for (int i = 0; i < igtf.Length; i++)
			{
				float val = (float)i / byte.MaxValue;
				if (val >= d)
					val = PowF(val * a + b, g) + c + e;
				else
					val = val * c + f;

				igtf[i] = val;
				igtq[i] = FixToUQ15(val);
			}

			g = 1f / g;
			for (int i = 0; i < gt.Length; i++)
			{
				float val = UnFix15ToFloat(i);
				if (val > (c * d + f))
					val = (PowF(val - c - e, g) - b) / a;
				else
					val = c == 0f ? 0f : ((val - f) / c);

				gt[i] = FixToByte(val);
			}
		}

		private bool lutInverts(ushort[] igtq, byte[] gt)
		{
			for (int i = 0; i < igtq.Length; i++)
			{
				if (gt[igtq[i]] != i)
					return false;
			}

			return true;
		}

		private bool tryGetTagEntry(TagEntry[] entries, uint tag, out TagEntry entry)
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

		private bool tryGetMatrix(ReadOnlySpan<byte> bXYZ, ReadOnlySpan<byte> gXYZ, ReadOnlySpan<byte> rXYZ)
		{
			var hdr = bXYZ.Slice(0, 8);
			uint tag = ReadUInt32BigEndian(hdr);
			if (tag != IccTypes.XYZ || !hdr.SequenceEqual(gXYZ.Slice(0, 8)) || !hdr.SequenceEqual(rXYZ.Slice(0, 8)))
				return false;

			uint bx = ReadUInt32BigEndian(bXYZ.Slice(8));
			uint gx = ReadUInt32BigEndian(gXYZ.Slice(8));
			uint rx = ReadUInt32BigEndian(rXYZ.Slice(8));

			uint by = ReadUInt32BigEndian(bXYZ.Slice(12));
			uint gy = ReadUInt32BigEndian(gXYZ.Slice(12));
			uint ry = ReadUInt32BigEndian(rXYZ.Slice(12));

			uint bz = ReadUInt32BigEndian(bXYZ.Slice(16));
			uint gz = ReadUInt32BigEndian(gXYZ.Slice(16));
			uint rz = ReadUInt32BigEndian(rXYZ.Slice(16));

			float div = 1f / 65536f;
			Matrix = new Matrix4x4(
				bz * div, by * div, bx * div, 0f,
				gz * div, gy * div, gx * div, 0f,
				rz * div, ry * div, rx * div, 0f,
				0f,       0f,       0f,       1f
			);

			bool invertible = Matrix4x4.Invert(Matrix, out var imatrix);
			InverseMatrix = imatrix;
			return invertible;
		}

		private bool tryGetCurve(ReadOnlySpan<byte> trc)
		{
			uint tag = ReadUInt32BigEndian(trc);
			if (trc.Length < 12 || tag != IccTypes.curv && tag != IccTypes.para)
				return false;

			var igtf = new float[256];
			var igtq = new ushort[256];
			var gt = new byte[UQ15One + 1];

			if (tag == IccTypes.curv)
			{
				uint pcnt = ReadUInt32BigEndian(trc.Slice(8));
				if (trc.Length < (12 + pcnt * sizeof(ushort)))
					return false;

				if (pcnt == 0)
				{
					IsLinear = true;
					return true;
				}

				if (pcnt == 1)
				{
					ushort gi = ReadUInt16BigEndian(trc.Slice(12));
					float gf = 0f;
					switch (gi)
					{
						case 0:
							return false;
						case 256:
							IsLinear = true;
							return true;
						case 461:
							gf = 1.8f;
							break;
						case 563:
							gf = 2.2f;
							break;
						default:
							gf = gi / 256f;
							break;
					}

					lutsFromPower(gf, igtf, igtq, gt);
				}
				else
				{
					if (pcnt == 2 && ReadUInt16BigEndian(trc.Slice(12)) == ushort.MinValue && ReadUInt16BigEndian(trc.Slice(14)) == ushort.MaxValue)
					{
						IsLinear = true;
						return true;
					}

					var points = new ushort[pcnt];
					for (int i = 0; i < points.Length; i++)
						points[i] = ReadUInt16BigEndian(trc.Slice(12 + i * sizeof(ushort)));

					lutsFromPoints(points, igtf, igtq, gt);
				}
			}

			if (tag == IccTypes.para)
			{
				ushort func = ReadUInt16BigEndian(trc.Slice(8));
				if (trc.Length < 16 || func > 4)
					return false;

				var param = trc.Slice(12);
				int g = ReadInt32BigEndian(param);
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
					(c == 0 && (func == 3 || func == 4)) ||
					((uint)c > 0x10000u && (func == 2 || func == 3 || func == 4)) ||
					((uint)d > 0x10000u && (func == 3 || func == 4)) ||
					((uint)e > 0x10000u && (func == 4))
				) return false;

				float div = 1f / 65536;
				float fa = a * div, fb = b * div, fc = c * div, fd = d * div, fe = e * div, ff = f * div, fg = g * div;
				switch (func)
				{
					case 1:
						fd = -fb / fa;
						break;
					case 2:
						ff = fe = fc;
						fc = 0f;
						goto case 1;
					case 3:
					case 4:
						fe -= fc;
						break;
				}

				if (func == 0)
					lutsFromPower(fg, igtf, igtq, gt);
				else
					lutsFromParameters(fa, fb, fc, fd, fe, ff, fg, igtf, igtq, gt);
			}

			Gamma = gt;
			InverseGammaFloat = igtf;
			InverseGammaUQ15 = igtq;

			return true;
		}

		unsafe private void parse(ReadOnlySpan<byte> prof)
		{
			if (prof.Length < 132)
				return; //throw new InvalidDataException("Invalid ICC profile.  Header is incomplete.");

			uint len = ReadUInt32BigEndian(prof.Slice(0, sizeof(uint)));
			if (len != prof.Length)
				return; //throw new InvalidDataException("Invalid ICC profile.  Internal length doesn't match data length.");

			var acsp = ReadUInt32BigEndian(prof.Slice(36));
			var ver = prof.Slice(8, 4);
			IsValid = (ver[0] == 2 || ver[0] == 4) && acsp == IccStrings.acsp;
			if (!IsValid)
				return;

			uint dcs = ReadUInt32BigEndian(prof.Slice(16));
			DataColorSpace =
				dcs == IccStrings.RGB  ? ProfileColorSpace.Rgb  :
				dcs == IccStrings.GRAY ? ProfileColorSpace.Grey :
				dcs == IccStrings.CMYK ? ProfileColorSpace.Cmyk :
				ProfileColorSpace.Other;

			uint ccs = ReadUInt32BigEndian(prof.Slice(20));
			PcsColorSpace =
				ccs == IccStrings.XYZ ? ProfileColorSpace.Xyz :
				ccs == IccStrings.Lab ? ProfileColorSpace.Lab :
				ProfileColorSpace.Other;

			if (PcsColorSpace != ProfileColorSpace.Xyz || (DataColorSpace != ProfileColorSpace.Rgb && DataColorSpace != ProfileColorSpace.Grey))
				return;

			uint tagCount = ReadUInt32BigEndian(prof.Slice(128));
			if (len < (132 + tagCount * Unsafe.SizeOf<TagEntry>()))
			{
				IsValid = false;
				return;
			}

			var tagEntries = new TagEntry[((int)tagCount)];
			for (int i = 0; i < tagCount; i++)
			{
				int entryStart = 132 + i * Unsafe.SizeOf<TagEntry>();

				uint tag = ReadUInt32BigEndian(prof.Slice(entryStart));
				uint pos = ReadUInt32BigEndian(prof.Slice(entryStart + 4));
				uint cb = ReadUInt32BigEndian(prof.Slice(entryStart + 8));

				if (len < (pos + cb))
				{
					IsValid = false;
					return;
				}

				// not handling these yet, so we'll hand off to WCS
				if (tag == IccTags.A2B0 || tag == IccTags.B2A0)
					return;

				tagEntries[i] = new TagEntry(tag, (int)pos, (int)cb);
			}

			if (DataColorSpace == ProfileColorSpace.Grey && tryGetTagEntry(tagEntries, IccTags.kTRC, out var kTRC))
			{
				IsGreyTrc = tryGetCurve(prof.Slice(kTRC.pos, kTRC.cb));
			}

			if (DataColorSpace == ProfileColorSpace.Rgb && tryGetTagEntry(tagEntries, IccTags.bTRC, out var bTRC)
				&& tryGetTagEntry(tagEntries, IccTags.gTRC, out var gTRC) && tryGetTagEntry(tagEntries, IccTags.rTRC, out var rTRC))
			{
				var bTRCData = prof.Slice(bTRC.pos, bTRC.cb);
				var gTRCData = prof.Slice(gTRC.pos, gTRC.cb);
				var rTRCData = prof.Slice(rTRC.pos, rTRC.cb);

				IsRgbMatrix = bTRCData.SequenceEqual(gTRCData) && bTRCData.SequenceEqual(rTRCData) && tryGetCurve(bTRCData);
			}

			if (IsRgbMatrix && tryGetTagEntry(tagEntries, IccTags.bXYZ, out var bXYZ)
				 && tryGetTagEntry(tagEntries, IccTags.gXYZ, out var gXYZ) && tryGetTagEntry(tagEntries, IccTags.rXYZ, out var rXYZ))
			{
				IsRgbMatrix = tryGetMatrix(prof.Slice(bXYZ.pos, bXYZ.cb), prof.Slice(gXYZ.pos, gXYZ.cb), prof.Slice(rXYZ.pos, rXYZ.cb));
			}

			IsSrgb = Matrix.IsRouglyEqualTo(sRGB.Matrix) && lutInverts(InverseGammaUQ15, sRGB.Gamma);
		}

		public static ColorProfile sRGB => srgb.Value;

		private ColorProfile() { }

		public ColorProfile(ReadOnlySpan<byte> profileData)
		{
			parse(profileData);
		}
	}
}
using System;

using PhotoSauce.MagicScaler.Interop;

namespace PhotoSauce.MagicScaler
{
	internal abstract class WicFormatConverterBase : WicBitmapSourceBase
	{
		protected byte[] LineBuff;
		protected Guid OutFormat;
		protected bool HasAlpha;

		public override Guid GetPixelFormat() => OutFormat;

		public WicFormatConverterBase(IWICBitmapSource source, Guid dstFormat) : base(source)
		{
			OutFormat = dstFormat;
			LineBuff = new byte[Stride];
			HasAlpha = Format == Consts.GUID_WICPixelFormat64bppBGRA || Format == Consts.GUID_WICPixelFormat32bppBGRA;
		}
	}

	internal class WicLinearFormatConverter : WicFormatConverterBase
	{
		public WicLinearFormatConverter(IWICBitmapSource source, Guid dstFormat) : base(source, dstFormat) { }

		unsafe public override void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			fixed (byte* bstart = LineBuff)
			fixed (ushort* igtstart = LookupTables.InverseGamma, atstart = LookupTables.Alpha)
			{
				int oh = prc.Height, oy = prc.Y;

				prc.Height = 1;
				for (int y = 0; y < oh; y++)
				{
					prc.Y = oy + y;
					Source.CopyPixels(prc, Stride, Stride, (IntPtr)bstart);

					ushort* op = (ushort*)pbBuffer + y * cbStride;
					if (HasAlpha)
						mapValuesWithAlpha(bstart, op, igtstart, atstart, (uint)prc.Width * Bpp);
					else
						mapValues(bstart, op, igtstart, (uint)prc.Width * Bpp);
				}
			}
		}

		unsafe private static void mapValues(byte* ipstart, ushort* opstart, ushort* igtstart, uint len)
		{
			byte* ip = ipstart + 8, ipe = ipstart + len;
			ushort* op = opstart + 8, igt = igtstart;

			while (ip < ipe)
			{
				ushort o0 = igt[ip[-8]];
				ushort o1 = igt[ip[-7]];
				ushort o2 = igt[ip[-6]];
				ushort o3 = igt[ip[-5]];
				op[-8] = o0;
				op[-7] = o1;
				op[-6] = o2;
				op[-5] = o3;

				o0 = igt[ip[-4]];
				o1 = igt[ip[-3]];
				o2 = igt[ip[-2]];
				o3 = igt[ip[-1]];
				op[-4] = o0;
				op[-3] = o1;
				op[-2] = o2;
				op[-1] = o3;

				op += 8;
				ip += 8;
			}

			ip -= 8;
			op -= 8;
			while (ip < ipe)
			{
				op[0] = igt[ip[0]];
				ip++;
				op++;
			}
		}

		unsafe private static void mapValuesWithAlpha(byte* ipstart, ushort* opstart, ushort* igtstart, ushort* atstart, uint len)
		{
			byte* ip = ipstart + 4, ipe = ip + len;
			ushort* op = opstart + 4, igt = igtstart, at = atstart;

			while (ip < ipe)
			{
				ushort o0 = igt[ip[-4]];
				ushort o1 = igt[ip[-3]];
				ushort o2 = igt[ip[-2]];
				ushort o3 = at[ip[-1]];
				op[-4] = o0;
				op[-3] = o1;
				op[-2] = o2;
				op[-1] = o3;
				op += 4;
				ip += 4;
			}
		}
	}

	internal class WicGammaFormatConverter : WicFormatConverterBase
	{
		public WicGammaFormatConverter(IWICBitmapSource source, Guid dstFormat) : base(source, dstFormat) { }

		unsafe public override void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			fixed (byte* bstart = LineBuff, gtstart = LookupTables.Gamma)
			{
				int oh = prc.Height, oy = prc.Y;

				prc.Height = 1;
				for (int y = 0; y < oh; y++)
				{
					prc.Y = oy + y;
					Source.CopyPixels(prc, Stride, Stride, (IntPtr)bstart);

					byte* op = (byte*)pbBuffer + y * cbStride;
					if (HasAlpha)
						mapValuesWithAlpha((ushort*)bstart, op, gtstart, (uint)prc.Width * Bpp / sizeof(ushort));
					else
						mapValues((ushort*)bstart, op, gtstart, (uint)prc.Width * Bpp / sizeof(ushort));
				}
			}
		}

		unsafe private static void mapValues(ushort* ipstart, byte* opstart, byte* gtstart, uint len)
		{
			ushort* ip = ipstart + 8, ipe = ipstart + len;
			byte* op = opstart + 8, gt = gtstart;

			while (ip < ipe)
			{
				byte o0 = gt[ip[-8]];
				byte o1 = gt[ip[-7]];
				byte o2 = gt[ip[-6]];
				byte o3 = gt[ip[-5]];
				op[-8] = o0;
				op[-7] = o1;
				op[-6] = o2;
				op[-5] = o3;

				o0 = gt[ip[-4]];
				o1 = gt[ip[-3]];
				o2 = gt[ip[-2]];
				o3 = gt[ip[-1]];
				op[-4] = o0;
				op[-3] = o1;
				op[-2] = o2;
				op[-1] = o3;

				op += 8;
				ip += 8;
			}

			ip -= 8;
			op -= 8;
			while (ip < ipe)
			{
				op[0] = gt[ip[0]];
				ip++;
				op++;
			}
		}

		unsafe private static void mapValuesWithAlpha(ushort* ipstart, byte* opstart, byte* gtstart, uint len)
		{
			ushort* ip = ipstart + 4, ipe = ip + len;
			byte* op = opstart + 4, gt = gtstart;

			while (ip < ipe)
			{
				byte o0 = gt[ip[-4]];
				byte o1 = gt[ip[-3]];
				byte o2 = gt[ip[-2]];
				byte o3 = (byte)Math.Min(MathUtil.UnscaleToInt32(ip[-1] << 8), 255);
				op[-4] = o0;
				op[-3] = o1;
				op[-2] = o2;
				op[-1] = o3;
				op += 4;
				ip += 4;
			}
		}
	}
}

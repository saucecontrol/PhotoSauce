using System;
using System.Drawing;

#if !NET46
using System.Drawing.Temp;
#endif

using PhotoSauce.MagicScaler.Interop;
using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler
{
	internal class WicMatte : WicBitmapSourceBase
	{
		byte MaskRed, MaskGreen, MaskBlue;
		ushort MaskRedLinear, MaskGreenLinear, MaskBlueLinear;

		public WicMatte(IWICBitmapSource source, Color color) : base(source)
		{
			if (Format != Consts.GUID_WICPixelFormat32bppBGRA && Format != Consts.GUID_WICPixelFormat64bppBGRA)
				throw new NotSupportedException("Pixel format not supported.  Must be BGRA");

			MaskRed = color.R;
			MaskGreen = color.G;
			MaskBlue = color.B;

			var igt = LookupTables.InverseGamma;

			MaskRedLinear = igt[MaskRed];
			MaskGreenLinear = igt[MaskGreen];
			MaskBlueLinear = igt[MaskBlue];
		}

		unsafe public override void CopyPixels(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			Source.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);

			if (Format == Consts.GUID_WICPixelFormat64bppBGRA)
			{
				applyMatteLinear(prc, (ushort*)pbBuffer, (int)(cbStride / sizeof(ushort)));
			}
			else
			{
				fixed (ushort* igtstart = LookupTables.InverseGamma, atstart = LookupTables.Alpha)
				fixed (byte* gtstart = LookupTables.Gamma)
					applyMatteGamma(prc, igtstart, gtstart, atstart, (byte*)pbBuffer, (int)cbStride);
			}
		}

		unsafe private void applyMatteLinear(WICRect prc, ushort* pixels, int stride)
		{
			const ushort maxalpha = IntScale;
			ushort mrl = MaskRedLinear, mgl = MaskGreenLinear, mbl = MaskBlueLinear;

			for (int y = 0; y < prc.Height; y++)
			{
				ushort* ip = pixels + y * stride;
				ushort* ipe = ip + prc.Width * Channels;

				while (ip < ipe)
				{
					int alpha = ip[3];
					if (alpha == 0)
					{
						ip[0] = mbl;
						ip[1] = mgl;
						ip[2] = mrl;
						ip[3] = maxalpha;
					}
					else if (alpha < IntScale)
					{
						int ia = alpha, ma = maxalpha - ia;
						ushort ib = ip[0];
						ushort ig = ip[1];
						ushort ir = ip[2];

						ib = UnscaleToUInt15(ib * ia + mbl * ma);
						ig = UnscaleToUInt15(ig * ia + mgl * ma);
						ir = UnscaleToUInt15(ir * ia + mrl * ma);

						ip[0] = ib;
						ip[1] = ig;
						ip[2] = ir;
						ip[3] = maxalpha;
					}

					ip += 4;
				}
			}
		}

		unsafe private void applyMatteGamma(WICRect prc, ushort* igtstart, byte* gtstart, ushort* atstart, byte* pixels, int stride)
		{
			const byte maxalpha = byte.MaxValue;

			byte* gt = gtstart;
			ushort* igt = igtstart, at = atstart;

			for (int y = 0; y < prc.Height; y++)
			{
				byte* ip = pixels + y * stride;
				byte* ipe = ip + prc.Width * Channels;

				while (ip < ipe)
				{
					int alpha = ip[3];
					if (alpha == 0)
					{
						ip[0] = MaskBlue;
						ip[1] = MaskGreen;
						ip[2] = MaskRed;
						ip[3] = maxalpha;
					}
					else if (alpha < maxalpha)
					{
						int ia = at[alpha], ma = IntScale - ia;
						int ib = igt[ip[0]];
						int ig = igt[ip[1]];
						int ir = igt[ip[2]];

						ib = UnscaleToUInt15(ib * ia + MaskBlueLinear * ma);
						ig = UnscaleToUInt15(ig * ia + MaskGreenLinear * ma);
						ir = UnscaleToUInt15(ir * ia + MaskRedLinear * ma);

						ip[0] = gt[ib];
						ip[1] = gt[ig];
						ip[2] = gt[ir];
						ip[3] = maxalpha;
					}

					ip += 4;
				}
			}
		}
	}
}

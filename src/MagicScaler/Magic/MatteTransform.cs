using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;

#if DRAWING_SHIM
using System.Drawing.Temp;
#endif

using PhotoSauce.MagicScaler.Interop;
using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler
{
	internal class MatteTransform : PixelSource
	{
		private byte maskRed, maskGreen, maskBlue;

		public MatteTransform(PixelSource source, Color color) : base(source)
		{
			if (Format.ColorRepresentation != PixelColorRepresentation.Bgr || Format.AlphaRepresentation == PixelAlphaRepresentation.None)
				throw new NotSupportedException("Pixel format not supported.  Must be BGRA");

			maskRed = color.R;
			maskGreen = color.G;
			maskBlue = color.B;
		}

		unsafe protected override void CopyPixelsInternal(WICRect prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer)
		{
			Timer.Stop();
			Source.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
			Timer.Start();

			if (Format == PixelFormat.Bgra128BppLinearFloat || Format == PixelFormat.Pbgra128BppLinearFloat)
				applyMatteLinearFloat(prc, (float*)pbBuffer, (int)(cbStride / sizeof(float)));
			else if (Format == PixelFormat.Bgra64BppLinearUQ15 || Format == PixelFormat.Pbgra64BppLinearUQ15)
				applyMatteLinear(prc, (ushort*)pbBuffer, (int)(cbStride / sizeof(ushort)), Format.AlphaRepresentation == PixelAlphaRepresentation.Associated);
			else if (Format.FormatGuid == Consts.GUID_WICPixelFormat32bppBGRA || Format.FormatGuid == Consts.GUID_WICPixelFormat32bppPBGRA)
				applyMatteCompressed(prc, (byte*)pbBuffer, (int)cbStride, Format.AlphaRepresentation == PixelAlphaRepresentation.Associated);
		}

		unsafe private void applyMatteLinearFloat(WICRect prc, float* pixels, int stride)
		{
			var igt = LookupTables.InverseGammaFloat;
			float mrl = igt[maskRed], mgl = igt[maskGreen], mbl = igt[maskBlue];

			var v1 = new Vector<float>(1f);
			var vmat = new Vector<float>(new[] { mbl, mgl, mrl, 1f, mbl, mgl, mrl, 1f });
			var vm0 = new Vector<int>(new[] { -1, -1, -1, -1,  0,  0,  0,  0 });
			var vm1 = new Vector<int>(new[] { -1, -1, -1,  0, -1, -1, -1,  0 });

			for (int y = 0; y < prc.Height; y++)
			{
				float* ip = pixels + y * stride;
				float* ipe = ip + prc.Width * 4 - Vector<float>.Count;

				while (ip <= ipe)
				{
					var vi = Unsafe.Read<Vector<float>>(ip);
					float ia0 = vi[3], ia1 = Vector<float>.Count == 8 ? vi[7] : ia0;

					if (ia0 == 0 && ia1 == 0)
					{
						Unsafe.Write(ip, vmat);
					}
					else if (ia0 < 1f || ia1 < 1f)
					{
						var vpa = Vector.ConditionalSelect(vm0, new Vector<float>(ia0), new Vector<float>(ia1));
						var vma = v1 - vpa;

						var vr = vi + vmat * vma;
						var vo = Vector.ConditionalSelect(vm1, vr, v1);
						Unsafe.Write(ip, vo);
					}

					ip += Vector<float>.Count;
				}

				ipe += Vector<float>.Count;
				while (ip < ipe)
				{
					float ib = ip[0], ig = ip[1], ir = ip[2], ia = ip[3], ma = 1f - ia;

					ib += mbl * ma;
					ig += mgl * ma;
					ir += mrl * ma;

					ip[0] = ib;
					ip[1] = ig;
					ip[2] = ir;
					ip[3] = 1f;

					ip += 4;
				}
			}
		}

		unsafe private void applyMatteLinear(WICRect prc, ushort* pixels, int stride, bool premul)
		{
			const ushort maxalpha = UQ15One;
			var igt = LookupTables.InverseGammaUQ15;
			ushort mrl = igt[maskRed], mgl = igt[maskGreen], mbl = igt[maskBlue];

			for (int y = 0; y < prc.Height; y++)
			{
				ushort* ip = pixels + y * stride;
				ushort* ipe = ip + prc.Width * 4;

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
					else if (alpha < maxalpha)
					{
						int ia = alpha, ma = maxalpha - ia;
						ushort ib = ip[0];
						ushort ig = ip[1];
						ushort ir = ip[2];
						if (premul) ia = UQ15One;

						ib = UnFixToUQ15(ib * ia + mbl * ma);
						ig = UnFixToUQ15(ig * ia + mgl * ma);
						ir = UnFixToUQ15(ir * ia + mrl * ma);

						ip[0] = ib;
						ip[1] = ig;
						ip[2] = ir;
						ip[3] = maxalpha;
					}

					ip += 4;
				}
			}
		}

		unsafe private void applyMatteCompressed(WICRect prc, byte* pixels, int stride, bool premul)
		{
			const byte maxalpha = byte.MaxValue;

			fixed (ushort* igtstart = &LookupTables.InverseGammaUQ15[0], atstart = &LookupTables.AlphaUQ15[0])
			fixed (byte* gtstart = &LookupTables.Gamma[0])
			{
				byte* gt = gtstart;
				ushort* igt = igtstart, at = atstart;
				ushort mrl = igt[maskRed], mgl = igt[maskGreen], mbl = igt[maskBlue];

				for (int y = 0; y < prc.Height; y++)
				{
					byte* ip = pixels + y * stride;
					byte* ipe = ip + prc.Width * 4;

					while (ip < ipe)
					{
						int alpha = ip[3];
						if (alpha == 0)
						{
							ip[0] = maskBlue;
							ip[1] = maskGreen;
							ip[2] = maskRed;
							ip[3] = maxalpha;
						}
						else if (alpha < maxalpha)
						{
							int ia = at[alpha], ma = UQ15One - ia;
							int ib = igt[ip[0]];
							int ig = igt[ip[1]];
							int ir = igt[ip[2]];
							if (premul) ia = UQ15One;

							ib = UnFixToUQ15(ib * ia + mbl * ma);
							ig = UnFixToUQ15(ig * ia + mgl * ma);
							ir = UnFixToUQ15(ir * ia + mrl * ma);

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
}

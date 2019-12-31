using System;
using System.Numerics;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

using PhotoSauce.Interop.Wic;
using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler
{
	internal class ColorMatrixTransformInternal : PixelSource
	{
		private readonly Vector4 vec0, vec1, vec2, vec3;

		public ColorMatrixTransformInternal(PixelSource source, Matrix4x4 matrix) : base(source)
		{
			vec0 = new Vector4(matrix.M33, matrix.M23, matrix.M13, matrix.M43);
			vec1 = new Vector4(matrix.M32, matrix.M22, matrix.M12, matrix.M42);
			vec2 = new Vector4(matrix.M31, matrix.M21, matrix.M11, matrix.M41);
			vec3 = new Vector4(matrix.M34, matrix.M24, matrix.M14, matrix.M44);
		}

		unsafe protected override void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, IntPtr pbBuffer)
		{
			Profiler.PauseTiming();
			Source.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
			Profiler.ResumeTiming();

			if (Format.NumericRepresentation == PixelNumericRepresentation.Float)
#if HWINTRINSICS
				if (Avx.IsSupported)
					copyPixelsAvx(prc, cbStride, pbBuffer);
				else
#endif
					copyPixelsFloat(prc, cbStride, pbBuffer);
			else if (Format.NumericRepresentation == PixelNumericRepresentation.Fixed)
				copyPixelsFixed(prc, cbStride, pbBuffer);
			else
				copyPixelsByte(prc, cbStride, pbBuffer);
		}

		unsafe private void copyPixelsByte(in PixelArea prc, int cbStride, IntPtr pbBuffer)
		{
			int* pm = stackalloc[] {
				Fix15(vec0.X), Fix15(vec0.Y), Fix15(vec0.Z), Fix15(vec0.W),
				Fix15(vec1.X), Fix15(vec1.Y), Fix15(vec1.Z), Fix15(vec1.W),
				Fix15(vec2.X), Fix15(vec2.Y), Fix15(vec2.Z), Fix15(vec2.W),
				                                             Fix15(vec3.W)
			};

			int chan = Format.ChannelCount;
			bool alpha = chan == 4 && pm[12] != UQ15One;

			for (int y = 0; y < prc.Height; y++)
			{
				byte* ip = (byte*)pbBuffer + y * cbStride, ipe = ip + prc.Width * chan;
				while (ip < ipe)
				{
					int i0 = ip[0];
					int i1 = ip[1];
					int i2 = ip[2];

					byte o0 = UnFix15ToByte(i0 * pm[0] + i1 * pm[1] + i2 * pm[ 2] + byte.MaxValue * pm[ 3]);
					byte o1 = UnFix15ToByte(i0 * pm[4] + i1 * pm[5] + i2 * pm[ 6] + byte.MaxValue * pm[ 7]);
					byte o2 = UnFix15ToByte(i0 * pm[8] + i1 * pm[9] + i2 * pm[10] + byte.MaxValue * pm[11]);

					ip[0] = o0;
					ip[1] = o1;
					ip[2] = o2;

					if (alpha)
						ip[3] = UnFix15ToByte(ip[3] * pm[12]);

					ip += chan;
				}
			}
		}

		unsafe private void copyPixelsFixed(in PixelArea prc, int cbStride, IntPtr pbBuffer)
		{
			int* pm = stackalloc[] {
				Fix15(vec0.X), Fix15(vec0.Y), Fix15(vec0.Z), Fix15(vec0.W),
				Fix15(vec1.X), Fix15(vec1.Y), Fix15(vec1.Z), Fix15(vec1.W),
				Fix15(vec2.X), Fix15(vec2.Y), Fix15(vec2.Z), Fix15(vec2.W),
				                                             Fix15(vec3.W)
			};

			int chan = Format.ChannelCount;
			bool alpha = chan == 4 && pm[12] != UQ15One;

			for (int y = 0; y < prc.Height; y++)
			{
				ushort* ip = (ushort*)((byte*)pbBuffer + y * cbStride), ipe = ip + prc.Width * chan;
				while (ip < ipe)
				{
					int i0 = ip[0];
					int i1 = ip[1];
					int i2 = ip[2];

					ushort o0 = UnFixToUQ15(i0 * pm[0] + i1 * pm[1] + i2 * pm[ 2] + UQ15One * pm[ 3]);
					ushort o1 = UnFixToUQ15(i0 * pm[4] + i1 * pm[5] + i2 * pm[ 6] + UQ15One * pm[ 7]);
					ushort o2 = UnFixToUQ15(i0 * pm[8] + i1 * pm[9] + i2 * pm[10] + UQ15One * pm[11]);

					ip[0] = o0;
					ip[1] = o1;
					ip[2] = o2;

					if (alpha)
						ip[3] = UnFixToUQ15(ip[3] * pm[12]);

					ip += chan;
				}
			}
		}

		unsafe private void copyPixelsFloat(in PixelArea prc, int cbStride, IntPtr pbBuffer)
		{
			int chan = Format.ChannelCount;
			bool alpha = Format.AlphaRepresentation != PixelAlphaRepresentation.None;

			Vector4 vm0 = vec0, vm1 = vec1, vm2 = vec2;
			float falpha = vec3.W, fone = Vector4.One.X;

			for (int y = 0; y < prc.Height; y++)
			{
				float* ip = (float*)((byte*)pbBuffer + y * cbStride), ipe = ip + prc.Width * chan;
				while (ip < ipe)
				{
					var vn = Unsafe.ReadUnaligned<Vector4>(ip);
					vn.W = fone;

					float f0 = Vector4.Dot(vn, vm0); 
					float f1 = Vector4.Dot(vn, vm1);
					float f2 = Vector4.Dot(vn, vm2);

					ip[0] = f0;
					ip[1] = f1;
					ip[2] = f2;

					if (alpha)
						ip[3] *= falpha;

					ip += 4;
				}
			}
		}

#if HWINTRINSICS
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		unsafe private void copyPixelsAvx(in PixelArea prc, int cbStride, IntPtr pbBuffer)
		{
			int chan = Format.ChannelCount;

			var vt0 = vec0;
			var vt1 = vec1;
			var vt2 = vec2;
			var vt3 = vec3;

			var vm0 = Avx.BroadcastVector128ToVector256((float*)&vt0);
			var vm1 = Avx.BroadcastVector128ToVector256((float*)&vt1);
			var vm2 = Avx.BroadcastVector128ToVector256((float*)&vt2);
			var vm3 = Avx.BroadcastVector128ToVector256((float*)&vt3);

			var vone = Vector256.Create(1f);

			for (int y = 0; y < prc.Height; y++)
			{
				float* ip = (float*)((byte*)pbBuffer + y * cbStride), ipe = ip + prc.Width * chan;

				ipe -= Vector256<float>.Count;
				while (ip <= ipe)
				{
					var vi = Avx.LoadVector256(ip);
					var vn = Avx.Blend(vi, vone, HWIntrinsics.BlendMaskAlpha);

					var vr0 = Avx.DotProduct(vn, vm0, 0b_1111_0001);
					var vr1 = Avx.DotProduct(vn, vm1, 0b_1111_0010);
					var vr2 = Avx.DotProduct(vn, vm2, 0b_1111_0100);

					vi = Avx.Multiply(vi, vm3);
					vi = Avx.Blend(vi, vr0, 0b_0001_0001);
					vi = Avx.Blend(vi, vr1, 0b_0010_0010);
					vi = Avx.Blend(vi, vr2, 0b_0100_0100);

					Avx.Store(ip, vi);

					ip += Vector256<float>.Count;
				}

				if (ip <= ipe + Vector128<float>.Count)
				{
					var vi = Sse.LoadVector128(ip);
					var vn = Sse41.Blend(vi, vone.GetLower(), HWIntrinsics.BlendMaskAlpha);

					var vr0 = Sse41.DotProduct(vn, vm0.GetLower(), 0b_1111_0001);
					var vr1 = Sse41.DotProduct(vn, vm1.GetLower(), 0b_1111_0010);
					var vr2 = Sse41.DotProduct(vn, vm2.GetLower(), 0b_1111_0100);

					vi = Sse.Multiply(vi, vm3.GetLower());
					vi = Sse41.Blend(vi, vr0, 0b_0001_0001);
					vi = Sse41.Blend(vi, vr1, 0b_0010_0010);
					vi = Sse41.Blend(vi, vr2, 0b_0100_0100);

					Sse.Store(ip, vi);
				}
			}
		}
#endif
	}

	/// <summary>Transforms an image according to coefficients in a <see cref="Matrix4x4" />.</summary>
	public sealed class ColorMatrixTransform : PixelTransform, IPixelTransformInternal
	{
		private readonly Matrix4x4 matrix;

		/// <summary>Constructs a new <see cref="ColorMatrixTransform" /> using the specified <paramref name="matrix" />.</summary>
		/// <param name="matrix">A 4x4 matrix of coefficients.  The channel order is RGBA, column-major.</param>
		public ColorMatrixTransform(Matrix4x4 matrix) => this.matrix = matrix;

		void IPixelTransformInternal.Init(PipelineContext ctx)
		{
			if (ctx.Source.Format.Encoding == PixelValueEncoding.Linear)
			{
				if (ctx.Source.Format.NumericRepresentation == PixelNumericRepresentation.Float)
					MagicTransforms.AddInternalFormatConverter(ctx, PixelValueEncoding.Companded);
				else
					MagicTransforms.AddExternalFormatConverter(ctx);
			}

			if (matrix != default && !matrix.IsIdentity)
			{
				var fmt = matrix.M44 < 1f || ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None ? Consts.GUID_WICPixelFormat32bppBGRA : Consts.GUID_WICPixelFormat24bppBGR;
				if (ctx.Source.Format.FormatGuid != fmt)
					ctx.Source = ctx.AddDispose(new ConversionTransform(ctx.Source, null, null, fmt));

				ctx.Source = new ColorMatrixTransformInternal(ctx.Source, matrix);
			}

			Source = ctx.Source;
		}
	}
}

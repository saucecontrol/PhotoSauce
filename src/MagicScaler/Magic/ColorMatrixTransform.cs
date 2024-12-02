// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
#if HWINTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

using static PhotoSauce.MagicScaler.MathUtil;

namespace PhotoSauce.MagicScaler.Transforms;

internal sealed class ColorMatrixTransformInternal : ChainedPixelSource
{
	private readonly Vector4 vec0, vec1, vec2, vec3;

	public ColorMatrixTransformInternal(PixelSource source, Matrix4x4 matrix) : base(source)
	{
		if (source.Format.ColorRepresentation != PixelColorRepresentation.Bgr)
			throw new NotSupportedException("Pixel format not supported.");

		vec0 = new Vector4(matrix.M33, matrix.M23, matrix.M13, matrix.M43);
		vec1 = new Vector4(matrix.M32, matrix.M22, matrix.M12, matrix.M42);
		vec2 = new Vector4(matrix.M31, matrix.M21, matrix.M11, matrix.M41);
		vec3 = new Vector4(        0f,         0f,         0f, matrix.M44);
	}

	protected override unsafe void CopyPixelsInternal(in PixelArea prc, int cbStride, int cbBufferSize, byte* pbBuffer)
	{
		Profiler.PauseTiming();
		PrevSource.CopyPixels(prc, cbStride, cbBufferSize, pbBuffer);
		Profiler.ResumeTiming();

		if (Format.NumericRepresentation == PixelNumericRepresentation.Float)
#if HWINTRINSICS
			if (Sse41.IsSupported)
				copyPixelsSse41(prc, cbStride, pbBuffer);
			else
#endif
				copyPixelsFloat(prc, cbStride, pbBuffer);
		else if (Format.NumericRepresentation == PixelNumericRepresentation.Fixed)
			copyPixelsFixed(prc, cbStride, pbBuffer);
		else
			copyPixelsByte(prc, cbStride, pbBuffer);
	}

	private unsafe void copyPixelsByte(in PixelArea prc, int cbStride, byte* pbBuffer)
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
			byte* ip = pbBuffer + y * cbStride, ipe = ip + prc.Width * chan;
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

	private unsafe void copyPixelsFixed(in PixelArea prc, int cbStride, byte* pbBuffer)
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
			ushort* ip = (ushort*)(pbBuffer + y * cbStride), ipe = ip + prc.Width * chan;
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

	private unsafe void copyPixelsFloat(in PixelArea prc, int cbStride, byte* pbBuffer)
	{
		int chan = Format.ChannelCount;
		bool alpha = Format.AlphaRepresentation != PixelAlphaRepresentation.None;

		Vector4 vm0 = vec0, vm1 = vec1, vm2 = vec2;
		float falpha = vec3.W, fone = Vector4.One.X;

		for (int y = 0; y < prc.Height; y++)
		{
			float* ip = (float*)(pbBuffer + y * cbStride), ipe = ip + prc.Width * chan;
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
	private unsafe void copyPixelsSse41(in PixelArea prc, int cbStride, byte* pbBuffer)
	{
		int chan = Format.ChannelCount;

		var vm0 = Unsafe.ReadUnaligned<Vector128<float>>(ref Unsafe.As<float, byte>(ref Unsafe.AsRef(in vec0.X)));
		var vm1 = Unsafe.ReadUnaligned<Vector128<float>>(ref Unsafe.As<float, byte>(ref Unsafe.AsRef(in vec1.X)));
		var vm2 = Unsafe.ReadUnaligned<Vector128<float>>(ref Unsafe.As<float, byte>(ref Unsafe.AsRef(in vec2.X)));
		var vm3 = Unsafe.ReadUnaligned<Vector128<float>>(ref Unsafe.As<float, byte>(ref Unsafe.AsRef(in vec3.X)));

		var vml0 = Sse.UnpackLow (vm0, vm1).AsDouble();
		var vmh0 = Sse.UnpackHigh(vm0, vm1).AsDouble();
		var vml1 = Sse.UnpackLow (vm2, vm3).AsDouble();
		var vmh1 = Sse.UnpackHigh(vm2, vm3).AsDouble();

		vm0 = Sse2.UnpackLow (vml0, vml1).AsSingle();
		vm1 = Sse2.UnpackHigh(vml0, vml1).AsSingle();
		vm2 = Sse2.UnpackLow (vmh0, vmh1).AsSingle();
		vm3 = Sse2.UnpackHigh(vmh0, vmh1).AsSingle();
		var vone = Vector128.Create(1f);

		for (int y = 0; y < prc.Height; y++)
		{
			float* ip = (float*)(pbBuffer + y * cbStride), ipe = ip + prc.Width * chan;

			if (Avx.IsSupported)
			{
				var wm0 = vm0.ToVector256Unsafe().WithUpper(vm0);
				var wm1 = vm1.ToVector256Unsafe().WithUpper(vm1);
				var wm2 = vm2.ToVector256Unsafe().WithUpper(vm2);
				var wm3 = vm3.ToVector256Unsafe().WithUpper(vm3);
				var wone = Vector256.Create(1f);

				ipe -= Vector256<float>.Count;
				while (ip <= ipe)
				{
					var vi = Avx.LoadVector256(ip);

					var vr0 = Avx.Multiply(Avx.Shuffle(vi, vi, HWIntrinsics.ShuffleMaskChan0), wm0);
					var vr1 = Avx.Multiply(Avx.Shuffle(vi, vi, HWIntrinsics.ShuffleMaskChan1), wm1);
					vr0 = HWIntrinsics.MultiplyAdd(vr0, Avx.Shuffle(vi, vi, HWIntrinsics.ShuffleMaskChan2), wm2);
					vr1 = HWIntrinsics.MultiplyAdd(vr1, Avx.Blend(wone, vi, HWIntrinsics.BlendMaskAlpha), wm3);
					vi = Avx.Add(vr0, vr1);

					Avx.Store(ip, vi);
					ip += Vector256<float>.Count;
				}
				ipe += Vector256<float>.Count;

				vm0 = wm0.GetLower();
				vm1 = wm1.GetLower();
				vm2 = wm2.GetLower();
				vm3 = wm3.GetLower();
				vone = wone.GetLower();
			}

			while (ip < ipe)
			{
				var vi = Sse.LoadVector128(ip);

				var vr0 = Sse.Multiply(Sse.Shuffle(vi, vi, HWIntrinsics.ShuffleMaskChan0), vm0);
				var vr1 = Sse.Multiply(Sse.Shuffle(vi, vi, HWIntrinsics.ShuffleMaskChan1), vm1);
				vr0 = HWIntrinsics.MultiplyAdd(vr0, Sse.Shuffle(vi, vi, HWIntrinsics.ShuffleMaskChan2), vm2);
				vr1 = HWIntrinsics.MultiplyAdd(vr1, Sse41.Blend(vone, vi, HWIntrinsics.BlendMaskAlpha), vm3);
				vi = Sse.Add(vr0, vr1);

				Sse.Store(ip, vi);
				ip += Vector128<float>.Count;
			}
		}
	}
#endif

	public override string ToString() => nameof(ColorMatrixTransform);
}

/// <summary>Transforms an image according to coefficients in a <see cref="Matrix4x4" />.</summary>
/// <remarks>The matrix is treated as 3x4, with the 4th row used for translation.</remarks>
/// <remarks>Constructs a new <see cref="ColorMatrixTransform" /> using the specified <paramref name="matrix" />.</remarks>
/// <param name="matrix">A 4x4 matrix of coefficients.  The channel order is RGB, column-major.</param>
public sealed class ColorMatrixTransform(Matrix4x4 matrix) : PixelTransformInternalBase
{
	private readonly Matrix4x4 matrix = matrix;

	internal override void Init(PipelineContext ctx)
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
			var fmt = matrix.M44 < 1f || ctx.Source.Format.AlphaRepresentation != PixelAlphaRepresentation.None ? PixelFormat.Bgra32 : PixelFormat.Bgr24;
			if (ctx.Source.Format != fmt)
				ctx.Source = ctx.AddProfiler(new ConversionTransform(ctx.Source, fmt));

			ctx.Source = ctx.AddProfiler(new ColorMatrixTransformInternal(ctx.Source, matrix));
		}

		Source = ctx.Source;
	}
}

// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

namespace PhotoSauce.MagicScaler;

internal static class ConversionMatrix
{
	private static readonly Vector3C iccD50 = new(0xf6d6/65536d, 1d, 0xd32d/65536d);

	private static readonly Matrix3x3C iccBradfordMatrix = new(
		 0.8951,  0.2664, -0.1614,
		-0.7502,  1.7135,  0.0367,
		 0.0389, -0.0685,  1.0296
	);

	public static Matrix3x3C GetRgbToYcc(double kr, double kb)
	{
		double kg = 1 - kr - kb;
		double kbs = (1 - kb) * 2;
		double krs = (1 - kr) * 2;

		return new(
			kr,
			-kr / kbs,
			0.5,
			kg,
			-kg / kbs,
			-kg / krs,
			kb,
			0.5,
			-kb / krs
		);
	}

	// SMPTE RP 177-1993
	public static Matrix3x3C GetRgbToXyz(Vector3C r, Vector3C g, Vector3C b, Vector3C w)
	{
		var m = Matrix3x3C.FromColumns(r, g, b);
		Matrix3x3C.Invert(m, out var im);

		r *= Vector3C.Dot(im.Row1, w);
		g *= Vector3C.Dot(im.Row2, w);
		b *= Vector3C.Dot(im.Row3, w);

		return Matrix3x3C.FromColumns(r, g, b);
	}

	// ICC v4 Annex E
	public static Matrix3x3C GetChromaticAdaptation(Vector3C srcWhite)
	{
		var cr = iccBradfordMatrix;
		var dstWhite = iccD50;

		var vbr = cr.Row1;
		var vbg = cr.Row2;
		var vbb = cr.Row3;
		var vcs = new Vector3C(Vector3C.Dot(vbr, srcWhite), Vector3C.Dot(vbg, srcWhite), Vector3C.Dot(vbb, srcWhite));
		var vcd = new Vector3C(Vector3C.Dot(vbr, dstWhite), Vector3C.Dot(vbg, dstWhite), Vector3C.Dot(vbb, dstWhite));
		var vcr = vcd / vcs;

		var cm = new Matrix3x3C(
			vcr.X, 0, 0,
			0, vcr.Y, 0,
			0, 0, vcr.Z
		);

		Matrix3x3C.Invert(cr, out var icr);

		return icr * cm * cr;
	}
}

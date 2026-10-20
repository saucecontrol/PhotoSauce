// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

using Xunit;

using static PhotoSauce.ManagedCodecs.Ico.Test.TestUtil;

namespace PhotoSauce.ManagedCodecs.Ico.Test;

// Rough decode speed per bitmap layout, for comparing changes to the pixel loops.  Set ICO_BENCH to run.
public class SpeedTests(ITestOutputHelper output)
{
	public static TheoryData<string> Layouts() => [ "8-bit", "24-bit", "32-bit", "32-bit bitfields", "16-bit 565" ];

	[Theory, MemberData(nameof(Layouts))]
	public void DecodeLayout(string layout)
	{
		if (Environment.GetEnvironmentVariable("ICO_BENCH") is null)
			Assert.Skip("Set ICO_BENCH to run.");

		EnsureCodecs();

		const int size = 256;
		var rnd = new Random(5);
		var pixels = Enumerable.Range(0, size * size).Select(_ => (uint)rnd.Next()).ToArray();
		var dib = layout switch {
			"8-bit"            => new Dib { Width = size, Height = size, Bpp = 8, Palette = new uint[256], Pixels = pixels.Select(p => p & 0xff).ToArray() },
			"24-bit"           => new Dib { Width = size, Height = size, Bpp = 24, Pixels = pixels },
			"32-bit"           => new Dib { Width = size, Height = size, Bpp = 32, Pixels = pixels },
			"32-bit bitfields" => new Dib { Width = size, Height = size, Bpp = 32, Compression = 3, Masks = [ 0xff, 0xff00, 0xff0000 ], Pixels = pixels },
			_                  => new Dib { Width = size, Height = size, Bpp = 16, Compression = 3, Masks = [ 0xf800, 0x07e0, 0x001f ], Pixels = pixels.Select(p => p & 0xffff).ToArray() },
		};

		var ico = IcoBuilder.Build(dib);
		var buff = new byte[size * size * 4];
		var times = new List<double>();
		var sw = new Stopwatch();

		// The first pass is warm-up.  The fastest pass is the least disturbed by other load on the machine.
		for (int pass = 0; pass < 11; pass++)
		{
			sw.Restart();
			for (int i = 0; i < 100; i++)
			{
				using var cnt = Load(ico);
				using var frame = cnt.GetFrame(0);
				frame.PixelSource.CopyPixels(new(0, 0, size, size), size * 4, buff);
			}

			if (pass > 0)
				times.Add(sw.Elapsed.TotalMilliseconds);
		}

		times.Sort();
		output.WriteLine($"{layout}: 100 decodes of {size}x{size}, fastest {times[0]:f1} ms, median {times[times.Count / 2]:f1} ms.");
	}
}

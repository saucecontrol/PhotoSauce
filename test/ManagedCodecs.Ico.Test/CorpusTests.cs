// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Buffers.Binary;
using System.Collections.Generic;

using Xunit;

using PhotoSauce.MagicScaler;

using static PhotoSauce.ManagedCodecs.Ico.Test.TestUtil;

namespace PhotoSauce.ManagedCodecs.Ico.Test;

// Regression check against a local collection of real-world files, which cannot be redistributed.
// ICO_CORPUS points at a folder of .ico/.cur files.  If a sibling "expected" folder mirrors it with
// <file>\frame_NN_WxH.png reference images, each ICO frame is compared with its reference.
public class CorpusTests(ITestOutputHelper output)
{
	[Fact]
	public void Corpus()
	{
		string? root = Environment.GetEnvironmentVariable("ICO_CORPUS");
		if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
			Assert.Skip("Set ICO_CORPUS to a folder of ICO/CUR files to run this test.");

		EnsureCodecs();

		root = Path.GetFullPath(root);
		root = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string expectedRoot = Path.Combine(Path.GetDirectoryName(root) ?? root, "expected");
		var failures = new List<string>();
		int files = 0, frames = 0, compared = 0, viaPipeline = 0;

		foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(static f => f.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".cur", StringComparison.OrdinalIgnoreCase)))
		{
			files++;
			string rel = file[(root.Length + 1)..];
			string refDir = Path.Combine(expectedRoot, rel);

			try
			{
				using var stream = File.OpenRead(file);
				using var cnt = IcoContainer.TryLoad(stream, null);
				if (cnt is null)
				{
					// A container with no images is declined, because other formats share the signature.
					if (isEmptyContainer(file))
						output.WriteLine($"{rel}: empty");
					else
						failures.Add($"{rel}: not recognized");

					continue;
				}

				for (int i = 0; i < cnt.FrameCount; i++)
				{
					frames++;
					using var frame = cnt.GetFrame(i);
					var actual = Decode(frame);

					if (!Directory.Exists(refDir))
						continue;

					string? refFile = Directory.EnumerateFiles(refDir, $"frame_{i + 1:00}_*.png").FirstOrDefault();
					if (refFile is null)
					{
						failures.Add($"{rel} #{i}: no reference image");
						continue;
					}

					compared++;
					bool isPng = frame.PixelSource is not BmpPixelSource;
					string? diff = compare(refFile, frame.PixelSource.Width, frame.PixelSource.Height, actual, isPng);

					// Some references were made by sending the embedded PNG through the processing pipeline, which
					// converts PNGs with gAMA/sRGB chunks to sRGB.  The decoder returns the PNG codec's raw pixels, so
					// such a frame must match once it goes through the same pipeline.
					if (diff is not null && isPng)
					{
						diff = compare(refFile, frame.PixelSource.Width, frame.PixelSource.Height, throughPipeline(file, i), isPng);
						if (diff is null)
							viaPipeline++;
					}

					if (diff is not null)
						failures.Add($"{rel} #{i}: {diff}");
				}
			}
			catch (Exception ex)
			{
				failures.Add($"{rel}: {ex.GetType().Name}: {ex.Message}");
			}
		}

		output.WriteLine($"{files} files, {frames} frames, {compared} compared with reference ({viaPipeline} PNG frames through the pipeline), {failures.Count} failures.");
		foreach (string f in failures)
			output.WriteLine(f);

		Assert.True(files > 0, "No files found.");
		Assert.Empty(failures);
	}

	// Rough decode speed for bitmap frames, for comparing changes to the pixel loops.  Files are read into
	// memory first so only decoding is timed.
	[Fact]
	public void DecodeSpeed()
	{
		string? root = Environment.GetEnvironmentVariable("ICO_CORPUS");
		if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
			Assert.Skip("Set ICO_CORPUS to a folder of ICO/CUR files to run this test.");

		EnsureCodecs();

		var files = Directory.EnumerateFiles(root, "*.ico", SearchOption.AllDirectories).Select(File.ReadAllBytes).ToList();
		int frames = 0, pixels = 0;
		var sw = new System.Diagnostics.Stopwatch();
		var times = new List<double>();

		// The first pass is warm-up.  The fastest pass is the least disturbed by other load on the machine.
		for (int pass = 0; pass < 11; pass++)
		{
			sw.Restart();

			foreach (var bytes in files)
			{
				IcoContainer? cnt;
				try
				{
					cnt = IcoContainer.TryLoad(new MemoryStream(bytes), null);
				}
				catch (InvalidDataException)
				{
					continue;
				}

				if (cnt is null)
					continue;

				using (cnt)
				{
					for (int i = 0; i < cnt.FrameCount; i++)
					{
						using var frame = cnt.GetFrame(i);
						if (frame.PixelSource is not BmpPixelSource src)
							continue;

						_ = Decode(src);
						if (pass == 0)
						{
							frames++;
							pixels += src.Width * src.Height;
						}
					}
				}
			}

			if (pass > 0)
				times.Add(sw.Elapsed.TotalMilliseconds);
		}

		times.Sort();
		output.WriteLine($"{frames} bitmap frames, {pixels} pixels per pass: fastest {times[0]:f1} ms, median {times[times.Count / 2]:f1} ms (10 passes after warm-up).");
	}

	private static bool isEmptyContainer(string file)
	{
		var header = File.ReadAllBytes(file).AsSpan();
		return header.Length >= 6 && header[0] == 0 && header[1] == 0 && header[2] is 1 or 2 && header[3] == 0 && header[4] == 0 && header[5] == 0;
	}

	private static byte[] throughPipeline(string file, int index)
	{
		var settings = new ProcessImageSettings { DecoderOptions = new IcoDecoderOptions(index..(index + 1)) };
		using var pipeline = MagicImageProcessor.BuildPipeline(file, settings);

		return Decode(pipeline.PixelSource);
	}

	// Bitmap frames must match exactly.  PNG frame references went through color conversion and alpha
	// premultiplication, so they may differ by rounding: one premultiplied step (255 / alpha) per color
	// channel, and 1 in alpha.
	private static string? compare(string refFile, int width, int height, byte[] actual, bool rounding)
	{
		string name = Path.GetFileNameWithoutExtension(refFile);
		string size = name[(name.LastIndexOf('_') + 1)..];
		if (size != $"{width}x{height}")
			return $"size {width}x{height}, reference {size}";

		// Decode the reference PNG through the same path as an embedded PNG frame, which yields raw pixels.
		var png = File.ReadAllBytes(refFile);
		using var cnt = Load(IcoBuilder.Build((png, width, height)));
		using var frame = cnt.GetFrame(0);
		var expected = Decode(frame);

		int diffs = 0;
		string? first = null;
		for (int i = 0; i < actual.Length; i += 4)
		{
			// Fully transparent pixels may carry any color.
			if (actual[i + 3] == 0 && expected[i + 3] == 0)
				continue;

			int alpha = Math.Max((int)expected[i + 3], 1);
			int colorTolerance = rounding ? Math.Max(1, (255 + alpha - 1) / alpha) : 0;
			int alphaTolerance = rounding ? 1 : 0;

			bool same = Math.Abs(actual[i + 3] - expected[i + 3]) <= alphaTolerance;
			for (int c = 0; c < 3; c++)
				same &= Math.Abs(actual[i + c] - expected[i + c]) <= colorTolerance;

			if (!same)
			{
				uint a = BinaryPrimitives.ReadUInt32LittleEndian(actual.AsSpan(i)), e = BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(i));
				diffs++;
				first ??= $"pixel {i / 4} actual 0x{a:x8} expected 0x{e:x8}";
			}
		}

		return diffs == 0 ? null : $"{diffs} pixels differ, first {first}";
	}
}

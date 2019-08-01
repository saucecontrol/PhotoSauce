using System;

namespace PhotoSauce.MagicScaler
{
	internal interface IBlake2Incremental
	{
		int DigestLength { get; }
		void Update(ReadOnlySpan<byte> input);
		void Update(Span<byte> input);
		void Update<T>(ReadOnlySpan<T> input) where T : struct;
		void Update<T>(T input) where T : struct;
		byte[] Finish();
		bool TryFinish(Span<byte> output, out int bytesWritten);
	}

	internal static class Blake2b
	{
		public const int DefaultDigestLength = Blake2bContext.HashBytes;

		public static byte[] ComputeHash(ReadOnlySpan<byte> input) => ComputeHash(DefaultDigestLength, default, input);

		public static byte[] ComputeHash(int digestLength, ReadOnlySpan<byte> input) => ComputeHash(digestLength, default, input);

		public static byte[] ComputeHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input) => ComputeHash(DefaultDigestLength, key, input);

		public static byte[] ComputeHash(int digestLength, ReadOnlySpan<byte> key, ReadOnlySpan<byte> input)
		{
			var ctx = default(Blake2bContext);
			ctx.Init(digestLength, key);
			ctx.Update(input);
			return ctx.Finish();
		}

		public static void ComputeAndWriteHash(ReadOnlySpan<byte> input, Span<byte> output) => ComputeAndWriteHash(DefaultDigestLength, default, input, output);

		public static void ComputeAndWriteHash(int digestLength, ReadOnlySpan<byte> input, Span<byte> output) => ComputeAndWriteHash(digestLength, default, input, output);

		public static void ComputeAndWriteHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input, Span<byte> output) => ComputeAndWriteHash(DefaultDigestLength, key, input, output);

		public static void ComputeAndWriteHash(int digestLength, ReadOnlySpan<byte> key, ReadOnlySpan<byte> input, Span<byte> output)
		{
			if (output.Length < digestLength)
				throw new ArgumentException($"Output buffer must have a capacity of at least {digestLength} bytes.", nameof(output));

			var ctx = default(Blake2bContext);
			ctx.Init(digestLength, key);
			ctx.Update(input);
			ctx.TryFinish(output, out int _);
		}

		public static IBlake2Incremental CreateIncrementalHasher() => CreateIncrementalHasher(DefaultDigestLength, default);

		public static IBlake2Incremental CreateIncrementalHasher(int digestLength) => CreateIncrementalHasher(digestLength, default);

		public static IBlake2Incremental CreateIncrementalHasher(ReadOnlySpan<byte> key) => CreateIncrementalHasher(DefaultDigestLength, key);

		public static IBlake2Incremental CreateIncrementalHasher(int digestLength, ReadOnlySpan<byte> key)
		{
			var ctx = default(Blake2bContext);
			ctx.Init(digestLength, key);
			return ctx;
		}
	}
}

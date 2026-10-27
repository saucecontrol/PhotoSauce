PhotoSauce.NativeCodecs.JxlRs
=============================

This MagicScaler plugin decodes [JPEG XL](https://jpeg.org/jpegxl/) images with the pure-Rust [jxl-rs](https://github.com/libjxl/jxl-rs) decoder.

The project builds a small native bridge with Cargo and copies the resulting platform library next to the managed assembly. Rust and Cargo must be available when building from source. The current source build produces a native library for the host platform and architecture.

Contiguous `MemoryStream`, `UnmanagedMemoryStream`, and `ReadOnlySpan<byte>` inputs are read in place. Other seekable `Stream` implementations are consumed incrementally through a bounded native buffer, without copying the complete encoded input. Opening a still JPEG XL image exposes only bounded metadata chunks, stops after image/ICC metadata, and retains the primed decoder state for the first `CopyPixels` call. Animated images additionally scan visible frame locations but still defer pixel rendering. The first full-frame request decodes BGR/BGRA pixels directly into the caller's buffer. Partial or repeated requests decode into one pooled frame cache that is reused for subsequent reads from that frame. Animation frames are decoded on demand using jxl-rs frame seek targets rather than being retained simultaneously.

Decoding is single-threaded by default. Set <code>ProcessImageSettings.DecoderOptions</code> to a <code>JxlRsDecoderOptions</code> with <code>MaxDegreeOfParallelism</code> greater than 1 to cap the number of workers used by one image, or use 0 to let the shared worker pool select the available processor count automatically. For example, <code>new JxlRsDecoderOptions(Range.All, 8)</code> allows up to eight workers.

Usage
-----

Register the decoder at application startup:

```C#
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.JxlRs;

CodecManager.Configure(codecs => {
    codecs.UseJxlRs();
});
```

Once registered, MagicScaler automatically detects and decodes JPEG XL codestreams and container files. Still images, animation frames, orientation, output ICC profiles, and JPEG XL `Exif` boxes are supported. EXIF bytes are read from the source stream only when requested; they are not cached by the plugin. On .NET 8 or later, `Exif` boxes wrapped in Brotli-compressed `brob` boxes are decoded incrementally with bounded buffers. The .NET Framework 4.7.2 target exposes uncompressed `Exif` boxes only.

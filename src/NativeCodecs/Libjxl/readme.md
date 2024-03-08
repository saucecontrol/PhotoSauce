PhotoSauce.NativeCodecs.Libjxl
==============================

This MagicScaler plugin wraps the [libjxl](https://github.com/libjxl/libjxl) reference [JPEG XL](https://jpeg.org/jpegxl/) codec.

*IMPORTANT*: `libjxl` is a preview release and may not be fully stable.  In particular, it may crash your process on out of memory conditions.  See: https://github.com/libjxl/libjxl/issues/1450.

Requirements
------------

A compatible `libjxl` binary must be present for this plugin to function.  For convenience, the NuGet package includes native binaries for Windows (x86, x64, and ARM64) and Linux (glibc x64 and ARM64).

Usage
-----

### Codec Registration

To register the codec, call the `UseLibjxl` extension method from your `CodecManager.Configure` action at app startup.

```C#
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libjxl;

CodecManager.Configure(codecs => {
    codecs.UseLibjxl();
});
```

### Using the Codec

Once registered, the codec will automatically detect and decode compatible images.

To encode JPEG XL images, the encoder MIME type can be set on `ProcessImageSettings`:

```C#
var settings = new ProcessImageSettings();
settings.TrySetEncoderFormat(ImageMimeTypes.Jxl)
```

Or the encoder can be selected by file extension on overloads accepting file paths:

```C#
MagicImageProcessor.ProcessImage(@"\img\input.jpg", @"\img\output.jxl", settings);
```

PhotoSauce.NativeCodecs.Libjxl
==============================

This MagicScaler plugin wraps the [libjxl](https://github.com/libjxl/libjxl) reference [JPEG XL](https://jpeg.org/jpegxl/) codec.

*IMPORTANT: Version 0.6.1 of `libjxl` is a preview release and is not fully stable.  Some combinations of encoder settings may lead to access violations, infinte loops, or other catastrophic failures.  Use with caution.*

Requirements
------------

A compatible `libjxl` binary must be present for this plugin to function.  For convenience, the NuGet package includes native binaries for Windows 10+ (x86, x64, and ARM64) and Ubuntu 20.04 (x64 and ARM64).

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

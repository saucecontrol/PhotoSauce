PhotoSauce.NativeCodecs.Giflib
==============================

This MagicScaler plugin wraps the [GIFLIB](https://giflib.sourceforge.net/) native GIF codec.

Windows includes a GIF codec that is auto-discoverable by the MagicScaler pipeline.  This plugin adds GIF support on non-Windows platforms.

Requirements
------------

A compatible native binary must be present for this plugin to function.  For convenience, the NuGet package includes native binaries for Windows 10+ (x86, x64, and ARM64) and Ubuntu 20.04 (x64 and ARM64).

Usage
-----

### Codec Registration

To register the codec, call the `UseGiflib` extension method from your `CodecManager.Configure` action at app startup.  By default, the plugin will remove/replace the Windows GIF codec if it is present.

```C#
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Giflib;

CodecManager.Configure(codecs => {
    codecs.UseGiflib();
});
```

### Using the Codec

Once registered, the codec will automatically detect and decode compatible images.

To encode GIF images, the encoder MIME type can be set on `ProcessImageSettings`:

```C#
var settings = new ProcessImageSettings();
settings.TrySetEncoderFormat(ImageMimeTypes.Gif)
```

Or the encoder can be selected by file extension on overloads accepting file paths:

```C#
MagicImageProcessor.ProcessImage(@"\img\input.jpeg", @"\img\output.gif", settings);
```

PhotoSauce.NativeCodecs.Libpng
==============================

This MagicScaler plugin wraps the [libpng](http://www.libpng.org/pub/png/libpng.html) native PNG codec.

Windows includes a PNG codec that is auto-discoverable by the MagicScaler pipeline.  This plugin adds PNG support on non-Windows platforms or may be used on Windows for improved capabilities.

Requirements
------------

A compatible native binary must be present for this plugin to function.  For convenience, the NuGet package includes native binaries for Windows 10+ (x86, x64, and ARM64) and Ubuntu 20.04 (x64 and ARM64).

Standard releases of `libpng` are not usable reliably from .NET; a custom build is required.  A [vcpkg](https://github.com/microsoft/vcpkg) port containing the customizations is located in the [PhotoSauce GitHub repo](https://github.com/saucecontrol/PhotoSauce/tree/master/build/vcpkg/ports/pspng) if you need to build for a platform not included in this package.

Usage
-----

### Codec Registration

To register the codec, call the `UseLibpng` extension method from your `CodecManager.Configure` action at app startup.  By default, the plugin will remove/replace the Windows PNG codec if it is present.

```C#
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libpng;

CodecManager.Configure(codecs => {
    codecs.UseLibpng();
});
```

### Using the Codec

Once registered, the codec will automatically detect and decode compatible images.

To encode PNG images, the encoder MIME type can be set on `ProcessImageSettings`:

```C#
var settings = new ProcessImageSettings();
settings.TrySetEncoderFormat(ImageMimeTypes.Png)
```

Or the encoder can be selected by file extension on overloads accepting file paths:

```C#
MagicImageProcessor.ProcessImage(@"\img\input.jpeg", @"\img\output.png", settings);
```

### APNG Support

[APNG](https://en.wikipedia.org/wiki/APNG) decode is enabled by default in this codec, but encode is currently disabled due to an intermittent memory corruption bug in libpng.

You can enable APNG encoding for testing purposes by setting the `PhotoSauce.NativeCodecs.Libpng.EnableApngEncode` switch at app startup using [AppContext](https://learn.microsoft.com/en-us/dotnet/api/system.appcontext.setswitch) or [runtimeconfig.json](https://docs.microsoft.com/en-us/dotnet/core/runtime-config/).

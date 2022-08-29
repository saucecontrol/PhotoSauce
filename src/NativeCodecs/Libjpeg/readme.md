PhotoSauce.NativeCodecs.Libjpeg
===============================

This MagicScaler plugin wraps the [libjpeg-turbo](https://libjpeg-turbo.org/) native JPEG codec.

Windows includes a JPEG codec that is auto-discoverable by the MagicScaler pipeline.  This plugin adds JPEG support on non-Windows platforms or may be used on Windows for improved performance and capabilities.

Requirements
------------

A compatible native binary must be present for this plugin to function.  For convenience, the NuGet package includes native binaries for Windows 10+ (x86, x64, and ARM64) and Ubuntu 20.04 (x64 and ARM64).

Standard releases of `libjpeg` and `libjpeg-turbo` are not usable reliably from .NET; a custom build is required.  A [vcpkg](https://github.com/microsoft/vcpkg) port containing the customizations is located in the [PhotoSauce GitHub repo](https://github.com/saucecontrol/PhotoSauce/tree/master/build/vcpkg/ports/psjpeg) if you need to build for a platform not included in this package.

Usage
-----

### Codec Registration

To register the codec, call the `UseLibjpeg` extension method from your `CodecManager.Configure` action at app startup.  By default, the plugin will remove/replace the Windows JPEG codec if it is present.

```C#
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libjpeg;

CodecManager.Configure(codecs => {
    codecs.UseLibjpeg();
});
```

### Using the Codec

Once registered, the codec will automatically detect and decode compatible images.

To encode JPEG images, the encoder MIME type can be set on `ProcessImageSettings`:

```C#
var settings = new ProcessImageSettings();
settings.TrySetEncoderFormat(ImageMimeTypes.Jpeg)
```

Or the encoder can be selected by file extension on overloads accepting file paths:

```C#
MagicImageProcessor.ProcessImage(@"\img\input.png", @"\img\output.jpg", settings);
```

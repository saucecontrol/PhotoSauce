PhotoSauce.NativeCodecs.Libwebp
===============================

This MagicScaler plugin wraps the [libwebp](https://chromium.googlesource.com/webm/libwebp) reference [WebP](https://developers.google.com/speed/webp) codec.

Requirements
------------

A compatible set of `libwebp` binaries must be present for this plugin to function.  For convenience, the NuGet package includes native binaries for Windows 10+ (x86, x64, and ARM64) and Ubuntu 20.04 (x64 and ARM64).

Usage
-----

### Codec Registration

To register the codec, call the `UseLibwebp` extension method from your `CodecManager.Configure` action at app startup.

```C#
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libwebp;

CodecManager.Configure(codecs => {
    codecs.UseLibwebp();
});
```

### Using the Codec

Once registered, the codec will automatically detect and decode compatible images.

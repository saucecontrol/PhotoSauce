PhotoSauce.NativeCodecs.Libraw
==============================

This MagicScaler plugin wraps [LibRaw](https://www.libraw.org/) to decode camera RAW images.

Requirements
------------

A compatible LibRaw binary and the PhotoSauce native wrapper must be present for this plugin to function. For convenience, the NuGet package includes native binaries for Windows (x86, x64, and ARM64) and Linux (glibc x64 and ARM64).

Usage
-----

Register the codec at application startup:

```C#
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libraw;

CodecManager.Configure(codecs => {
    codecs.UseLibraw();
});
```

The default decoder uses camera white balance, LibRaw auto-brightness, and full-resolution demosaicing. Supply `LibrawDecoderOptions` to customize those choices.

LibRaw runs in the application process. Applications that accept untrusted RAW files should consider performing image decoding in an isolated worker process.

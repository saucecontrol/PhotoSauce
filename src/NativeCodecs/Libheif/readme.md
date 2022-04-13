PhotoSauce.NativeCodecs.Libheif
===============================

This MagicScaler plugin wraps the [libheif](https://github.com/strukturag/libheif) codec for [HEIC](https://en.wikipedia.org/wiki/High_Efficiency_Image_File_Format) images, such as those created by Apple devices running iOS 11 or later.  This plugin supports only decode, using the [libde265](https://github.com/strukturag/libde265) HEVC decoder.

Be aware that the HEVC video coding standard used in HEIC images is burdened by [numerous patents](https://en.wikipedia.org/wiki/High_Efficiency_Video_Coding#Patent_licensing) which may vary in validity by country.  

*By using this plugin, you acknowledge that your use of the codec is permitted royalty free or that you have purchased an appropriate license.*

Windows 10 and 11 include a HEIF codec by default, but it may not function for HEIC images without purchase of an HEVC extension from the Microsoft Store, and it may not function for all users or apps.  You may need to unregister the Windows HEIF codec in order to use this plugin for decode.

Requirements
------------

A compatible `libheif` binary must be present for this plugin to function.  For convenience, the NuGet package includes native binaries for Windows 10+ (x86, x64, and ARM64) and Ubuntu 20.04 (x64 and ARM64).

Usage
-----

### Codec Registration

To register the codec, call the `UseLibheif` extension method from your `CodecManager.Configure` action at app startup.  This example removes the default Windows HEIF codec before installing the plugin.

```C#
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libheif;

CodecManager.Configure(codecs => {
    var wicheif = codecs.OfType<IImageDecoderInfo>().FirstOrDefault(c => c.MimeTypes.Any(m => m == ImageMimeTypes.Heic));
    if (wicheif != null)
        codecs.Remove(wicheif);

    codecs.UseLibheif();
});
```

### Using the Codec

Once registered, the codec will automatically detect and decode compatible images.

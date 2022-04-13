PhotoSauce.ManagedCodecs.ImageSharp
===================================

This MagicScaler plugin sample makes the [ImageSharp](https://github.com/SixLabors/ImageSharp) [TARGA](https://en.wikipedia.org/wiki/Truevision_TGA) codec available for decode and encode.

This sample presents the minimum implementation required to decode and encode for a codec with no metadata support.

Usage
-----

### Codec Registration

To register the codec, call the `UseImageSharpTga` extension method from your `CodecManager.Configure` action at app startup.

```C#
using PhotoSauce.MagicScaler;
using PhotoSauce.ManagedCodecs.ImageSharp;

CodecManager.Configure(codecs => {
    codecs.UseImageSharpTga();
});
```

### Using the Codec

Once registered, the codec will automatically detect and decode compatible images.

To encode TGA images, the encoder MIME type can be set on `ProcessImageSettings`:

```C#
var settings = new ProcessImageSettings();
settings.TrySetEncoderFormat("image/tga")
```

Or the encoder can be selected by file extension on overloads accepting file paths:

```C#
MagicImageProcessor.ProcessImage(@"\img\input.jpg", @"\img\output.tga", settings);
```

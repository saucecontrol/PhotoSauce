PhotoSauce.ManagedCodecs.Ico
============================

This MagicScaler plugin is a fully-managed decoder for [ICO and CUR](https://en.wikipedia.org/wiki/ICO_(file_format)) files.  It has no native dependencies and works on all platforms.

Windows includes an ICO codec that is auto-discoverable by the MagicScaler pipeline, but it is not available on other platforms.  This plugin decodes bitmap frames the same way everywhere (PNG frames are left to the registered PNG codec), also reads CUR files, handles the AND mask and alpha channel consistently, and decodes truncated files as far as their data goes.  It exposes every image in the file as a frame.

Usage
-----

### Codec Registration

To register the codec, call the `UseIco` extension method from your `CodecManager.Configure` action at app startup.  By default, the plugin will remove/replace the Windows ICO codec if it is present.

```C#
using PhotoSauce.MagicScaler;
using PhotoSauce.ManagedCodecs.Ico;

CodecManager.Configure(codecs => {
    codecs.UseIco();
});
```

Frames stored as PNG are decoded by whichever PNG codec is registered, so register one as well on platforms other than Windows (for example, `UseLibpng` from `PhotoSauce.NativeCodecs.Libpng`).

### Using the Codec

Once registered, the codec will automatically detect and decode compatible images.

An ICO file usually holds the same picture at several sizes and color depths.  Use `ImageFileInfo` to list them, and `IcoDecoderOptions` to choose one:

```C#
var info = ImageFileInfo.Load(@"\img\input.ico");
int largest = info.Frames.Select((f, i) => (Size: f.Width * f.Height, Index: i)).Max().Index;

var settings = new ProcessImageSettings { DecoderOptions = new IcoDecoderOptions(largest..(largest + 1)) };
MagicImageProcessor.ProcessImage(@"\img\input.ico", @"\img\output.png", settings);
```

### Supported Formats

* ICO (type 1) and CUR (type 2) containers.  The cursor hotspot is ignored.
* Frames stored as PNG.
* Frames stored as bitmaps with a `BITMAPINFOHEADER` or later header, at 1, 4, 8, 16, 24 or 32 bits per pixel, uncompressed or with `BI_BITFIELDS` or `BI_ALPHABITFIELDS` channel masks, bottom-up or top-down.

All bitmap frames decode to BGRA.  Transparency comes from the alpha channel of 32-bit frames and of 16-bit frames with an alpha mask, or from the 1-bit AND mask otherwise.  A frame whose alpha channel is entirely zero uses the AND mask as well.  Mask pixels that would invert the screen decode as transparent.

Truncated files decode as far as their data goes:

* A bitmap row with no color data decodes as transparent black.
* A bitmap row with color data but no AND mask is opaque, unless the frame takes its alpha from its alpha channel, in which case the row keeps that alpha.
* A frame with no data at all, or a bitmap frame whose header is cut off, takes its size from the ICO directory and decodes as transparent black.  So does a PNG frame that the PNG codec cannot open and whose chunks are intact up to where the file ends, before `IEND`.
* A truncated PNG frame that the PNG codec can open decodes as far as that codec allows.  With `PhotoSauce.NativeCodecs.Libpng`, rows it cannot decode are cleared: black, and transparent if the PNG has an alpha channel.  The Windows PNG codec throws when it reaches the missing data.

A damaged bitmap header throws `InvalidDataException`.  So does a PNG frame that the PNG codec cannot open, unless it is truncated as described above; the codec's own exception is the inner exception.  A PNG counts as damaged if its first chunk is not a 13-byte `IHDR`, or if any chunk has a length over the PNG limit or a type that isn't four letters.  A corrupt but plausible length in a later chunk before the image data can't be told apart from truncation, so such a frame decodes as transparent black.  Errors reading the stream propagate as they are, as do failures to load the PNG codec itself: a missing native library, a missing type or member, or a type that fails to initialize.  A PNG codec's own version check is reported like damaged data, with its exception as the inner exception (libpng reports a mismatched native library this way).

### Limitations

* There is no encoder.
* RLE-compressed and 2-bit bitmaps are not supported.  They are not valid in ICO files, and none are known in the wild.
* Bitmap frames are limited to 4096 pixels in each dimension.
* A PNG frame that is an animated PNG decodes to the image the PNG codec returns as its first frame.
* The ICO signature is shared with uncompressed TGA files, so the decoder leaves two kinds of input to other codecs, which are then reported as an unsupported format if none accepts them: a file with no images, and a file whose first image would start inside its directory.

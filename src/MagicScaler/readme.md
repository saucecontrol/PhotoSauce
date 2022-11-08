PhotoSauce.MagicScaler
======================

MagicScaler is a high-performance image processing pipeline for .NET, focused on making complex imaging tasks simple.

It implements best-of-breed algorithms, linear light processing, and sharpening for the best image resizing quality available.

Speed and efficiency are unmatched by anything else on the .NET platform.

Requirements
------------

MagicScaler currently has full functionality only on Windows.

Work is in progress to reach full feature parity on Linux.  A growing collection of cross-platform codecs is available on [nuget.org](https://www.nuget.org/packages?q=photosauce.nativecodecs), and at this point most use cases are covered.  Notable exceptions are support for BMP and TIFF images and CMYK JPEG.

Usage
-----

### Image Resizing

```C#
MagicImageProcessor.ProcessImage(@"\img\big.jpg", @"\img\small.jpg", new ProcessImageSettings { Width = 400 });
```

The above example will resize `big.jpg` to a width of 400 pixels and save the output to	`small.jpg`.  The height will be set automatically to preserve the correct aspect ratio.  Default settings are optimized for a balance of speed and image quality.

The MagicScaler pipleline is also customizable if you wish to use an alternate pixel source, capture the output pixels for additional processing, or add custom filtering.

See the [full documentation](https://docs.photosauce.net/api/PhotoSauce.MagicScaler.MagicImageProcessor.html) for more details.

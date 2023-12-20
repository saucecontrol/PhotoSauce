PhotoSauce.MagicScaler
======================

MagicScaler is a high-performance image processing pipeline for .NET, focused on making complex imaging tasks simple.

It implements best-of-breed algorithms, linear light processing, and sharpening for the best image resizing quality available.

Speed and efficiency are unmatched by anything else on the .NET platform.

Requirements
------------

MagicScaler runs on Windows and Linux.

Linux hosting requires one or more of the cross-platform codec plugins available on [nuget.org](https://www.nuget.org/packages?q=photosauce.nativecodecs).  Most common image formats are supported.  Notable exceptions are support for BMP and TIFF images.

Usage
-----

### Image Resizing

```C#
MagicImageProcessor.ProcessImage(@"\img\big.jpg", @"\img\small.jpg", new ProcessImageSettings { Width = 400 });
```

The above example will resize `big.jpg` to a width of 400 pixels and save the output to	`small.jpg`.  The height will be set automatically to preserve the correct aspect ratio.  Default settings are optimized for a balance of speed and image quality.

The MagicScaler pipleline is also customizable if you wish to use an alternate pixel source, capture the output pixels for additional processing, or add custom filtering.

See the [full documentation](https://docs.photosauce.net/api/PhotoSauce.MagicScaler.MagicImageProcessor.html) for more details.

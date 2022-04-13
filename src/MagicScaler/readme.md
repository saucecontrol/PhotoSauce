PhotoSauce.MagicScaler
======================

MagicScaler is a high-performance image processing pipeline for .NET, focused on making complex imaging tasks simple.

It implements best-of-breed algorithms, linear light processing, and sharpening for the best image resizing quality available.

Speed and efficiency are unmatched by anything else on the .NET platform.

Requirements
------------

MagicScaler currently has full functionality only on Windows.  Although MagicScaler is compatible with -- and optimized for -- .NET Core and .NET 5+, it requires the [Windows Imaging Component](https://docs.microsoft.com/en-us/windows/desktop/wic/-wic-about-windows-imaging-codec) for its image codec support.

Work is in progress to reach full feature parity on Linux.

Usage
-----

### Image Resizing

```C#
MagicImageProcessor.ProcessImage(@"\img\big.jpg", @"\img\small.jpg", new ProcessImageSettings { Width = 400 });
```

The above example will resize `big.jpg` to a width of 400 pixels and save the output to	`small.jpg`.  The height will be set automatically to preserve the correct aspect ratio.  Default settings are optimized for a balance of speed and image quality.

The MagicScaler pipleline is also customizable if you wish to use an alternate pixel source, capture the output pixels for additional processing, or add custom filtering.

See the [full documentation](https://docs.photosauce.net) for more details.

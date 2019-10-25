[![NuGet](https://buildstats.info/nuget/PhotoSauce.MagicScaler)](https://www.nuget.org/packages/PhotoSauce.MagicScaler/) [![Build Status](https://dev.azure.com/saucecontrol/PhotoSauce/_apis/build/status/saucecontrol.PhotoSauce?branchName=master)](https://dev.azure.com/saucecontrol/PhotoSauce/_build/latest?definitionId=1&branchName=master) [![CI NuGet](https://img.shields.io/badge/NuGet-CI%20Feed-brightgreen?logo=azure-devops)](https://dev.azure.com/saucecontrol/PhotoSauce/_packaging?_a=feed&feed=dailies)

MagicScaler
===========

High-performance image processing pipeline for .NET.  Implements best-of-breed algorithms, linear light processing, and sharpening for the best image resizing quality available.  Speed and efficiency are unmatched by anything else on the .NET platform.

Benchmark results from the tests used in https://blogs.msdn.microsoft.com/dotnet/2017/01/19/net-core-image-processing/ updated to use current (Feb 2019) versions of the libraries.  Benchmark code is [here](https://github.com/saucecontrol/core-imaging-playground).

``` ini

BenchmarkDotNet=v0.10.14, OS=Windows 10.0.17134
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.0.100-preview-010184
  [Host]     : .NET Core 2.1.7 (CoreCLR 4.6.27129.04, CoreFX 4.6.27129.04), 64bit RyuJIT
  DefaultJob : .NET Core 2.1.7 (CoreCLR 4.6.27129.04, CoreFX 4.6.27129.04), 64bit RyuJIT


```

```
|                              Method |      Mean |     Error |    StdDev | Scaled |     Gen 0 |    Gen 1 |  Allocated |
|------------------------------------ |----------:|----------:|----------:|-------:|----------:|---------:|-----------:|
|      MagicScaler Load, Resize, Save |  91.09 ms | 0.5992 ms | 0.5312 ms |   0.22 |   62.5000 |        - |  342.16 KB |
|   System.Drawing Load, Resize, Save | 407.89 ms | 1.2496 ms | 1.1688 ms |   1.00 |         - |        - |   79.21 KB |
|       ImageSharp Load, Resize, Save | 228.25 ms | 0.5695 ms | 0.5328 ms |   0.56 |  250.0000 |        - | 1203.47 KB |
|      ImageMagick Load, Resize, Save | 436.11 ms | 1.6663 ms | 1.5586 ms |   1.07 |         - |        - |   54.17 KB |
|        ImageFree Load, Resize, Save | 336.68 ms | 1.1203 ms | 1.0479 ms |   0.83 | 6000.0000 | 625.0000 |   90.62 KB |
| SkiaSharp Canvas Load, Resize, Save | 164.30 ms | 0.8212 ms | 0.6857 ms |   0.40 |  937.5000 |        - | 3995.12 KB |
| SkiaSharp Bitmap Load, Resize, Save | 195.72 ms | 1.4354 ms | 1.3427 ms |   0.48 |  937.5000 |        - | 3972.68 KB |
```

Note that the image output is not the same between the tested libraries.  Not only is MagicScaler significantly faster, it also produces dramatically higher quality images.

Requirements
------------

Windows 7* or later with .NET 4.6+ or .NET Core 1.0+.  Although MagicScaler is compatible with (and optimized for) .NET Core, it requires the [Windows Imaging Component](https://docs.microsoft.com/en-us/windows/desktop/wic/-wic-about-windows-imaging-codec) to function.  There is currently no compatibility for Linux, OS X, or other supported .NET Core platforms.

*Windows 7 and Windows Sever 2008 R2 are supported only with the [Platform Update](https://support.microsoft.com/en-us/kb/2670838) installed.

Installation
------------

MagicScaler is available on [nuget](http://www.nuget.org/packages/PhotoSauce.MagicScaler/)

```
PM> Install-Package PhotoSauce.MagicScaler
```

Usage
-----

Basic usage looks something like this:

```C#
string inPath = @"c:\img\bigimage.jpg";
string outPath = @"c:\img\smallimage.jpg";
var settings = new ProcessImageSettings { Width = 400 };

using (var outStream = new FileStream(outPath, FileMode.Create))
{
  MagicImageProcessor.ProcessImage(inPath, outStream, settings);
}
``` 

The above example will resize `bigimage.jpg` to a width of 400 pixels and save the output to	 `smallimage.jpg`.  The height will be set automatically to preserve the correct aspect ratio.  Default settings are optimized for a balance of speed and image quality.

The MagicScaler pipleline is also customizable if you wish to use an alternate pixel source, capture the output pixels for additional processing, or add custom filtering.

See the [documentation page](doc/main.md) for more details.


WebRSize
========

Secure, Scalable Image Resizing for ASP.NET and IIS.

Requirements
------------

IIS 7+ with ASP.NET 4.6+.  The host App Pool must be run in Integrated Pipeline Mode.

Installation
------------

WebRSize is available on [nuget](http://www.nuget.org/packages/PhotoSauce.WebRSize/)

```
PM> Install-Package PhotoSauce.WebRSize
```

Usage
-----

WebRSize must be configured in your application's web.config file.  It will not activate without a valid configuration.

See the [documentation page](doc/web.md) for more details.


Versioning
----------

This project is using [semantic versioning](http://semver.org/).  Releases without an alpha/beta/RC tag are considered release quality and are safe for production use. The major version number will remain at 0, however, until the APIs are complete and stabilized.

Contributing
------------

Contributions are welcome, but please open a new issue for discussion before submitting any significant pull requests.  This will hopefully save any wasted or duplicate efforts.

License
-------

PhotoSauce is licensed under the [MIT](license) license.

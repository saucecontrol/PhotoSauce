MagicScaler
===========

MagicScaler brings high-performance, high-quality image scaling to .NET.

Requirements
------------

Windows 7* or later with .NET 4.6+ or .NET Core 1.0+

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
using (var outStream = new FileStream(@"c:\smallimage.jpg", FileMode.Create))
  MagicImageProcessor.ProcessImage(@"c:\bigimage.jpg", outStream, new ProcessImageSettings { Width = 400 });
``` 

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


Release History
---------------
#### MagicScaler 0.8.0.0
NOTE: This version contains breaking changes to the API.  While your code will most likely not require changes, you will have to rebuild when upgrading.

* Changed parameter names on public methods to be more descriptive.
* Changed ProcessImage() overloads that accepted byte[] to accept ArraySegment&lt;byte&gt;.
* Added metadata support (including Exif auto-rotation) to the .NET Core version.
* Added ImageFileInfo class to expose basic information read from image headers.
* Added IPixelSource interface to allow clients to feed pixels into the pipeline from custom sources.
* Added IPixelTransform class to allow custom filtering.
* Added ProcessingPipeline class to allow clients to request pixels from the pipeline without saving directly to an image file.
* Added ProcessImageResults class to expose calculated settings used and basic instrumentation.
* Added sample IPixelSource and IPixelTransform implementations.
* Improved fixed-point math accuracy for non-SIMD implementation.
* Improved RGBA performance in SIMD implementation.
* Improved Auto output format logic to match WebRSize.
* Fixed invalid crop values when using Hybrid scaling.
* Fixed invalid crop offsets when using Planar mode.
* UnsharpMaskSettings no longer overrides the Sharpen setting.  If Sharpen is false, there will be no auto-sharpening regardless of UnsharpMaskSettings.

#### WebRSize 0.3.0.0
NOTE: Cache file naming has changed in this version.  You should empty your WebRSize disk cache when upgrading.

* Changed cache file name generator to use the correct file extension when transcoding to a different format.
* Fixed a bug in the cache file name generator that caused duplicate cache files.
* Improved speed and reduced allocations in the HTTP intercept module.

See the [releases page](https://github.com/saucecontrol/PhotoSauce/releases) for previous updates.

Versioning
----------

This project is using [semantic versioning](http://semver.org/).  Releases without an alpha/beta/RC tag are considered release quality and are safe for production use. The major version number will remain at 0, however, until the APIs are complete and stabilized.  You can expect significant API changes coming in MagicScaler 0.8.

Contributing
------------

Because this project is still under active design and development, I am not accepting unsolicited pull requests at this time.  If you find a bug or would like to see a new feature implemented, please open a new issue for further discussion.  This will hopefully save any wasted or duplicate efforts.  If we can agree on a direction, help would be most welcome.

License
-------

Apache 2.0, mostly.  See the [license](license) file for details.

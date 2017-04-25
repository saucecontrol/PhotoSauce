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

Release History
---------------

#### 0.7.0.0
* Added .NET Core version. The Core build does not include metadata support (including auto-rotation) due to the absence of CustomMarshaler support in NetStandard <2.
* Added vectorized (SIMD) versions of convolvers and matting/compositing.  Can be enabled/disabled via a global setting.
* Added pooling for all internal pixel buffers.  Reduces garbage collections overall and improves GC performance related to buffer pinning.
* Added support for greyscale (indexed) BMP output and proper greyscale palettes for indexed PNG and GIF.
* Added support for custom DPI settings.  Copy from input image or set explicitly.
* Added support for configuring resampling filter with dictionary config (for use with WebRSize).
* Added global setting to enable/disable the planar processing pipeline.
* Expanded scenarios in which the planar pipeline can be used.  It now works for all planar inputs.
* Increased quality of default resamplers for high-ratio downscaling.  The improved performance of the convolvers means no penalty for always scaling high-quality.
* Fixed invalid pixel format error when using planar processing with indexed color output.
* Fixed argument out of range error when using planar processing with non-planar output at some output sizes.

#### 0.6.1.0
* Fixed argument out of range error when using planar scaling with 4:2:2 subsampled JPEG source images.

#### 0.6.0.0
* Fixed invalid color profile error when using hybrid scaling with non-JPEG CMYK images.
* Enabled sharpening by default.  This can be disabled with the Sharpen property on ProcessImageSettings.
* Added support for copying metadata from the source image.  See the MetadataNames property on ProcessImageSettings.
* Added CubicSmoother preconfigured interpolator.
* Changed Lanczos filter default to 3 lobes.  Removed the preconfigured 2-lobe filter and changed the name of Lanczos3 to Lanczos.
* Removed Spline16 interpolator.  This was not available as a preconfigured filter but could have been configured manually.
* Removed some unused internal classes.

#### 0.5.0.0
* Initial public release

Versioning
----------

MagicScaler is using [semantic versioning](http://semver.org/).  Releases without an alpha/beta/RC tag are considered release quality and are safe for production use. The major version number will remain at 0, however, until the API is complete and stabilized.  You can expect significant API changes coming in 0.8.

Contributing
------------

Because MagicScaler is still under active design and development, I am not accepting unsolicited pull requests at this time.  If you find a bug or would like to see a new feature implemented, please open a new issue for further discussion.  This will hopefully save any wasted or duplicate efforts.  If we can agree on a direction, help would be most welcome.

License
-------

Apache 2.0, mostly.  See the [license](license) file for details.

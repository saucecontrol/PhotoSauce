MagicScaler
===========

MagicScaler brings high-performance, high-quality image scaling to .NET.

Requirements
------------

Windows 7* or later with .NET 4.6 or later

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
string inPath = @"c:\bigimage.jpg";
var outStream = new MemoryStream(16384);

MagicImageProcessor.ProcessImage(inPath, outStream, new ProcessImageSettings { Width = 400 });

File.WriteAllBytes(@"c:\smallimage.jpg", outStream.ToArray());
``` 

See the [documentation page](doc/main.md) for more details.

Release History
---------------

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

Contributing
------------

Because MagicScaler is still under active development, I am not accepting unsolicited pull requests at this time.  If you find a bug or would like to see a new feature implemented, please open a new issue for further discussion.  This will hopefully save any wasted or duplicate efforts.

License
-------

See the [license](license) file for details.

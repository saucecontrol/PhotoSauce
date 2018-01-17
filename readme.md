MagicScaler
===========

High-performance image processing pipeline for .NET.  Implements best-of-breed algorithms, linear light processing, and sharpening for the best image resizing quality available.  Speed and efficiency are unmatched by anything else on the .NET platform.

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
{
  MagicImageProcessor.ProcessImage(@"c:\bigimage.jpg", outStream, new ProcessImageSettings { Width = 400 });
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


Release History
---------------
#### MagicScaler 0.8.4.0
* Fixed an issue that caused sharpening to be a no-op when working with some pixel formats in sRGB blending mode.
* Improved quality of scaling and sharpening with partially-transparent images.
* Added [GitLink](https://github.com/GitTools/GitLink) to enable github source server support for debugging.

#### WebRSize 0.3.2.0
* Fixed incorrect file extension for 404 images in the disk cache
* Added exception handler for "Client Disconnected" errors when transmitting images from the HttpHandler
* Added devicePixelRatio (dpr) setting to enable automatic size and quality adjustments for retina clients
* Added "q" shortcut for quality setting

See the [releases page](https://github.com/saucecontrol/PhotoSauce/releases) for previous updates.

Versioning
----------

This project is using [semantic versioning](http://semver.org/).  Releases without an alpha/beta/RC tag are considered release quality and are safe for production use. The major version number will remain at 0, however, until the APIs are complete and stabilized.

Contributing
------------

Because this project is still under active design and development, I am not accepting unsolicited pull requests at this time.  If you find a bug or would like to see a new feature implemented, please open a new issue for further discussion.  This will hopefully save any wasted or duplicate efforts.  If we can agree on a direction, help would be most welcome.

License
-------

Apache 2.0, mostly.  See the [license](license) file for details.

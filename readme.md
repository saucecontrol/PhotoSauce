[![NuGet](https://buildstats.info/nuget/PhotoSauce.MagicScaler)](https://www.nuget.org/packages/PhotoSauce.MagicScaler/) [![Build Status](https://dev.azure.com/saucecontrol/PhotoSauce/_apis/build/status/saucecontrol.PhotoSauce?branchName=master)](https://dev.azure.com/saucecontrol/PhotoSauce/_build/latest?definitionId=1&branchName=master) [![CI NuGet](https://img.shields.io/badge/nuget-CI%20builds-4da2db?logo=azure-devops)](https://dev.azure.com/saucecontrol/PhotoSauce/_packaging?_a=feed&feed=photosauce_ci)

MagicScaler
===========

High-performance image processing pipeline for .NET.  Implements best-of-breed algorithms, linear light processing, and sharpening for the best image resizing quality available.  Speed and efficiency are unmatched by anything else on the .NET platform.

Requirements
------------

MagicScaler currently runs only on Windows.  Although MagicScaler is compatible with -- and optimized for -- .NET Core and .NET 5+, it requires the [Windows Imaging Component](https://docs.microsoft.com/en-us/windows/desktop/wic/-wic-about-windows-imaging-codec) for its image codec support.  Work is in progress to allow MagicScaler to use other native or managed codecs, which will allow running on Linux.

Usage
-----

Basic usage looks something like this:

```C#
string inPath = @"c:\img\bigimage.jpg";
string outPath = @"c:\img\smallimage.jpg";
var settings = new ProcessImageSettings { Width = 400 };

using var outStream = new FileStream(outPath, FileMode.Create);
MagicImageProcessor.ProcessImage(inPath, outStream, settings);
``` 

The above example will resize `bigimage.jpg` to a width of 400 pixels and save the output to	 `smallimage.jpg`.  The height will be set automatically to preserve the correct aspect ratio.  Default settings are optimized for a balance of speed and image quality.

The MagicScaler pipleline is also customizable if you wish to use an alternate pixel source, capture the output pixels for additional processing, or add custom filtering.

See the [full documentation](https://docs.photosauce.net) for more details.

MagicScaler Performance
-----------------------

Benchmark results in this section come from the tests used in https://blogs.msdn.microsoft.com/dotnet/2017/01/19/net-core-image-processing/ -- updated to use current (Apr 2021) versions of the libraries and runtime.  The original benchmark project [is also on GitHub](https://github.com/bleroy/core-imaging-playground).

*For these results, the benchmarks were modified to use a constant `UnrollFactor` so these runs more accurately report managed memory allocations and GC counts.  By default, BenchmarkDotNet targets a run time in the range of 500ms-1s for each iteration.  This means it executes slower benchmark methods using a smaller number of operations per iteration, and it can wildly under-report allocation and GCs, as those numbers are extrapolated from the limited iterations it runs.  The constant `UnrollFactor` ensures all benchmarks' reported memory stats are based on the same run counts. The `UnrollFactor` used for each run is listed at the top of each set of results.*

Benchmark environment:

``` ini
BenchmarkDotNet=v0.12.1.1514-nightly, OS=Windows 10.0.19042.867 (20H2/October2020Update)
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK=5.0.104
  [Host]       : .NET 5.0.4 (5.0.421.11614), X64 RyuJIT
  .Net 5.0 CLI : .NET 5.0.4 (5.0.421.11614), X64 RyuJIT
```

### End-to-End Image Resizing

First up is a semi-real-world image resizing benchmark, in which 12 JPEGs of approximately 1-megapixel each are resized to 150px wide thumbnails and saved back as JPEG.

``` ini
Job=.Net 5.0 CLI  Toolchain=.NET 5.0  IterationCount=5  LaunchCount=1  UnrollFactor=32  WarmupCount=5

|                                 Method |      Mean |     Error |   StdDev | Ratio | RatioSD |     Gen 0 |     Gen 1 |     Gen 2 | Allocated |
|--------------------------------------- |----------:|----------:|---------:|------:|--------:|----------:|----------:|----------:|----------:|
|     *MagicScaler Load, Resize, Save(1) |  65.94 ms |  0.628 ms | 0.163 ms |  0.17 |    0.00 |         - |         - |         - |     47 KB |
|   System.Drawing Load, Resize, Save(2) | 377.65 ms |  1.514 ms | 0.234 ms |  1.00 |    0.00 |         - |         - |         - |      8 KB |
|       ImageSharp Load, Resize, Save(3) | 151.90 ms |  1.351 ms | 0.209 ms |  0.40 |    0.00 |  468.7500 |   31.2500 |         - |  1,985 KB |
|      ImageMagick Load, Resize, Save(4) | 410.55 ms |  2.867 ms | 0.444 ms |  1.09 |    0.00 |         - |         - |         - |     50 KB |
|        ImageFree Load, Resize, Save(5) | 251.34 ms |  1.129 ms | 0.175 ms |  0.67 |    0.00 | 6000.0000 | 6000.0000 | 6000.0000 |     90 KB |
| SkiaSharp Canvas Load, Resize, Save(6) | 247.03 ms | 32.118 ms | 8.341 ms |  0.66 |    0.02 |         - |         - |         - |     95 KB |
| SkiaSharp Bitmap Load, Resize, Save(7) | 241.35 ms |  2.641 ms | 0.686 ms |  0.64 |    0.00 |         - |         - |         - |     81 KB |
|          NetVips Load, Resize, Save(8) | 133.71 ms |  2.132 ms | 0.330 ms |  0.35 |    0.00 |         - |         - |         - |     44 KB |
```

* (1) `PhotoSauce.MagicScaler` version 0.12.0.
* (2) `System.Drawing.Common` version 5.0.2.
* (3) `SixLabors.ImageSharp` version 1.0.3.
* (4) `Magick.NET-Q8-AnyCPU` version 7.23.3.
* (5) `FreeImage.Standard` version 4.3.8.
* (6) `SkiaSharp` version 2.80.2.
* (7) `NetVips` version 2.0.0 with `NetVips.Native` (libvips) version 8.10.6.

Note that unmanaged memory usage is not measured by BenchmarkDotNet's `MemoryDiagnoser`, nor is managed memory allocated but never released to GC (e.g. pooled objects/buffers).  See the [MagicScaler Efficiency](#magicscaler-efficiency) section for an analysis of total process memory usage for each library.

The performance numbers mostly speak for themselves, but some notes on image quality are warranted.  The benchmark suite saves the output so that the visual quality of the output of each library can be compared in addition to the performance.  See the [MagicScaler Quality](#magicscaler-quality) section below for details.

### Parallel End-to-End Resizing

This benchmark is the same as the previous but uses `Parallel.ForEach` to run the 12 test images in parallel.  It is meant to highlight cases where the libraries' performance doesn't scale up linearly with extra processors.

``` ini
Job=.Net 5.0 CLI  Toolchain=.NET 5.0  IterationCount=5  LaunchCount=1  UnrollFactor=32  WarmupCount=5

|                                         Method |      Mean |    Error |   StdDev | Ratio |     Gen 0 |     Gen 1 |     Gen 2 | Allocated |
|----------------------------------------------- |----------:|---------:|---------:|------:|----------:|----------:|----------:|----------:|
|     *MagicScaler Load, Resize, Save - Parallel |  17.83 ms | 0.857 ms | 0.133 ms |  0.11 |         - |         - |         - |     77 KB |
|   System.Drawing Load, Resize, Save - Parallel | 159.25 ms | 8.514 ms | 1.318 ms |  1.00 |         - |         - |         - |     34 KB |
|       ImageSharp Load, Resize, Save - Parallel |  41.14 ms | 1.790 ms | 0.465 ms |  0.26 |  500.0000 |  125.0000 |         - |  4,573 KB |
|      ImageMagick Load, Resize, Save - Parallel | 116.01 ms | 7.927 ms | 1.227 ms |  0.73 |         - |         - |         - |     75 KB |
|        ImageFree Load, Resize, Save - Parallel |  68.52 ms | 1.919 ms | 0.498 ms |  0.43 | 3875.0000 | 3875.0000 | 3875.0000 |    112 KB |
| SkiaSharp Canvas Load, Resize, Save - Parallel |  62.34 ms | 4.861 ms | 0.752 ms |  0.39 |         - |         - |         - |    118 KB |
| SkiaSharp Bitmap Load, Resize, Save - Parallel |  62.05 ms | 4.008 ms | 1.041 ms |  0.39 |         - |         - |         - |    104 KB |
|          NetVips Load, Resize, Save - Parallel |  54.00 ms | 4.324 ms | 1.123 ms |  0.34 |         - |         - |         - |     69 KB |
```

Note the relative performance drop-off for NetVips.  It uses multiple threads for a single operation by default, making it scale up poorly and leaving it vulnerable to [CPU oversubscription](https://web.archive.org/web/20200221153045/https://docs.microsoft.com/en-us/archive/blogs/visualizeparallel/oversubscription-a-classic-parallel-performance-problem) problems under heavy server load.

Similarly, System.Drawing fails to scale up as well as the other libraries, but for the opposite reason.  The System.Drawing tests run at less than 100% CPU when run in parallel, presumably due to some internal locking/serialization designed to limit memory use.

<details>
<summary>Resize-Only Synthetic Benchmark</summary>

### Resize-Only Synthetic Benchmark

This benchmark creates a blank image of 1280x853 and resizes it to 150x99, throwing away the result.  MagicScaler does very well on this one, and it's the only one MagicScaler can do on Linux (for now), but it isn't a real-world scenario, so take the results with a grain of salt.

``` ini
Job=.Net 5.0 CLI  Toolchain=.NET 5.0  IterationCount=5  LaunchCount=1  UnrollFactor=256  WarmupCount=5

|                  Method |        Mean |     Error |   StdDev | Ratio | RatioSD |    Gen 0 |    Gen 1 |    Gen 2 | Allocated |
|------------------------ |------------:|----------:|---------:|------:|--------:|---------:|---------:|---------:|----------:|
|     *MagicScaler Resize |    718.4 μs |   2.76 μs |  0.43 μs |  0.07 |    0.00 |        - |        - |        - |   1,904 B |
|   System.Drawing Resize | 10,665.7 μs | 244.82 μs | 63.58 μs |  1.00 |    0.00 |        - |        - |        - |     136 B |
|       ImageSharp Resize |  2,682.1 μs | 113.49 μs | 29.47 μs |  0.25 |    0.00 |        - |        - |        - |   9,152 B |
|      ImageMagick Resize | 50,694.0 μs |  57.54 μs | 14.94 μs |  4.75 |    0.03 |        - |        - |        - |   5,336 B |
|        FreeImage Resize |  7,658.7 μs |  61.17 μs | 15.89 μs |  0.72 |    0.00 | 500.0000 | 500.0000 | 500.0000 |     136 B |
| SkiaSharp Canvas Resize |  2,500.1 μs | 259.54 μs | 67.40 μs |  0.23 |    0.01 |        - |        - |        - |   1,584 B |
| SkiaSharp Bitmap Resize |  2,437.9 μs | 217.19 μs | 56.40 μs |  0.23 |    0.00 |        - |        - |        - |     488 B |
|          NetVips Resize |  5,703.9 μs | 278.41 μs | 72.30 μs |  0.53 |    0.01 |        - |        - |        - |   4,152 B |
```

</details>

MagicScaler Efficiency
----------------------

Raw speed isn't the only important factor when evaluating performance.  As demonstrated in the parallel benchmark results above, some libraries consume extra resources in order to produce a result quickly, at the expense of overall scalability.  Particularly when integrating image processing into another application, like a CMS or an E-Commerce site, it is important that your imaging library not steal resources from the rest of the system.  That applies to both processor time and memory.

BenchmarkDotNet does a good job of showing relative performance, and its managed memory diagnoser is quite useful for identifying excessive GC allocations, but its default configuration doesn't track actual processor usage or any memory that doesn't show up in GC collections.  For example, when it reports a time of 100ms on a benchmark, was that 100ms of a single processor at 100%?  More than one processor?  Less than 100%?  And what about memory allocated but never collected, like object caches and pooled arrays?  And what about unmanaged memory?  To capture these things, we must use different tools.

Because most of the libraries tested make calls to native libraries internally (ImageSharp is the only pure-managed library in the bunch), measuring only GC memory can be very misleading.  And even ImageSharp's memory usage isn't accurately reflected in the BDN `MemoryDiagnoser`'s numbers, because it holds allocated heap memory in the form of pooled objects and arrays (as does MagicScaler).

In order to accurately measure both CPU time and total memory usage, I devised a more real-world test.  The 1-megapixel images in the benchmark test suite make for reasonable benchmark run times, but 1-megapixel is hardly representative of what we see coming from even smartphones now.  In order to stress the libraries a bit more, I replaced the input images in the benchmark app's input folder with the [Bee Heads album](https://www.flickr.com/photos/usgsbiml/albums/72157633925491877) from the USGS Bee Inventory flickr.  This collection contains 351 images (350 JPEG, 1 GIF), ranging in size from 2-22 megapixels, with an average of 13.4 megapixels.  The total album is just over 2.5 GiB in size, and it can be downloaded directly from flickr.

I re-used the image resizing code from the benchmark app but processed the test images only once, using `Parallel.ForEach` to load up the system.  Because of the test image set's size, startup and JIT overhead are overshadowed by the actual image processing, and although there may be some variation in times between runs, the overall picture is accurate and is more realistic than the BDN runs that cycle through the same small set of small images.  Each library's test was run in isolation so memory stats would include only that library.

This table shows the actual CPU time and peak memory usage as captured by the [Windows Performance Toolkit](https://docs.microsoft.com/en-us/windows-hardware/test/wpt/) when running the modified benchmark app on .NET 5.0 x64.

|                   Method | Peak Memory | VirtualAlloc Total |  CPU Time |
|------------------------- |------------:|-------------------:|----------:|
|   *MagicScaler Bee Heads |      404 MB |            4749 MB |  50053 ms |
| System.Drawing Bee Heads |      849 MB |           36435 MB | 201269 ms |
|     ImageSharp Bee Heads |     7459 MB |           27575 MB | 161416 ms |
|    ImageMagick Bee Heads |      699 MB |           17481 MB | 291919 ms |
|      FreeImage Bee Heads |      612 MB |           16408 MB | 232971 ms |
|      SkiaSharp Bee Heads |      991 MB |           26368 MB | 190533 ms |
|        NetVips Bee Heads |      443 MB |            3170 MB | 130149 ms |

A few of the more interesting points from the above numbers:

ImageSharp shows relatively low allocations and GC counts in the BDN managed memory diagnoser, but it is clear from the WPT trace that it is allocating and never releasing large amounts of memory.  In a repetitive benchmark, this might not be obvious, but when working on a larger number of larger images, it will put a major strain on memory.

It's clear from the CPU time numbers that System.Drawing is spending a fair amount of its time idle.  Its total consumed time is roughly the same as SkiaSharp's, but its wall clock time shows it to be roughly 3x slower.  This can be seen in WPA's CPU utilization graphs but not in the BDN results.

NetVips shows higher CPU time in the WPT trace than it does running without tracing.  Its wall-clock time was almost identical to MagicScaler's on this image set.  Both Vips and MagicScaler lazily evaluate requests for pixels, so their memory usage is significantly lower than other libraries'.

<details>
<summary>32-bit Process Results</summary>
<br />

Because the peak memory numbers for some libraries are so high, it's also worth looking at how they perform in 32-bit environment, which is naturally memory constrained.  The following table shows results of the same test for .NET Core 5.0 x86.

|                   Method | Peak Memory | VirtualAlloc Total |  CPU Time |
|------------------------- |------------:|-------------------:|----------:|
|   *MagicScaler Bee Heads |      361 MB |            4747 MB |  57250 ms |
| System.Drawing Bee Heads |      769 MB |           36405 MB | 219007 ms |
|     ImageSharp Bee Heads |  FAIL - OOM |                    |           |
|    ImageMagick Bee Heads |      701 MB |           17381 MB | 359497 ms |
|      FreeImage Bee Heads |      671 MB |           16393 MB | 264883 ms |
|      SkiaSharp Bee Heads |      944 MB |           26366 MB | 288483 ms |
|        NetVips Bee Heads |      424 MB |            3277 MB |  94891 ms |

ImageSharp failed to complete the test with less addressable memory available.

Once again, MagicScaler is the most efficient with memory and CPU.  However once again, NetVips completed the suite in similar wall-clock time to MagicScaler, so its CPU time may not be accurate under the profiler.  All libraries that succeeded took longer in 32-bit mode, but SkiaSharp was disproportionately slower.

For the record, here is the exception thrown by ImageSharp:

ImageSharp
```
System.OutOfMemoryException: Exception of type 'System.OutOfMemoryException' was thrown.
   at System.Buffers.ConfigurableArrayPool`1.Rent(Int32 minimumLength)
   at SixLabors.Memory.ArrayPoolMemoryAllocator.Allocate[T](Int32 length, AllocationOptions options)
   ...
```

</details>

MagicScaler Quality
-------------------

The benchmark application detailed above saves the output from its end-to-end image resizing tests so they can be evaluated for file size and image quality.  The images were contributed by @bleroy, who created the original benchmark.  They were chosen because of their high-frequency detail which had been observed to be challenging for some image resampling algorithms.  The images have a couple of other interesting properties as well.

First, 10 of the 12 images are saved in the Abobe RGB color space, with an embedded color profile.  Second, they have a significant amount of metadata related to the source camera and their processing history.  Third, one of the images is in the sRGB color space and contains an embedded profile for sRGB, which is relatively large.  These factors combine to highlight some of the shortcomings in the tested libraries.

There is one design flaw in the benchmark app that makes it more difficult to judge the output quality, however.  The tests were configured to save the thumbnails with a low JPEG quality value and to use 4:2:0 [chroma subsampling](https://en.wikipedia.org/wiki/Chroma_subsampling).  While these values are acceptable or even ideal for large images, they will cause compression artifacts in areas of small details.  At thumbnail size, the *only* details are small details, so it is better to use a higher-quality JPEG setting for very small images.  MagicScaler performs this adjustment automatically under its default settings, but those are overridden in the benchmark suite to be consistent with the other libraries.

Note that outside the ill-advised overrides to JPEG quality settings, the results discussed here are from MagicScaler's default settings.  Not only is MagicScaler faster and more more efficient than the other libraries, it is also easier to use.  You'll get better quality with its defaults than can be obtained with lots of extra code in other libraries.

### Color Management

Handling images in a color space other than sRGB can be a challenge, and it's not something most developers are familiar with.  Now that all iOS and most Android devices' camera apps default to the [Display P3](https://en.wikipedia.org/wiki/DCI-P3#Display_P3) color space and wide-gamut displays are common, this is a topic of increasing importance.

MagicScaler will automatically normalize the color space of images it processes to maximize compatibility with image consumers while preserving the original gamut of the image.  Other software may not be color aware or may make it difficult to process in a color-correct manner.

<details>
<summary>Additional Info on Color Space Handling</summary>
<br />

There are essentially 3 ways to approach an image with an embedded color profile:

1) Convert the image to sRGB.  This has the advantage that any downstream software that reads the processed image does not need to be color managed to display it correctly.  This was historically a problem with many apps, including popular web browsers (you can [test yours here](https://chromachecker.com/webbrowser/en/manual)).  The disadvantage is that it takes extra processing to do this conversion.
2) Preserve the color space by embedding the ICC profile in the output image.  This is basically the opposite of option 1).  It's cheaper to do but may result in other software mangling the colors later.  It also results in a larger file because the profile may be very large -- in the case of a thumbnail, it might double the file size or more.
3) Ignore the color profile and treat the image as if it's encoded as sRGB.  This option is absolutely incorrect and would put your software in the category of color manglers mentioned above.

The libraries tested in the benchmark have different capabilities, so the options available depend on the library.

* System.Drawing supports options 1) and 3) but does 3) by default.  The original version of the benchmark did it that way, until I submitted a PR later to correct it to do 1).  This resulted in correct colors but a drop in speed.
* ImageSharp can do 2) or 3) and does 2) by default.  When it was originally integrated into the benchmark, however, it only did 3).
* ImageMagick can do any of the options above but does 2) by default.  However, it also preserves all other metadata by default, and the test images have quite a lot of metadata.  This results in thumbnails that are extremely oversized and consist of roughly 90% metadata.  For this reason, the ImageMagick tests were written to strip all metadata, resulting in behavior 3).
* FreeImage works the same way as ImageMagick by default, and its test was implemented the same way.  It could have done option 2), but it was implemented to do option 3).
* Skia can be made to do option 1) with a lot of [extra code](https://skia.org/user/sample/color?cl=9919).  This was not done in the benchmark tests, resulting in behavior 3).  Update: as of SkiaSharp 2.80, behavior 1) is now default, but with a marked reduction in speed.
* MagicScaler can do any of the above options but does option 2) by default.  I contributed the MagicScaler test in the benchmark myself, and it has been correct from the beginning, although it used behavior 1) by default initially.

The net result is that if you look at the sample image output in @bleroy's [blog post](https://devblogs.microsoft.com/dotnet/net-core-image-processing/), the MagicScaler output has different colors than all the others.  10 of the 12 images have washed-out colors in the output from the other libraries -- most apparent in the vibrant red, green, and blue hues, such as the snake or the Wild River ride on the back of what is now the [MoPOP building](https://www.mopop.org/building).  If you download the project today and run it, the outputs from System.Drawing (corrected by me), ImageSharp (fixed in the library), SkiaSharp (fixed in the library), and MagicScaler will all have correct colors, and the rest will be wrong.

The recently-added NetVips test has the same problem as ImageMagick.  It would do option 2) by default but would carry all the metadata with it and so has been written to do 3).  Like ImageMagick, it could be modified to do option 1) instead, but that would require much more code and would be significantly slower.

These are common mistakes made by developers starting out with image processing, because it can be easy to miss the shift in colors and difficult to discover how to do the right thing.

</details>

<samp>
Sample Images:

| System.Drawing | &nbsp;&nbsp;MagicScaler&nbsp; | &nbsp;&nbsp;ImageSharp&nbsp;&nbsp; | &nbsp;&nbsp;Magick.NET&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;&nbsp;NetVips&nbsp;&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;FreeImage&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;SkiaSharp&nbsp;&nbsp; |
|----------------|-------------|------------|------------|---------|-----------|-----------|
|<img src="/doc/images/IMG_2525-SystemDrawing.jpg" alt="System.Drawing" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-MagicScaler.jpg" alt="MagicScaler" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-ImageSharp.jpg" alt="ImageSharp" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-MagickNET.jpg" alt="MagickNET" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-NetVips.jpg" alt="NetVips" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-FreeImage.jpg" alt="FreeImage" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-SkiaSharpCanvas.jpg" alt="SkiaSharp" style="max-width: 150px" />|
</samp>

The color difference between these should be obvious.  Compared to the [original image](https://github.com/bleroy/core-imaging-playground/blob/master/images/IMG_2525.jpg), it's easy to see which are correct (unless your browser is busted).

### Gamma-Corrected Blending

Of the libraries tested in the benchmark, only MagicScaler performs the resampling step in [linear light](http://www.imagemagick.org/Usage/resize/#resize_colorspace).  ImageSharp, ImageMagick, and Vips are capable of processing in linear light but would require extra code to do so and would perform significantly worse.

<samp>
Sample Images:

| System.Drawing | &nbsp;&nbsp;MagicScaler&nbsp; | &nbsp;&nbsp;ImageSharp&nbsp;&nbsp; | &nbsp;&nbsp;Magick.NET&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;&nbsp;NetVips&nbsp;&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;FreeImage&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;SkiaSharp&nbsp;&nbsp; |
|----------------|-------------|------------|------------|---------|-----------|-----------|
|<img src="/doc/images/IMG_2301-SystemDrawing.jpg" alt="System.Drawing" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-MagicScaler.jpg" alt="MagicScaler" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-ImageSharp.jpg" alt="ImageSharp" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-MagickNET.jpg" alt="MagickNET" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-NetVips.jpg" alt="NetVips" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-FreeImage.jpg" alt="FreeImage" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-SkiaSharpCanvas.jpg" alt="SkiaSharp" style="max-width: 150px" />|
</samp>

In addition to keeping the correct colors, MagicScaler does markedly better at preserving image highlights because of the linear light blending.  Notice the highlights on the flowers are a better representation of those in the [original image](https://github.com/bleroy/core-imaging-playground/blob/master/images/IMG_2301.jpg)

### High-Quality Resampling

Most imaging libraries have at least some capability to do high-quality resampling, but not all do.  MagicScaler defaults to high-quality, but the other libraries in this test were configured for their best quality as well.

<samp>
Sample Images:

| System.Drawing | &nbsp;&nbsp;MagicScaler&nbsp; | &nbsp;&nbsp;ImageSharp&nbsp;&nbsp; | &nbsp;&nbsp;Magick.NET&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;&nbsp;NetVips&nbsp;&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;FreeImage&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;SkiaSharp&nbsp;&nbsp; |
|----------------|-------------|------------|------------|---------|-----------|-----------|
|<img src="/doc/images/IMG_2445-SystemDrawing.jpg" alt="System.Drawing" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-MagicScaler.jpg" alt="MagicScaler" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-ImageSharp.jpg" alt="ImageSharp" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-MagickNET.jpg" alt="MagickNET" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-NetVips.jpg" alt="NetVips" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-FreeImage.jpg" alt="FreeImage" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-SkiaSharpCanvas.jpg" alt="SkiaSharp" style="max-width: 150px" />|
</samp>

FreeImage and SkiaSharp have particularly poor image quality in this test, with output substantially more blurry than the others.

Note that when the benchmark and blog post were originally published, Skia supported multiple ways to resize images, which is why there are two Skia benchmark tests.  Under the current version of Skia, those two versions have the same benchmark numbers and same output quality, because the Skia internals have been changed to unify its resizing code.  The lower-quality version is [all that's left](https://github.com/mono/SkiaSharp/issues/520).

And here's that [original image](https://github.com/bleroy/core-imaging-playground/blob/master/images/IMG_2445.jpg) for reference.

### Sharpening

Finally, MagicScaler performs a post-resizing sharpening step to compensate for the natural blurring that occurs when an image is resized.  Some of the other libraries would be capable of doing the same, but again, that would require extra code and would negatively impact the performance numbers.

<samp>
Sample Images:

| System.Drawing | &nbsp;&nbsp;MagicScaler&nbsp; | &nbsp;&nbsp;ImageSharp&nbsp;&nbsp; | &nbsp;&nbsp;Magick.NET&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;&nbsp;NetVips&nbsp;&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;FreeImage&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;SkiaSharp&nbsp;&nbsp; |
|----------------|-------------|------------|------------|---------|-----------|-----------|
|<img src="/doc/images/sample-SystemDrawing.jpg" alt="System.Drawing" style="max-width: 150px" />|<img src="/doc/images/sample-MagicScaler.jpg" alt="MagicScaler" style="max-width: 150px" />|<img src="/doc/images/sample-ImageSharp.jpg" alt="ImageSharp" style="max-width: 150px" />|<img src="/doc/images/sample-MagickNET.jpg" alt="MagickNET" style="max-width: 150px" />|<img src="/doc/images/sample-NetVips.jpg" alt="NetVips" style="max-width: 150px" />|<img src="/doc/images/sample-FreeImage.jpg" alt="FreeImage" style="max-width: 150px" />|<img src="/doc/images/sample-SkiaSharpCanvas.jpg" alt="SkiaSharp" style="max-width: 150px" />|
</samp>

The linear light blending combined with the sharpening work to preserve more details from this [original image](https://github.com/bleroy/core-imaging-playground/blob/master/images/sample.jpg) than the other libraries do.  Again, some details are mangled by the poor JPEG settings, so MagicScaler's default settings would do even better.

Also of note is that ImageSharp's thumbnail for this image is roughly twice the size of the others because it has embedded the 3KiB sRGB color profile from the original image unnecessarily.


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

See the [documentation page](https://docs.photosauce.net/web.html) for more details.


Versioning
----------

This project is using [semantic versioning](http://semver.org/).  Releases without an alpha/beta/RC tag are considered release quality and are safe for production use. The major version number will remain at 0, however, until the APIs are complete and stabilized.

Contributing
------------

Contributions are welcome, but please open a new issue or discussion before submitting any pull requests that alter API or functionality.  This will hopefully save any wasted or duplicate efforts.

License
-------

PhotoSauce is licensed under the [MIT](license) license.

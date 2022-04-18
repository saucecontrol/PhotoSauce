[![NuGet](https://buildstats.info/nuget/PhotoSauce.MagicScaler)](https://www.nuget.org/packages/PhotoSauce.MagicScaler/) [![Build Status](https://dev.azure.com/saucecontrol/PhotoSauce/_apis/build/status/saucecontrol.PhotoSauce?branchName=master)](https://dev.azure.com/saucecontrol/PhotoSauce/_build/latest?definitionId=1&branchName=master) [![CI NuGet](https://img.shields.io/badge/nuget-CI%20builds-4da2db?logo=azure-devops)](https://dev.azure.com/saucecontrol/PhotoSauce/_packaging?_a=feed&feed=photosauce_ci)

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

See the [full documentation](https://docs.photosauce.net/api/PhotoSauce.MagicScaler.MagicImageProcessor.html) for more details.


MagicScaler Performance
-----------------------

Benchmark results in this section come from the tests used in https://blogs.msdn.microsoft.com/dotnet/2017/01/19/net-core-image-processing/ -- updated to use current (Apr 2022) versions of the libraries and runtime.  The original benchmark project [is also on GitHub](https://github.com/bleroy/core-imaging-playground).

*For these results, the benchmarks were modified to use a constant `UnrollFactor` so these runs more accurately report managed memory allocations and GC counts.  By default, BenchmarkDotNet targets a run time in the range of 500ms-1s for each iteration.  This means it executes slower benchmark methods using a smaller number of operations per iteration, and it can wildly under-report allocation and GCs, as those numbers are extrapolated from the limited iterations it runs.  The constant `UnrollFactor` ensures all benchmarks' reported memory stats are based on the same run counts. The `UnrollFactor` used for each run is listed at the top of each set of results.*

Benchmark environment:

``` ini
BenchmarkDotNet=v0.13.1.1695-nightly, OS=Windows 10 (10.0.19043.1586/21H1/May2021Update)
Intel Xeon W-11855M CPU 3.20GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK=6.0.300-preview.22204.3
  [Host]   : .NET 6.0.3 (6.0.322.12309), X64 RyuJIT
  ShortRun : .NET 6.0.3 (6.0.322.12309), X64 RyuJIT
```

### End-to-End Image Resizing

First up is a semi-real-world image resizing benchmark, in which 12 JPEGs of approximately 1-megapixel each are resized to 150px wide thumbnails and saved back as JPEG.

``` ini
Job=ShortRun  IterationCount=5  LaunchCount=1  UnrollFactor=32  WarmupCount=5

|                                 Method |      Mean |     Error |   StdDev | Ratio | RatioSD |     Gen 0 |     Gen 1 |     Gen 2 |  Allocated | Alloc Ratio |
|--------------------------------------- |----------:|----------:|---------:|------:|--------:|----------:|----------:|----------:|-----------:|------------:|
|     *MagicScaler Load, Resize, Save(1) |  55.71 ms |  3.683 ms | 0.957 ms |  0.16 |    0.00 |         - |         - |         - |   45.51 KB |        4.45 |
|   System.Drawing Load, Resize, Save(2) | 357.62 ms | 12.352 ms | 3.208 ms |  1.00 |    0.00 |         - |         - |         - |   10.23 KB |        1.00 |
|       ImageSharp Load, Resize, Save(3) | 108.06 ms |  7.481 ms | 1.943 ms |  0.30 |    0.01 |  156.2500 |   62.5000 |         - | 1492.73 KB |      145.98 |
|      ImageMagick Load, Resize, Save(4) | 349.53 ms | 13.851 ms | 3.597 ms |  0.98 |    0.02 |         - |         - |         - |   51.75 KB |        5.06 |
|        ImageFree Load, Resize, Save(5) | 209.06 ms |  8.241 ms | 2.140 ms |  0.58 |    0.01 | 6000.0000 | 6000.0000 | 6000.0000 |   92.69 KB |        9.06 |
| SkiaSharp Canvas Load, Resize, Save(6) | 167.62 ms |  7.604 ms | 1.975 ms |  0.47 |    0.01 |         - |         - |         - |  101.44 KB |        9.92 |
| SkiaSharp Bitmap Load, Resize, Save(6) | 169.28 ms |  2.591 ms | 0.401 ms |  0.47 |    0.00 |         - |         - |         - |    85.5 KB |        8.36 |
|          NetVips Load, Resize, Save(7) | 102.40 ms |  1.180 ms | 0.307 ms |  0.29 |    0.00 |         - |         - |         - |   46.04 KB |        4.50 |
```

* (1) `PhotoSauce.MagicScaler` version 0.13.0.
* (2) `System.Drawing.Common` version 5.0.3.
* (3) `SixLabors.ImageSharp` version 2.1.0.
* (4) `Magick.NET-Q8-AnyCPU` version 11.1.0.
* (5) `FreeImage.Standard` version 4.3.8.
* (6) `SkiaSharp` version 2.80.3.
* (7) `NetVips` version 2.1.0 with `NetVips.Native` (libvips) version 8.12.2.

Note that unmanaged memory usage is not measured by BenchmarkDotNet's `MemoryDiagnoser`, nor is managed memory allocated but never released to GC (e.g. pooled objects/buffers).  See the [MagicScaler Efficiency](#magicscaler-efficiency) section for an analysis of total process memory usage for each library.

The performance numbers mostly speak for themselves, but some notes on image quality are warranted.  The benchmark suite saves the output so that the visual quality of the output of each library can be compared in addition to the performance.  See the [MagicScaler Quality](#magicscaler-quality) section below for details.

### Parallel End-to-End Resizing

This benchmark is the same as the previous but uses `Parallel.ForEach` to run the 12 test images in parallel.  It is meant to highlight cases where the libraries' performance doesn't scale up linearly with extra processors.

``` ini
Job=ShortRun  IterationCount=5  LaunchCount=1  UnrollFactor=64  WarmupCount=5

|                                         Method |      Mean |    Error |   StdDev | Ratio |     Gen 0 |     Gen 1 |     Gen 2 |  Allocated | Alloc Ratio |
|----------------------------------------------- |----------:|---------:|---------:|------:|----------:|----------:|----------:|-----------:|------------:|
|     *MagicScaler Load, Resize, Save - Parallel |  11.49 ms | 0.643 ms | 0.100 ms |  0.09 |         - |         - |         - |   73.98 KB |        1.88 |
|   System.Drawing Load, Resize, Save - Parallel | 133.11 ms | 0.709 ms | 0.184 ms |  1.00 |         - |         - |         - |   39.41 KB |        1.00 |
|       ImageSharp Load, Resize, Save - Parallel |  22.91 ms | 0.281 ms | 0.073 ms |  0.17 |  156.2500 |   78.1250 |         - | 1524.18 KB |       38.68 |
|      ImageMagick Load, Resize, Save - Parallel | 100.92 ms | 5.891 ms | 1.530 ms |  0.76 |         - |         - |         - |   85.45 KB |        2.17 |
|        ImageFree Load, Resize, Save - Parallel |  46.08 ms | 2.399 ms | 0.371 ms |  0.35 | 3062.5000 | 3062.5000 | 3062.5000 |  118.96 KB |        3.02 |
| SkiaSharp Canvas Load, Resize, Save - Parallel |  34.09 ms | 0.527 ms | 0.082 ms |  0.26 |   15.6250 |         - |         - |  134.99 KB |        3.43 |
| SkiaSharp Bitmap Load, Resize, Save - Parallel |  34.36 ms | 1.025 ms | 0.159 ms |  0.26 |   15.6250 |         - |         - |   116.9 KB |        2.97 |
|          NetVips Load, Resize, Save - Parallel |  38.69 ms | 0.252 ms | 0.065 ms |  0.29 |         - |         - |         - |   77.99 KB |        1.98 |
```

Note the relative performance drop-off for NetVips.  It uses multiple threads for a single operation by default, making it scale up poorly and leaving it vulnerable to [CPU oversubscription](https://web.archive.org/web/20200221153045/https://docs.microsoft.com/en-us/archive/blogs/visualizeparallel/oversubscription-a-classic-parallel-performance-problem) problems under heavy server load.

Similarly, System.Drawing fails to scale up as well as the other libraries, but for the opposite reason.  The System.Drawing tests run at less than 100% CPU when run in parallel, presumably due to some internal locking/serialization designed to limit memory use.

<details>
<summary>Resize-Only Synthetic Benchmark</summary>

### Resize-Only Synthetic Benchmark

This benchmark creates a blank image of 1280x853 and resizes it to 150x99, throwing away the result.  MagicScaler does very well on this one, and it's the only one MagicScaler can do on Linux (for now), but it isn't a real-world scenario, so take the results with a grain of salt.

``` ini
Job=ShortRun  IterationCount=15  LaunchCount=1  UnrollFactor=256  WarmupCount=5

|                  Method |        Mean |     Error |    StdDev | Ratio | RatioSD |    Gen 0 |    Gen 1 |    Gen 2 | Allocated | Alloc Ratio |
|------------------------ |------------:|----------:|----------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
|     *MagicScaler Resize |    577.9 μs |   1.01 μs |   0.94 μs |  0.06 |    0.00 |        - |        - |        - |    1425 B |       10.33 |
|   System.Drawing Resize |  9,093.6 μs |  84.16 μs |  74.61 μs |  1.00 |    0.00 |        - |        - |        - |     138 B |        1.00 |
|       ImageSharp Resize |  2,247.4 μs |  94.39 μs |  88.29 μs |  0.25 |    0.01 |        - |        - |        - |   10364 B |       75.10 |
|      ImageMagick Resize | 37,032.8 μs | 906.42 μs | 847.87 μs |  4.08 |    0.11 |        - |        - |        - |    5338 B |       38.68 |
|        FreeImage Resize |  5,940.9 μs | 119.98 μs | 112.23 μs |  0.65 |    0.01 | 500.0000 | 500.0000 | 500.0000 |     306 B |        2.22 |
| SkiaSharp Canvas Resize |  1,577.6 μs |  19.76 μs |  18.49 μs |  0.17 |    0.00 |        - |        - |        - |    1745 B |       12.64 |
| SkiaSharp Bitmap Resize |  1,558.8 μs |  10.30 μs |   9.13 μs |  0.17 |    0.00 |        - |        - |        - |     489 B |        3.54 |
|          NetVips Resize |  3,540.6 μs |  23.28 μs |  21.78 μs |  0.39 |    0.00 |        - |        - |        - |    3859 B |       27.96 |
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

Versioning
----------

This project is using [semantic versioning](http://semver.org/).  Releases without a Preview/RC tag are considered release quality and are safe for production use. The major version number will remain at 0, however, until the APIs are complete and stabilized.

Contributing
------------

Contributions are welcome, but please open a new issue or discussion before submitting any pull requests that alter API or functionality.  This will hopefully save any wasted or duplicate effort.

License
-------

PhotoSauce is licensed under the [MIT](license) license.

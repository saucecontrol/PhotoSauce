[![NuGet](https://buildstats.info/nuget/PhotoSauce.MagicScaler)](https://www.nuget.org/packages/PhotoSauce.MagicScaler/) [![Build Status](https://dev.azure.com/saucecontrol/PhotoSauce/_apis/build/status/saucecontrol.PhotoSauce?branchName=master)](https://dev.azure.com/saucecontrol/PhotoSauce/_build/latest?definitionId=1&branchName=master) [![CI NuGet](https://img.shields.io/badge/nuget-CI%20builds-4da2db?logo=azure-devops)](https://dev.azure.com/saucecontrol/PhotoSauce/_packaging?_a=feed&feed=photosauce_ci)

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


MagicScaler Performance
-----------------------

Benchmark results in this section come from the tests used in https://blogs.msdn.microsoft.com/dotnet/2017/01/19/net-core-image-processing/ -- updated to use current (Nov 2022) versions of the libraries and runtime.  The original benchmark project [is also on GitHub](https://github.com/bleroy/core-imaging-playground).

*For these results, the benchmarks were modified to use a constant `UnrollFactor` so these runs more accurately report managed memory allocations and GC counts.  By default, BenchmarkDotNet targets a run time in the range of 500ms-1s for each iteration.  This means it executes slower benchmark methods using a smaller number of operations per iteration, and it can wildly under-report allocation and GCs, as those numbers are extrapolated from the limited iterations it runs.  The constant `UnrollFactor` ensures all benchmarks' reported memory stats are based on the same run counts. The `UnrollFactor` used for each run is listed at the top of each set of results.*

### End-to-End Image Resizing

This is a semi-real-world image resizing benchmark, in which 12 JPEGs of approximately 1 megapixel each are resized to 150px wide thumbnails and saved back as JPEG.  Not all libraries are supported on all platforms.  See version notes below.

#### Windows x64

```ini
BenchmarkDotNet=v0.13.2.1974-nightly, OS=Windows 10 (10.0.19043.2006/21H1/May2021Update)
Intel Xeon W-11855M CPU 3.20GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK=7.0.100-rc.2.22477.23
  ShortRun : .NET 6.0.10 (6.0.1022.47605), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  UnrollFactor=32  WarmupCount=3

|                               Method |      Mean |     Error |   StdDev | Ratio | RatioSD |      Gen0 |      Gen1 |      Gen2 |  Allocated | Alloc Ratio |
|------------------------------------- |----------:|----------:|---------:|------:|--------:|----------:|----------:|----------:|-----------:|------------:|
|   *MagicScaler Load, Resize, Save(1) |  46.85 ms |  2.948 ms | 0.456 ms |  0.13 |    0.00 |         - |         - |         - |   42.37 KB |        4.65 |
| System.Drawing Load, Resize, Save(2) | 354.73 ms |  8.168 ms | 1.264 ms |  1.00 |    0.00 |         - |         - |         - |    9.11 KB |        1.00 |
|      ImageFlow Load, Resize, Save(3) | 226.48 ms |  2.284 ms | 0.353 ms |  0.64 |    0.00 |  968.7500 |  968.7500 |  968.7500 | 4627.13 KB |      508.17 |
|     ImageSharp Load, Resize, Save(4) | 115.90 ms | 12.847 ms | 3.724 ms |  0.33 |    0.04 |  156.2500 |   62.5000 |         - |  1532.8 KB |      168.34 |
|    ImageMagick Load, Resize, Save(5) | 345.99 ms | 14.847 ms | 3.856 ms |  0.98 |    0.01 |         - |         - |         - |   50.44 KB |        5.54 |
|      ImageFree Load, Resize, Save(6) | 212.10 ms |  2.055 ms | 0.318 ms |  0.60 |    0.00 | 6000.0000 | 6000.0000 | 6000.0000 |   91.95 KB |       10.10 |
|      SkiaSharp Load, Resize, Save(7) | 117.43 ms |  1.270 ms | 0.330 ms |  0.33 |    0.00 |         - |         - |         - |   84.19 KB |        9.25 |
|        NetVips Load, Resize, Save(8) | 100.92 ms |  4.383 ms | 0.678 ms |  0.28 |    0.00 |         - |         - |         - |   115.9 KB |       12.73 |
```

#### Windows Arm64 (Windows Dev Kit 2023)

```ini
BenchmarkDotNet=v0.13.2.1974-nightly, OS=Windows 11 (10.0.22621.674)
Snapdragon Compute Platform, 1 CPU, 8 logical and 8 physical cores
.NET SDK=6.0.402
  ShortRun : .NET 6.0.10 (6.0.1022.47605), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=5  LaunchCount=1  UnrollFactor=32  WarmupCount=3

|                               Method |      Mean |     Error |   StdDev | Ratio |     Gen0 |     Gen1 |  Allocated | Alloc Ratio |
|------------------------------------- |----------:|----------:|---------:|------:|---------:|---------:|-----------:|------------:|
|   *MagicScaler Load, Resize, Save(1) |  65.72 ms |  0.532 ms | 0.082 ms |  0.17 |        - |        - |   42.36 KB |        4.65 |
| System.Drawing Load, Resize, Save(2) | 386.78 ms |  2.359 ms | 0.613 ms |  1.00 |        - |        - |    9.11 KB |        1.00 |
|     ImageSharp Load, Resize, Save(4) | 287.06 ms |  3.125 ms | 0.812 ms |  0.74 | 375.0000 | 187.5000 | 1635.48 KB |      179.62 |
|    ImageMagick Load, Resize, Save(5) | 588.63 ms | 13.049 ms | 2.019 ms |  1.52 |        - |        - |   50.44 KB |        5.54 |
|      SkiaSharp Load, Resize, Save(7) | 158.27 ms |  1.816 ms | 0.472 ms |  0.41 |        - |        - |   82.32 KB |        9.04 |
|        NetVips Load, Resize, Save(8) | 136.21 ms |  6.125 ms | 1.591 ms |  0.35 |        - |        - |   115.9 KB |       12.73 |
```

#### Linux x64 (WSL2)

```ini
BenchmarkDotNet=v0.13.2.1974-nightly, OS=ubuntu 20.04
Intel Xeon W-11855M CPU 3.20GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK=6.0.402
  ShortRun : .NET 6.0.10 (6.0.1022.47605), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  UnrollFactor=32  WarmupCount=3

|                               Method |     Mean |    Error |   StdDev | Ratio | RatioSD |     Gen0 |     Gen1 |     Gen2 |  Allocated | Alloc Ratio |
|------------------------------------- |---------:|---------:|---------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
|    MagicScaler Load, Resize, Save(1) |  99.8 ms |  1.91 ms |  0.47 ms |  0.37 |    0.01 |        - |        - |        - |   42.38 KB |        4.51 |
| System.Drawing Load, Resize, Save(2) | 271.7 ms | 12.34 ms |  3.20 ms |  1.00 |    0.00 |        - |        - |        - |     9.4 KB |        1.00 |
|      ImageFlow Load, Resize, Save(3) | 321.2 ms |  6.80 ms |  1.77 ms |  1.18 |    0.01 | 968.7500 | 968.7500 | 968.7500 | 4627.46 KB |      492.06 |
|     ImageSharp Load, Resize, Save(4) | 226.5 ms |  4.09 ms |  1.06 ms |  0.83 |    0.01 | 156.2500 |  62.5000 |        - | 1532.81 KB |      162.99 |
|    ImageMagick Load, Resize, Save(5) | 522.6 ms | 27.02 ms |  7.02 ms |  1.92 |    0.04 |        - |        - |        - |   50.84 KB |        5.41 |
|      SkiaSharp Load, Resize, Save(7) | 338.4 ms | 38.62 ms | 10.03 ms |  1.25 |    0.04 |        - |        - |        - |   84.48 KB |        8.98 |
|        NetVips Load, Resize, Save(8) | 380.5 ms | 11.04 ms |  2.87 ms |  1.40 |    0.02 |        - |        - |        - |  116.48 KB |       12.39 |
```

#### Linux Arm64 (Raspberry Pi 4b 2GB)

```ini
BenchmarkDotNet=v0.13.2.1974-nightly, OS=ubuntu 22.04
Unknown processor
.NET SDK=6.0.402
  ShortRun : .NET 6.0.10 (6.0.1022.47605), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=5  LaunchCount=1  UnrollFactor=8  WarmupCount=3

|                               Method |       Mean |    Error |  StdDev | Ratio |      Gen0 |     Gen1 |  Allocated | Alloc Ratio |
|------------------------------------- |-----------:|---------:|--------:|------:|----------:|---------:|-----------:|------------:|
|   *MagicScaler Load, Resize, Save(1) |   214.7 ms |  4.33 ms | 0.67 ms |  0.18 |         - |        - |   43.67 KB |        4.42 |
| System.Drawing Load, Resize, Save(2) | 1,205.9 ms | 26.54 ms | 6.89 ms |  1.00 |         - |        - |    9.87 KB |        1.00 |
|     ImageSharp Load, Resize, Save(4) |   997.0 ms | 18.08 ms | 4.70 ms |  0.83 | 1625.0000 | 375.0000 | 1635.48 KB |      165.72 |
|    ImageMagick Load, Resize, Save(5) | 1,688.5 ms | 12.84 ms | 3.34 ms |  1.40 |         - |        - |   51.47 KB |        5.21 |
|      SkiaSharp Load, Resize, Save(7) |   279.7 ms |  1.87 ms | 0.48 ms |  0.23 |  125.0000 |        - |   81.29 KB |        8.24 |
|        NetVips Load, Resize, Save(8) |   421.5 ms | 14.72 ms | 3.82 ms |  0.35 |  125.0000 |        - |  117.35 KB |       11.89 |
```

#### Versions Tested

* (1) `PhotoSauce.MagicScaler` version 0.13.2 with `PhotoSauce.NativeCodecs.Libjpeg` version 2.1.4-preview1.
* (2) `System.Drawing.Common` version 5.0.3.
* (3) `Imageflow.AllPlatforms` Version 0.9.0.
* (4) `SixLabors.ImageSharp` version 2.1.3.
* (5) `Magick.NET-Q8-AnyCPU` version 12.2.0.
* (6) `FreeImage.Standard` version 4.3.8.
* (7) `SkiaSharp` version 2.88.3.
* (8) `NetVips` version 2.2.0 with `NetVips.Native` (libvips) version 8.13.2.

Note that unmanaged memory usage is not measured by BenchmarkDotNet's `MemoryDiagnoser`, nor is managed memory allocated but never released to GC (e.g. pooled objects/buffers).  See the [MagicScaler Efficiency](#magicscaler-efficiency) section for an analysis of total process memory usage for each library.

The performance numbers mostly speak for themselves, but some notes on image quality are warranted.  The benchmark suite saves the output so that the visual quality of the output of each library can be compared in addition to the performance.  See the [MagicScaler Quality](#magicscaler-quality) section below for details.

<details>
<summary>More Benchmarks</summary>

Benchmark environment:

``` ini
BenchmarkDotNet=v0.13.2.1974-nightly, OS=Windows 10 (10.0.19043.2006/21H1/May2021Update)
Intel Xeon W-11855M CPU 3.20GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK=7.0.100-rc.2.22477.23
  ShortRun : .NET 6.0.10 (6.0.1022.47605), X64 RyuJIT AVX2
```

### Parallel End-to-End Resizing

This benchmark is the same as the previous but uses `Parallel.ForEach` to run the 12 test images in parallel.  It is meant to highlight cases where the libraries' performance doesn't scale up linearly with extra processors.

``` ini
Job=ShortRun  IterationCount=5  LaunchCount=1  UnrollFactor=64  WarmupCount=3

|                                       Method |      Mean |     Error |    StdDev | Ratio | RatioSD |      Gen0 |      Gen1 |      Gen2 |  Allocated | Alloc Ratio |
|--------------------------------------------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|----------:|-----------:|------------:|
|   *MagicScaler Load, Resize, Save - Parallel |  11.07 ms |  0.511 ms |  0.133 ms |  0.08 |    0.00 |         - |         - |         - |   71.35 KB |        1.95 |
| System.Drawing Load, Resize, Save - Parallel | 144.01 ms | 26.195 ms |  4.054 ms |  1.00 |    0.00 |         - |         - |         - |   36.62 KB |        1.00 |
|      ImageFlow Load, Resize, Save - Parallel |  51.70 ms |  5.179 ms |  0.801 ms |  0.36 |    0.01 |  984.3750 |  984.3750 |  984.3750 | 4628.02 KB |      126.39 |
|     ImageSharp Load, Resize, Save - Parallel |  26.25 ms | 32.435 ms |  5.019 ms |  0.18 |    0.03 |  171.8750 |   78.1250 |         - | 1564.91 KB |       42.74 |
|    ImageMagick Load, Resize, Save - Parallel | 149.86 ms | 27.115 ms |  7.042 ms |  1.03 |    0.05 |         - |         - |         - |   88.22 KB |        2.41 |
|      ImageFree Load, Resize, Save - Parallel |  63.07 ms | 41.212 ms | 10.703 ms |  0.45 |    0.09 | 3156.2500 | 3156.2500 | 3156.2500 |  117.06 KB |        3.20 |
|      SkiaSharp Load, Resize, Save - Parallel |  26.31 ms |  1.399 ms |  0.216 ms |  0.18 |    0.01 |   15.6250 |         - |         - |  110.13 KB |        3.01 |
```

The NetVips test hung during this benchmark and had to be excluded.  Previous versions worked, so it is unclear whether the issue lies with BenchmarkDotNet or a change in NetVips.

### Resize-Only Synthetic Benchmark

This benchmark creates a blank image of 1280x853 and resizes it to 150x99, throwing away the result.  MagicScaler does very well on this one, but it isn't a real-world scenario, so take the results with a grain of salt.

``` ini
Job=ShortRun  IterationCount=5  LaunchCount=1  UnrollFactor=256  WarmupCount=3

|                Method |        Mean |       Error |    StdDev | Ratio | RatioSD |     Gen0 |     Gen1 |     Gen2 | Allocated | Alloc Ratio |
|---------------------- |------------:|------------:|----------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
|   *MagicScaler Resize |    621.8 us |    57.39 us |  14.90 us |  0.07 |    0.00 |        - |        - |        - |    1385 B |       10.04 |
| System.Drawing Resize |  9,525.0 us |   619.59 us | 160.91 us |  1.00 |    0.00 |        - |        - |        - |     138 B |        1.00 |
|      ImageFlow Resize |  6,464.7 us |   237.70 us |  61.73 us |  0.68 |    0.01 |  11.7188 |        - |        - |  116005 B |      840.62 |
|     ImageSharp Resize |  2,299.1 us |    72.70 us |  18.88 us |  0.24 |    0.01 |        - |        - |        - |   10365 B |       75.11 |
|    ImageMagick Resize | 39,426.5 us | 2,093.00 us | 543.54 us |  4.14 |    0.10 |        - |        - |        - |    5338 B |       38.68 |
|      FreeImage Resize |  5,855.3 us |   324.42 us |  84.25 us |  0.61 |    0.01 | 500.0000 | 500.0000 | 500.0000 |     306 B |        2.22 |
|      SkiaSharp Resize |  1,560.8 us |    49.18 us |   7.61 us |  0.16 |    0.00 |        - |        - |        - |     489 B |        3.54 |
|        NetVips Resize |  9,412.2 us |   131.95 us |  34.27 us |  0.99 |    0.02 |        - |        - |        - |    3859 B |       27.96 |
```

</details>

MagicScaler Efficiency
----------------------

Raw speed isn't the only important factor when evaluating performance.  As demonstrated in the parallel benchmark results above, some libraries consume extra resources in order to produce a result quickly, at the expense of overall scalability.  Particularly when integrating image processing into another application, like a CMS or an E-Commerce site, it is important that your imaging library not steal resources from the rest of the system.  That applies to both processor time and memory.

BenchmarkDotNet does a good job of showing relative performance, and its managed memory diagnoser is quite useful for identifying excessive GC allocations, but its default configuration doesn't track actual processor usage or any memory that doesn't show up in GC collections.  For example, when it reports a time of 100ms on a benchmark, was that 100ms of a single processor at 100%?  More than one processor?  Less than 100%?  And what about memory allocated but never collected, like object caches and pooled arrays?  And what about unmanaged memory?  To capture these things, we must use different tools.

Because most of the libraries tested make calls to native libraries internally (ImageSharp is the only pure-managed library in the bunch), measuring only GC memory can be very misleading.  And even ImageSharp's memory usage isn't accurately reflected in the BDN `MemoryDiagnoser`'s numbers, because it holds allocated heap memory in the form of pooled objects and arrays (as does MagicScaler).

In order to accurately measure both CPU time and total memory usage, I devised a more real-world test.  The 1-megapixel images in the benchmark test suite make for reasonable benchmark run times, but 1-megapixel is hardly representative of what we see coming from even smartphones now.  In order to stress the libraries a bit more, I replaced the input images in the benchmark app's input folder with the [Bee Heads album](https://www.flickr.com/photos/usgsbiml/albums/72157633925491877) from the USGS Bee Inventory flickr.  This collection contains 351 images (350 JPEG, 1 GIF), ranging in size from 2-22 megapixels, with an average of 13.4 megapixels.  The total album is just over 2.5 GiB in size, and it can be downloaded directly from flickr.

I re-used the image resizing code from the benchmark app but processed the test images only once, using `Parallel.ForEach` to load up the system.  Because of the test image set's size, startup and JIT overhead are overshadowed by the actual image processing, and although there may be some variation in times between runs, the overall picture is accurate and is more realistic than the BDN runs that cycle through the same small set of small images.  Each library's test was run in isolation so memory stats would include only that library.

This table shows the actual CPU time and peak memory usage as captured by the [Windows Performance Toolkit](https://docs.microsoft.com/en-us/windows-hardware/test/wpt/) when running the modified benchmark app on .NET 6.0 x64.

|                   Method | Peak Memory | VirtualAlloc Total |  CPU Time | Clock Time |
|------------------------- |------------:|-------------------:|----------:|-----------:|
|   *MagicScaler Bee Heads |      420 MB |            2389 MB |  37153 ms |      3.99s |
| System.Drawing Bee Heads |     1001 MB |           36449 MB | 156050 ms |     39.58s |
|     ImageSharp Bee Heads |     1079 MB |            1101 MB |  96510 ms |     10.76s |
|      ImageFlow Bee Heads |     1485 MB |           28843 MB | 209247 ms |     22.61s |
|    ImageMagick Bee Heads |      878 MB |           17426 MB | 277780 ms |     29.86s |
|      FreeImage Bee Heads |     1048 MB |           16398 MB | 198207 ms |     21.77s |
|      SkiaSharp Bee Heads |     1273 MB |           26371 MB | 110919 ms |     12.13s |
|        NetVips Bee Heads |      785 MB |            3029 MB |  43515 ms |      5.01s |

It's clear from the CPU time vs wall clock time that System.Drawing is spending a fair amount of its time idle.  Its total consumed CPU time is middle of the pack, but its wall clock time shows it to be the slowest by far.

Earlier runs of this test showed extreme total memory use in NetVips and ImageSharp, but they've both brough their memory use way down.  MagicScaler still manages clear wins in peak memory and CPU time.

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

These are common mistakes made by developers starting out with image processing, because it can be easy to miss the shift in colors and difficult to discover how to do the right thing.

</details>

<samp>
Sample Images:

| System.Drawing | &nbsp;&nbsp;MagicScaler&nbsp; | &nbsp;&nbsp;ImageSharp&nbsp;&nbsp; | &nbsp;&nbsp;Magick.NET&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;&nbsp;NetVips&nbsp;&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;FreeImage&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;SkiaSharp&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;ImageFlow&nbsp;&nbsp; |
|----------------|-------------|------------|------------|---------|-----------|-----------|-----------|
|<img src="/doc/images/IMG_2525-SystemDrawing.jpg" alt="System.Drawing" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-MagicScaler.jpg" alt="MagicScaler" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-ImageSharp.jpg" alt="ImageSharp" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-MagickNET.jpg" alt="MagickNET" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-NetVips.jpg" alt="NetVips" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-FreeImage.jpg" alt="FreeImage" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-SkiaSharpCanvas.jpg" alt="SkiaSharp" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-ImageFlow.jpg" alt="ImageFlow" style="max-width: 150px" />|
</samp>

The color difference between these should be obvious.  Compared to the [original image](https://github.com/bleroy/core-imaging-playground/blob/master/images/IMG_2525.jpg), it's easy to see which are correct (unless your browser is busted).

### Gamma-Corrected Blending

Of the libraries originally tested in the benchmark, only MagicScaler performs the resampling step in [linear light](http://www.imagemagick.org/Usage/resize/#resize_colorspace).  ImageSharp, ImageMagick, and Vips are capable of processing in linear light but would require extra code to do so and would perform significantly worse.  ImageFlow is a recent addition which also processes in linear light by default.

<samp>
Sample Images:

| System.Drawing | &nbsp;&nbsp;MagicScaler&nbsp; | &nbsp;&nbsp;ImageSharp&nbsp;&nbsp; | &nbsp;&nbsp;Magick.NET&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;&nbsp;NetVips&nbsp;&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;FreeImage&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;SkiaSharp&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;ImageFlow&nbsp;&nbsp; |
|----------------|-------------|------------|------------|---------|-----------|-----------|-----------|
|<img src="/doc/images/IMG_2301-SystemDrawing.jpg" alt="System.Drawing" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-MagicScaler.jpg" alt="MagicScaler" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-ImageSharp.jpg" alt="ImageSharp" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-MagickNET.jpg" alt="MagickNET" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-NetVips.jpg" alt="NetVips" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-FreeImage.jpg" alt="FreeImage" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-SkiaSharpCanvas.jpg" alt="SkiaSharp" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-ImageFlow.jpg" alt="ImageFlow" style="max-width: 150px" />|
</samp>

In addition to keeping the correct colors, MagicScaler does markedly better at preserving image highlights because of the linear light blending.  Notice the highlights on the flowers are a better representation of those in the [original image](https://github.com/bleroy/core-imaging-playground/blob/master/images/IMG_2301.jpg)

### High-Quality Resampling

Most imaging libraries have at least some capability to do high-quality resampling, but not all do.  MagicScaler defaults to high-quality, but the other libraries in this test were configured for their best quality as well.

<samp>
Sample Images:

| System.Drawing | &nbsp;&nbsp;MagicScaler&nbsp; | &nbsp;&nbsp;ImageSharp&nbsp;&nbsp; | &nbsp;&nbsp;Magick.NET&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;&nbsp;NetVips&nbsp;&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;FreeImage&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;SkiaSharp&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;ImageFlow&nbsp;&nbsp; |
|----------------|-------------|------------|------------|---------|-----------|-----------|-----------|
|<img src="/doc/images/IMG_2445-SystemDrawing.jpg" alt="System.Drawing" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-MagicScaler.jpg" alt="MagicScaler" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-ImageSharp.jpg" alt="ImageSharp" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-MagickNET.jpg" alt="MagickNET" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-NetVips.jpg" alt="NetVips" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-FreeImage.jpg" alt="FreeImage" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-SkiaSharpCanvas.jpg" alt="SkiaSharp" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-ImageFlow.jpg" alt="ImageFlow" style="max-width: 150px" />|
</samp>

FreeImage and SkiaSharp have particularly poor image quality in this test, with output substantially more blurry than the others.  ImageFlow benefits from its linear light processing but also produces blurrier output than others.

Note that when the benchmark and blog post were originally published, Skia supported multiple ways to resize images, which is why there are two Skia benchmark tests.  Under the current version of Skia, those two versions have the same benchmark numbers and same output quality, because the Skia internals have been changed to unify its resizing code.  The lower-quality version is [all that's left](https://github.com/mono/SkiaSharp/issues/520).

And here's that [original image](https://github.com/bleroy/core-imaging-playground/blob/master/images/IMG_2445.jpg) for reference.

### Sharpening

Finally, MagicScaler performs a post-resizing sharpening step to compensate for the natural blurring that occurs when an image is resized.  Some of the other libraries would be capable of doing the same, but again, that would require extra code and would negatively impact the performance numbers.

<samp>
Sample Images:

| System.Drawing | &nbsp;&nbsp;MagicScaler&nbsp; | &nbsp;&nbsp;ImageSharp&nbsp;&nbsp; | &nbsp;&nbsp;Magick.NET&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;&nbsp;NetVips&nbsp;&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;FreeImage&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;SkiaSharp&nbsp;&nbsp; | &nbsp;&nbsp;&nbsp;ImageFlow&nbsp;&nbsp; |
|----------------|-------------|------------|------------|---------|-----------|-----------|-----------|
|<img src="/doc/images/sample-SystemDrawing.jpg" alt="System.Drawing" style="max-width: 150px" />|<img src="/doc/images/sample-MagicScaler.jpg" alt="MagicScaler" style="max-width: 150px" />|<img src="/doc/images/sample-ImageSharp.jpg" alt="ImageSharp" style="max-width: 150px" />|<img src="/doc/images/sample-MagickNET.jpg" alt="MagickNET" style="max-width: 150px" />|<img src="/doc/images/sample-NetVips.jpg" alt="NetVips" style="max-width: 150px" />|<img src="/doc/images/sample-FreeImage.jpg" alt="FreeImage" style="max-width: 150px" />|<img src="/doc/images/sample-SkiaSharpCanvas.jpg" alt="SkiaSharp" style="max-width: 150px" />|<img src="/doc/images/sample-ImageFlow.jpg" alt="ImageFlow" style="max-width: 150px" />|
</samp>

The linear light blending combined with the sharpening work to preserve more details from this [original image](https://github.com/bleroy/core-imaging-playground/blob/master/images/sample.jpg) than the other libraries do.  Again, some details are mangled by the poor JPEG settings, so MagicScaler's default settings would do even better.

Also of note is that ImageSharp's and NetVips' thumbnails for this image are roughly twice the size of the others because they have embedded the 3KiB sRGB color profile from the original image unnecessarily.

Versioning
----------

This project is using [semantic versioning](http://semver.org/).  Releases without a Preview/RC tag are considered release quality and are safe for production use. The major version number will remain at 0, however, until the APIs are complete and stabilized.

Contributing
------------

Contributions are welcome, but please open a new issue or discussion before submitting any pull requests that alter API or functionality.  This will hopefully save any wasted or duplicate effort.

License
-------

PhotoSauce is licensed under the [MIT](license) license.

[![NuGet](https://buildstats.info/nuget/PhotoSauce.MagicScaler)](https://www.nuget.org/packages/PhotoSauce.MagicScaler/) [![Build Status](https://dev.azure.com/saucecontrol/PhotoSauce/_apis/build/status/saucecontrol.PhotoSauce?branchName=master)](https://dev.azure.com/saucecontrol/PhotoSauce/_build/latest?definitionId=1&branchName=master) [![CI NuGet](https://img.shields.io/badge/nuget-CI%20builds-4da2db?logo=azure-devops)](https://dev.azure.com/saucecontrol/PhotoSauce/_packaging?_a=feed&feed=photosauce_ci)

MagicScaler
===========

High-performance image processing pipeline for .NET.  Implements best-of-breed algorithms, linear light processing, and sharpening for the best image resizing quality available.  Speed and efficiency are unmatched by anything else on the .NET platform.

## MagicScaler Performance

Benchmark results in this section come from the tests used in https://blogs.msdn.microsoft.com/dotnet/2017/01/19/net-core-image-processing/ -- updated to use current (Jan 2020) versions of the libraries and runtime.  The original benchmark project [is also on GitHub](https://github.com/bleroy/core-imaging-playground).

For these results, the benchmarks were modified to use a constant `UnrollFactor` so these runs more accurately report managed memory allocations and GC counts.  By default, BenchmarkDotNet runs slower benchmark methods with a smaller number of operations per iteration, meaning it can under-report allocation and GCs for those.  The constant `UnrollFactor` ensures all benchmarks' reported memory stats are based on the same run counts.

Benchmark environment:

``` ini
BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.100
  [Host]            : .NET Core 3.1.0 (CoreCLR 4.700.19.56402, CoreFX 4.700.19.56404), X64 RyuJIT
  .Net Core 3.1 CLI : .NET Core 3.1.0 (CoreCLR 4.700.19.56402, CoreFX 4.700.19.56404), X64 RyuJIT
```

### End-to-End Image Resizing

First up is a semi-real-world image resizing benchmark, in which 12 JPEGs of approximately 1-megapixel each are resized to 150px wide thumbnails and saved back as JPEG.

``` ini
Job=.Net Core 3.1 CLI  Toolchain=.NET Core 3.1  UnrollFactor=32  WarmupCount=5  

|                                 Method |      Mean |    Error |   StdDev | Ratio |     Gen 0 |    Gen 1 | Gen 2 |  Allocated |
|--------------------------------------- |----------:|---------:|---------:|------:|----------:|---------:|------:|-----------:|
|     *MagicScaler Load, Resize, Save(1) |  69.82 ms | 0.341 ms | 0.319 ms |  0.18 |         - |        - |     - |   77.04 KB |
|   System.Drawing Load, Resize, Save(2) | 378.48 ms | 3.185 ms | 2.980 ms |  1.00 |         - |        - |     - |    7.71 KB |
|       ImageSharp Load, Resize, Save(3) | 246.56 ms | 0.465 ms | 0.435 ms |  0.65 |  687.5000 |        - |     - | 3785.37 KB |
|      ImageMagick Load, Resize, Save(4) | 398.36 ms | 0.797 ms | 0.745 ms |  1.05 |         - |        - |     - |   51.03 KB |
|        ImageFree Load, Resize, Save(5) | 253.21 ms | 1.184 ms | 1.108 ms |  0.67 | 6000.0000 | 656.2500 |     - |   93.38 KB |
| SkiaSharp Canvas Load, Resize, Save(6) | 143.41 ms | 0.314 ms | 0.294 ms |  0.38 |  250.0000 |  62.5000 |     - | 1139.36 KB |
| SkiaSharp Bitmap Load, Resize, Save(6) | 143.21 ms | 0.206 ms | 0.192 ms |  0.38 |  250.0000 |        - |     - | 1153.85 KB |
|          NetVips Load, Resize, Save(7) | 139.27 ms | 0.819 ms | 0.766 ms |  0.37 |         - |        - |     - |   88.99 KB |
```

* (1) `PhotoSauce.MagicScaler` version 0.10.0.
* (2) `System.Drawing.Common` version 4.7.0.
* (3) `SixLabors.ImageSharp` version 1.0.0-dev003181.
* (4) `Magick.NET-Q8-AnyCPU` version 7.15.0.
* (5) `FreeImage.Standard` version 4.3.8.
* (6) `SkiaSharp` version 1.68.1.1.
* (7) `NetVips` version 1.1.0 with `NetVips.Native` (libvips) version 8.8.4.

Note that unmanaged memory usage is not measured by BenchmarkDotNet, nor is managed memory allocated but never released to GC (e.g. pooled objects/buffers).  See the [MagicScaler Efficiency](#magicscaler-efficiency) section for an analysis of total process memory usage for each library.

The performance numbers mostly speak for themselves, but some notes on image quality are warranted.  The benchmark suite saves the output so that the visual quality of the output of each library can be compared in addition to the performance.  See the [MagicScaler Quality](#magicscaler-quality) section below for details.

### Parallel End-to-End Resizing

This benchmark is the same as the previous but uses `Parallel.ForEach` to run the 12 test images in parallel.  It is meant to highlight cases where the libraries' performance doesn't scale up linearly with extra processors.

``` ini
Job=.Net Core 3.1 CLI  Toolchain=.NET Core 3.1  UnrollFactor=32  WarmupCount=5  

|                                         Method |      Mean |    Error |   StdDev | Ratio |     Gen 0 |    Gen 1 | Gen 2 |  Allocated |
|----------------------------------------------- |----------:|---------:|---------:|------:|----------:|---------:|------:|-----------:|
|     *MagicScaler Load, Resize, Save - Parallel |  18.85 ms | 0.283 ms | 0.265 ms |  0.12 |   31.2500 |        - |     - |  182.49 KB |
|   System.Drawing Load, Resize, Save - Parallel | 159.21 ms | 1.519 ms | 1.421 ms |  1.00 |         - |        - |     - |   32.74 KB |
|       ImageSharp Load, Resize, Save - Parallel |  66.95 ms | 0.698 ms | 0.653 ms |  0.42 |  625.0000 | 281.2500 |     - | 6652.07 KB |
|      ImageMagick Load, Resize, Save - Parallel | 112.41 ms | 1.596 ms | 1.415 ms |  0.71 |         - |        - |     - |   76.84 KB |
|        ImageFree Load, Resize, Save - Parallel |  68.38 ms | 0.721 ms | 0.674 ms |  0.43 | 3625.0000 | 375.0000 |     - |  117.29 KB |
| SkiaSharp Canvas Load, Resize, Save - Parallel |  38.51 ms | 0.471 ms | 0.440 ms |  0.24 |  281.2500 |  31.2500 |     - |  1165.1 KB |
| SkiaSharp Bitmap Load, Resize, Save - Parallel |  38.38 ms | 0.281 ms | 0.263 ms |  0.24 |  281.2500 |  31.2500 |     - |  1178.4 KB |
|          NetVips Load, Resize, Save - Parallel |  67.79 ms | 0.386 ms | 0.361 ms |  0.43 |         - |        - |     - |  116.92 KB |
```

Note the relative performance drop-off for NetVips.  It uses multiple threads for a single operation by default, making it scale up poorly and leaving it vulnerable to [thread oversubscription](https://docs.microsoft.com/en-us/archive/blogs/visualizeparallel/oversubscription-a-classic-parallel-performance-problem) problems under heavy server load.

### Resize-Only Synthetic Benchmark

This benchmark creates a blank image of 1280x853 and resizes it to 150x99, throwing away the result.  It is meant to separate the cost of decoding and encoding from the resizing part of the operation.  It doesn't represent a real-world scenario but can be useful when looking at relative performance and overhead.

``` ini
Job=.Net Core 3.1 CLI  Toolchain=.NET Core 3.1  UnrollFactor=256  WarmupCount=5  

|                  Method |      Mean |     Error |    StdDev | Ratio | RatioSD |    Gen 0 |   Gen 1 | Gen 2 | Allocated |
|------------------------ |----------:|----------:|----------:|------:|--------:|---------:|--------:|------:|----------:|
|     *MagicScaler Resize |  1.258 ms | 0.0094 ms | 0.0088 ms |  0.12 |    0.00 |        - |       - |     - |    2208 B |
|   System.Drawing Resize | 10.458 ms | 0.0954 ms | 0.0893 ms |  1.00 |    0.00 |        - |       - |     - |     136 B |
|       ImageSharp Resize |  5.701 ms | 0.0195 ms | 0.0173 ms |  0.54 |    0.00 |  31.2500 |       - |     - |  139440 B |
|      ImageMagick Resize | 44.802 ms | 0.0829 ms | 0.0775 ms |  4.28 |    0.04 |        - |       - |     - |    5560 B |
|        FreeImage Resize |  7.525 ms | 0.0182 ms | 0.0161 ms |  0.72 |    0.01 | 500.0000 | 54.6875 |     - |     136 B |
| SkiaSharp Canvas Resize |  2.032 ms | 0.0221 ms | 0.0207 ms |  0.19 |    0.00 |        - |       - |     - |    4234 B |
| SkiaSharp Bitmap Resize |  2.006 ms | 0.0055 ms | 0.0046 ms |  0.19 |    0.00 |        - |       - |     - |    6816 B |
|          NetVips Resize |  3.768 ms | 0.0202 ms | 0.0179 ms |  0.36 |    0.00 |   3.9063 |       - |     - |   20921 B |
```

Note that the NetVips benchmark case was modified to force it to use a unique image per iteration, as [suggested by the libvips author](https://github.com/bleroy/core-imaging-playground/pull/17#issuecomment-542381464).  By default, Vips caches results between iterations, meaning it doesn't actually perform the resize with the benchmark code as written.

Also note that the MagicScaler test case uses an actual test pattern rather than a blank image, so its output could be captured and evaluated.  Despite the fact that it does more work, it outperforms the other libraries handily.

## MagicScaler Efficiency

Raw speed isn't the only important factor when evaluating performance.  As demonstrated in the parallel benchmark results above, some libraries consume extra resources in order to produce a result quickly, at the expense of overall scalability.  Particularly when integrating image processing into another application, like a CMS or an E-Commerce site, it is important that your imaging library not steal resources from the rest of the system.  That applies to both processor time and memory.

BenchmarkDotNet does a good job of showing relative performance, and its managed memory diagnoser is quite useful for identifying excessive GC allocations, but its default configuration doesn't track actual processor usage or any memory that doesn't show up in GC collections.  For example, when it reports a time of 100ms on a benchmark, was that 100ms of a single processor at 100%?  More than one processor?  Less than 100%?  And what about memory allocated but never collected, like object caches and pooled arrays?  And what about unmanaged memory?  To capture these things, we must use different tools.

Because most of the libraries tested make calls to native libraries internally (ImageSharp is the only pure-managed library in the bunch), measuring only GC memory can be very misleading.  And even ImageSharp's memory usage isn't accurately reflected in the BDN `MemoryDiagnoser`'s numbers, because it holds allocated heap memory in the form of pooled objects and arrays.

In order to accurately measure both CPU time and total memory usage, I devised a more real-world test.  The 1-megapixel images in the benchmark test suite make for reasonable benchmark run times, but 1-megapixel is hardly representative of what we see coming from even smartphones now.  In order to stress the libraries a bit more, I replaced the input images in the benchmark app's input folder with the [Bee Heads album](https://www.flickr.com/photos/usgsbiml/albums/72157633925491877) from the USGS Bee Inventory flickr.  This collection contains 351 images (350 JPEG, 1 GIF), ranging in size from 2-22 megapixels, with an average of 13.4 megapixels.  The total album is just over 2.5 GiB in size, and it can be downloaded directly from flickr.

I re-used the image resizing code from the benchmark app but processed the test images only once, using `Parallel.ForEach` to load up the system.  Because of the test image set's size, startup and JIT overhead are overshadowed by the actual image processing, and although there may be some variation in times between runs, the overall picture is accurate and is more realistic than the BDN runs that cycle through the same small set of small images.  Each library's test was run in isolation so memory stats would include only that library.

This table shows the actual CPU time and peak memory usage as captured by the [Windows Performance Toolkit](https://docs.microsoft.com/en-us/windows-hardware/test/wpt/) when running the modified benchmark app on .NET Core 3.1 x64.

|                   Method | Peak Memory | VirtualAlloc Total |  CPU Time |
|------------------------- |------------:|-------------------:|----------:|
|   *MagicScaler Bee Heads |      373 MB |            4752 MB |  55701 ms |
| System.Drawing Bee Heads |     1082 MB |           36553 MB | 194308 ms |
|     ImageSharp Bee Heads |     8719 MB |           27920 MB | 195293 ms |
|    ImageMagick Bee Heads |      771 MB |           17394 MB | 288371 ms |
|      FreeImage Bee Heads |      670 MB |           16668 MB | 220011 ms |
|      SkiaSharp Bee Heads |      911 MB |           26450 MB | 135316 ms |
|        NetVips Bee Heads |     3278 MB |            3428 MB |  88533 ms |

A few of the more interesting points from the above numbers:

ImageSharp shows relatively low allocations and GC counts in the BDN managed memory diagnoser, but it is clear from the WPT trace that it is allocating and never releasing large amounts of memory.  In a repetitive benchmark, this might not be obvious, but when working on a larger number of larger images, it will put a major strain on memory.

Similarly, Vips uses huge amounts of memory without releasing it.  The ratio of peak memory to total allocated memory indicates that it is doing aggressive caching.  This also would not show up as well in a repetitive benchmark, but it could negatively impact anything sharing memory space with it.

It's clear from the CPU time numbers that System.Drawing is spending a fair amount of its time idle.  Its total consumed time is roughly the same as ImageSharp's, but its wall clock time shows it to be roughly 4x slower.

And it's clear that MagicScaler is the most efficient by far in terms of both memory and CPU time.

Because the peak memory numbers for some libraries are so high, it's also worth looking at how they perform in 32-bit environment, which is naturally memory-constrained.  The following table shows results of the same test for .NET Core 3.1 x86.

|                   Method | Peak Memory | VirtualAlloc Total |  CPU Time |
|------------------------- |------------:|-------------------:|----------:|
|   *MagicScaler Bee Heads |      409 MB |            4746 MB |  63937 ms |
| System.Drawing Bee Heads |      969 MB |           36503 MB | 209003 ms |
|     ImageSharp Bee Heads |  FAIL - OOM |                    |           |
|    ImageMagick Bee Heads |      721 MB |           17342 MB | 367823 ms |
|      FreeImage Bee Heads |      667 MB |           16685 MB | 250278 ms |
|      SkiaSharp Bee Heads |      941 MB |           26418 MB | 155130 ms |
|        NetVips Bee Heads |  FAIL - OOM |                    |           |

As one might expect, ImageSharp and NetVips both failed to complete the test on 32-bit .NET Core.  ImageSharp failed almost immediately, while NetVips made it just over halfway through the test.

Once again, MagicScaler is the most efficient with memory and CPU.  All libraries that succeeded took longer in 32-bit mode, but ImageMagick was disproportionately slower.

For the record, here are the exceptions thrown by the failing libraries:

ImageSharp
```
System.OutOfMemoryException: Exception of type 'System.OutOfMemoryException' was thrown.
   at System.Buffers.ConfigurableArrayPool`1.Rent(Int32 minimumLength)
   at SixLabors.Memory.ArrayPoolMemoryAllocator.Allocate[T](Int32 length, AllocationOptions options)
   ...
```

NetVips
```
NetVips.VipsException: unable to call jpegsave
VipsJpeg: Insufficient memory (case 4)

   at NetVips.Operation.Call(String operationName, VOption kwargs, Image matchImage, Object[] args)
   at NetVips.Image.Jpegsave(String filename, Nullable`1 pageHeight, Nullable`1 q, String profile, Nullable`1 optimizeCoding, Nullable`1 interlace, Nullable`1 noSubsample, Nullable`1 trellisQuant, Nullable`1 overshootDeringing, Nullable`1 optimizeScans, Nullable`1 quantTable, Nullable`1 strip, Double[] background)
   ...
```

## MagicScaler Quality

The benchmark application detailed above saves the output from its end-to-end image resizing tests so they can be evaluated for file size and image quality.  The images were contributed by @bleroy, who created the original benchmark.  They were chosen because of their high-frequency detail which had been observed to be challenging for some image resampling algorithms.  The images have a couple of other interesting properties as well.

First, 10 of the 12 images are saved in the Abobe RGB color space, with an embedded color profile.  Second, they have a significant amount of metadata related to the source camera and their processing history.  Third, one of the images is in the sRGB color space and contains an embedded profile for sRGB, which is relatively large.  These factors combine to highlight some of the shortcomings in the tested libraries.

There is one design flaw in the benchmark app that makes it more difficult to judge the output quality, however.  The tests were configured to save the thumbnails with a low JPEG quality value and to use 4:2:0 [chroma subsampling](https://en.wikipedia.org/wiki/Chroma_subsampling).  While these values are acceptable or even ideal for large images, they will cause compression artifacts in areas of small details.  At thumbnail size, the *only* details are small details, so it is better to use a higher-quality JPEG setting for very small images.  MagicScaler performs this adjustment automatically under its default settings, but those are overridden in the benchmark suite to be consistent with the other libraries.

Note that outside the ill-advised overrides to JPEG quality settings, the results discussed here are from MagicScaler's default settings.  Not only is MagicScaler faster and more more efficient than the other libraries, it is also easier to use.  You'll get better quality with its defaults than can be obtained with lots of extra code in other libraries.

### Color Management

Handling images in a color space other than sRGB can be a challenge, and it's not something most developers are familiar with.  There are essentially 3 ways to approach an image with an embedded color profile:

1) Convert the image to sRGB.  This has the advantage that any downstream software that reads the processed image does not need to be color managed to display it correctly.  This was historically a problem with many apps, including popular web browsers (you can [test yours here](https://chromachecker.com/webbrowser/en/manual)).  The disadvantage is that it takes extra processing to do this conversion.
2) Preserve the color space by embedding the ICC profile in the output image.  This is basically the opposite of option 1).  It's cheaper to do but may result in other software mangling the colors later.  It also results in a larger file because the profile may be very large -- in the case of a thumbnail, it might double the file size or more.
3) Ignore the color profile and treat the image as if it's encoded as sRGB.  This option is absolutely incorrect and would put your software in the category of color manglers mentioned above.

The libraries tested in the benchmark have different capabilities, so the options available depend on the library.

* System.Drawing supports options 1) and 3) but does 3) by default.  The original version of the benchmark did it that way, until I submitted a PR later to correct it to do 1).  This resulted in correct colors but a drop in speed.
* ImageSharp can do 2) or 3) and does 2) by default.  When it was originally integrated into the benchmark, however, it only did 3).
* ImageMagick can do any of the options above but does 2) by default.  However, it also preserves all other metadata by default, and the test images have quite a lot of metadata.  This results in thumbnails that are extremely oversized and consist of roughly 90% metadata.  For this reason, the ImageMagick tests were written to strip all metadata, resulting in behavior 3).
* FreeImage works the same way as ImageMagick by default, and its test was implemented the same way.  It could have done option 2), but it was implemented to do option 3).
* Skia does option 3) by default but can be made to do option 1) with a lot of [extra code](https://skia.org/user/sample/color?cl=9919).  This was not done in the benchmark tests.
* MagicScaler can do any of the above options but does option 1) by default.  I contributed the MagicScaler test in the benchmark myself.  It has been correct from the beginning.

The net result is that if you look at the sample image output in @bleroy's [blog post](https://devblogs.microsoft.com/dotnet/net-core-image-processing/), the MagicScaler output has different colors than all the others.  10 of the 12 images have washed-out colors in the output from the other libraries -- most apparent in the vibrant red, green, and blue hues, such as the snake or the Wild River ride on the back of what is now the [MoPOP building](https://www.mopop.org/building).  If you download the project today and run it, the outputs from System.Drawing (corrected by me), ImageSharp (fixed in the library), and MagicScaler will all have correct colors, and the rest will be wrong.

The recently-added NetVips test has the same problem as ImageMagick.  It would do option 2) by default but would carry all the metadata with it and so has been written to do 3).  Like ImageMagick, it could be modified to do option 1) instead, but that would require much more code and would be significantly slower.

These are common mistakes made by developers starting out with image processing, because it can be easy to miss the shift in colors and difficult to discover how to do the right thing.

Sample Images:

| System.Drawing | MagicScaler | ImageSharp | Magick.NET | NetVips | FreeImage | SkiaSharp |
|----------------|-------------|------------|------------|---------|-----------|-----------|
|<img src="/doc/images/IMG_2525-SystemDrawing.jpg" alt="System.Drawing" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-MagicScaler.jpg" alt="MagicScaler" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-ImageSharp.jpg" alt="ImageSharp" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-MagickNET.jpg" alt="MagickNET" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-NetVips.jpg" alt="NetVips" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-FreeImage.jpg" alt="FreeImage" style="max-width: 150px" />|<img src="/doc/images/IMG_2525-SkiaSharpCanvas.jpg" alt="SkiaSharp" style="max-width: 150px" />|

The color difference between these should be obvious.  Compared to the [original image](https://github.com/bleroy/core-imaging-playground/blob/master/images/IMG_2525.jpg), it's easy to see which are correct (unless your browser is busted).

### Gamma-Corrected Blending

Of the libraries tested in the benchmark, only MagicScaler performs the resampling step in [linear light](http://www.imagemagick.org/Usage/resize/#resize_colorspace).  ImageSharp, ImageMagick, and Vips are capable of processing in linear light but would require extra code to do so and would perform significantly worse.

Sample Images:

| System.Drawing | MagicScaler | ImageSharp | Magick.NET | NetVips | FreeImage | SkiaSharp |
|----------------|-------------|------------|------------|---------|-----------|-----------|
|<img src="/doc/images/IMG_2301-SystemDrawing.jpg" alt="System.Drawing" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-MagicScaler.jpg" alt="MagicScaler" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-ImageSharp.jpg" alt="ImageSharp" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-MagickNET.jpg" alt="MagickNET" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-NetVips.jpg" alt="NetVips" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-FreeImage.jpg" alt="FreeImage" style="max-width: 150px" />|<img src="/doc/images/IMG_2301-SkiaSharpCanvas.jpg" alt="SkiaSharp" style="max-width: 150px" />|

In addition to keeping the correct colors, MagicScaler does markedly better at preserving image highlights because of the linear light blending.  Notice the highlights on the flowers are a better representation of those in the [original image](https://github.com/bleroy/core-imaging-playground/blob/master/images/IMG_2301.jpg)

### High-Quality Resampling

Most imaging libraries have at least some capability to do high-quality resampling, but not all do.  MagicScaler defaults to high-quality, but the other libraries in this test were configured for their best quality as well.

Sample Images:

| System.Drawing | MagicScaler | ImageSharp | Magick.NET | NetVips | FreeImage | SkiaSharp |
|----------------|-------------|------------|------------|---------|-----------|-----------|
|<img src="/doc/images/IMG_2445-SystemDrawing.jpg" alt="System.Drawing" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-MagicScaler.jpg" alt="MagicScaler" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-ImageSharp.jpg" alt="ImageSharp" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-MagickNET.jpg" alt="MagickNET" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-NetVips.jpg" alt="NetVips" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-FreeImage.jpg" alt="FreeImage" style="max-width: 150px" />|<img src="/doc/images/IMG_2445-SkiaSharpCanvas.jpg" alt="SkiaSharp" style="max-width: 150px" />|

FreeImage and SkiaSharp have particularly poor image quality in this test, with output substantially more blurry than the others.

Note that when the benchmark and blog post were originally published, Skia supported multiple ways to resize images, which is why there are two Skia benchmark tests.  Under the current version of Skia, those two versions have the same benchmark numbers and same output quality, because the Skia internals have been changed to unify its resizing code.  The lower-quality version is [all that's left](https://github.com/mono/SkiaSharp/issues/520).

And here's that [original image](https://github.com/bleroy/core-imaging-playground/blob/master/images/IMG_2445.jpg) for reference.

### Sharpening

Finally, MagicScaler performs a post-resizing sharpening step to compensate for the natural blurring that occurs when an image is resized.  Some of the other libraries would be capable of doing the same, but again, that would require extra code and would negatively impact the performance numbers.

Sample Images:

| System.Drawing | MagicScaler | ImageSharp | Magick.NET | NetVips | FreeImage | SkiaSharp |
|----------------|-------------|------------|------------|---------|-----------|-----------|
|<img src="/doc/images/sample-SystemDrawing.jpg" alt="System.Drawing" style="max-width: 150px" />|<img src="/doc/images/sample-MagicScaler.jpg" alt="MagicScaler" style="max-width: 150px" />|<img src="/doc/images/sample-ImageSharp.jpg" alt="ImageSharp" style="max-width: 150px" />|<img src="/doc/images/sample-MagickNET.jpg" alt="MagickNET" style="max-width: 150px" />|<img src="/doc/images/sample-NetVips.jpg" alt="NetVips" style="max-width: 150px" />|<img src="/doc/images/sample-FreeImage.jpg" alt="FreeImage" style="max-width: 150px" />|<img src="/doc/images/sample-SkiaSharpCanvas.jpg" alt="SkiaSharp" style="max-width: 150px" />|

The linear light blending combined with the sharpening work to preserve more details from this [original image](https://github.com/bleroy/core-imaging-playground/blob/master/images/sample.jpg) than the other libraries do.  Again, some details are mangled by the poor JPEG settings, so MagicScaler's default settings would do even better.

Also of note is that ImageSharp's thumbnail for this image is roughly twice the size of the others because it has embedded the 3KiB sRGB color profile from the original image unnecessarily.


Requirements
------------

MagicScaler currently runs only on Windows.  Although MagicScaler is compatible with (and optimized for) .NET Core, it requires the [Windows Imaging Component](https://docs.microsoft.com/en-us/windows/desktop/wic/-wic-about-windows-imaging-codec) to function.  Work is in progress to allow MagicScaler to run on other platforms.

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

Contributions are welcome, but please open a new issue for discussion before submitting any significant pull requests.  This will hopefully save any wasted or duplicate efforts.

License
-------

PhotoSauce is licensed under the [MIT](license) license.

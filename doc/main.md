## MagicImageProcessor

The main MagicScaler image processor.

### EnablePlanarPipeline: static bool

Enables MagicImageProcessor's planar format pipeline for images stored in planar ([YCbCr](https://en.wikipedia.org/wiki/YCbCr)) format.  This pipeline offers significant performance advantages over standard RGB processing for JPEG images.

Most image processing software will convert YCbCr JPEG input to RGB for processing and then convert back to YCbCr for JPEG output.  In addition to saving the processing time spent on this unnecessary conversion, planar processing allows for other work savings.

* [Chroma subsampled](https://en.wikipedia.org/wiki/Chroma_subsampling) images do not need to have their chroma planes upsampled to the luma size, only to have them rescaled again.  MagicScaler can scale the subsampled chroma plane directly to its final size.
* When saving in a chroma subsampled format, the final chroma scaling is done with a high-quality resampler rather than the Linear resampler used by the encoder.
* When processing in [linear light](http://web.archive.org/web/20160826144709/http://www.4p8.com/eric.brasseur/gamma.html), gamma correction needs only be performed on the luma plane.
* When sharpening is applied, it also needs only be performed on the luma plane.

This feature is only available if WIC supports it (Windows 8.1/Windows Server 2012 and above) and the input image format is YCbCr.  The output results will be slightly different than those produced with RGB processing but no less correct or visually appealing.

Default Value: true

### EnableSimd : static bool

Enables [SIMD](https://en.wikipedia.org/wiki/SIMD) versions of MagicScaler's image convolution and matting/compositing algorithms.  For high-quality resampling, SIMD processing yields significant performance improvements.  This is most notable with linear light processing, which now has no performance penalty compared with sRGB processing.

If processing predominately with low-quality resampling or in sRGB blending mode, there can be a slight performance penalty for SIMD processing.  You can disable this if your use cases do not follow the high-quality MagicScaler defaults. 

Note that the SIMD processing is done in floating point whereas the standard processing is done in fixed point math.  This will result in very slight output differences due to rounding.  Differences will not be visually significant but can be detected with a binary compare.

Default Value: true if the runtime/JIT and hardware support hardware-accelerated System.Numerics.Vectors, otherwise false

### ProcessImage(string, Stream, ProcessImageSettings)

Accepts a file path for the input image, a stream for the output image, and a [ProcessImageSettings](#processimagesettings) object for settings.  The output stream must allow Seek and Write.  Returns a [ProcessImageResult](#processimageresult).

### ProcessImage(ArraySegment&lt;byte&gt;, Stream, ProcessImageSettings)

Accepts a byte ArraySegment for the input image, a stream for the output image, and a [ProcessImageSettings](#processimagesettings) object for settings.  The output stream must allow Seek and Write.  Returns a [ProcessImageResult](#processimageresult).

### ProcessImage(Stream, Stream, ProcessImageSettings)

Accepts a stream for the input image, a stream for the output image, and a [ProcessImageSettings](#processimagesettings) object for settings.  The output stream must allow Seek and Write.  The input stream must allow Seek and Read.  Returns a [ProcessImageResult](#processimageresult).

### ProcessImage(IPixelSource, Stream, ProcessImageSettings)

Accepts an [IPixelSource](#ipixelsource) input, a stream for the output image, and a [ProcessImageSettings](#processimagesettings) object for settings.  The output stream must allow Seek and Write.  Returns a [ProcessImageResult](#processimageresult).

### BuildPipeline(string, ProcessImageSettings)

Accepts a file path for the input image and a [ProcessImageSettings](#processimagesettings) object for settings.  Returns a [ProcessingPipeline](#processingpipeline) for custom processing.

### BuildPipeline(ArraySegment&lt;byte&gt;, ProcessImageSettings)

Accepts a byte ArraySegment for the input image and a [ProcessImageSettings](#processimagesettings) object for settings.  Returns a [ProcessingPipeline](#processingpipeline) for custom processing.

### BuildPipeline(Stream, ProcessImageSettings)

Accepts a stream for the input image and a [ProcessImageSettings](#processimagesettings) object for settings.  Returns a [ProcessingPipeline](#processingpipeline) for custom processing.  The input stream must allow Seek and Read.

### BuildPipeline(IPixelSource, ProcessImageSettings)

Accepts an [IPixelSource](#ipixelsource) input and a [ProcessImageSettings](#processimagesettings) object for settings.  Returns a [ProcessingPipeline](#processingpipeline) for custom processing.

### ExecutePipeline(ProcessingPipeline, Stream)

Accepts a [ProcessingPipeline](#processingpipeline) and a stream for the output image.  This method completes pipeline processing by requesting the processed pixels and saving the output.  The output stream must allow Seek and Read.  Returns a [ProcessImageResult](#processimageresult).

## GdiImageProcessor

This class is included only for testing/benchmarking purposes.  It will be removed in a future version and should not be used for production code.

This class is not included in the .NET Core version.

## WicImageProcessor

This class is included only for testing/benchmarking purposes.  It will be removed in a future version and should not be used for production code.

## ProcessImageSettings

Settings for the ProcessImage operation

### FrameIndex: int

The frame number (starting from 0) to read from a multi-frame file, such as a multi-page TIFF or animated GIF.  For single-frame images, 0 is the only valid value.

Default Value: 0

### Width: int

The output image width in pixels.  If auto-cropping is enabled, a value of 0 will set the width automatically based on the output height.  If Width and Height are both set to 0, no resizing will be performed but a crop may still be applied.

Default Value: 0

### Height: int

The output image height in pixels.  If auto-cropping is enabled, a value of 0 will set the height automatically based on the output width.  If Width and Height are both set to 0, no resizing will be performed but a crop may still be applied.

Default Value: 0

### DpiX: double

The output image horizontal DPI.  A value of 0 will preserve the DPI of the input image.  Not all image formats support a DPI setting and most applications will ignore it.

Default Value: 96

### DpiY: double

The output image vertical DPI.  A value of 0 will preserve the DPI of the input image.  Not all output formats support a DPI setting and most applications will ignore it.

Default Value: 96

### Sharpen: bool

Indicates whether an [unsharp mask](https://en.wikipedia.org/wiki/Unsharp_masking) operation should be performed on the image following the resize.  The sharpening settings are controlled by the [UnsharpMask](#unsharpmask-unsharpmasksettings) property.

Default value: true

### ResizeMode: CropScaleMode

A [CropScaleMode](#cropscalemode) value indicating whether auto-cropping should be performed or whether the resized image may have a different aspect ratio.  Auto-cropping is performed only if a [Crop](#crop-rectangle) value is not explicitly set.

Default value: Crop

### Crop: Rectangle

A System.Drawing.Rectangle that specifies which part of the input image should be included.  If the rectangle is empty and the [ResizeMode](#resizemode-cropscalemode) is set to `Crop`, the image will be cropped automatically.  Points given for this rectangle must be expressed in terms of the input image.

If the input image has an [Exif Orientation](http://sylvana.net/jpegcrop/exif_orientation.html) tag, rotation and/or flipping will be applied to the image before the crop.  Crop values should be expressed in terms of the image's correct orientation, not the encoded orientation.

Default value: Rectangle.Empty

### Anchor: CropAnchor

A [CropAnchor](#cropanchor) value indicating the position of the auto-crop rectangle.  Values may be combined to specify a vertical and horizontal position.  Ex: `myCrop = CropAnchor.Top | CropAnchor.Left`.

Default value: CropAnchor.Center

### SaveFormat: FileFormat

A [FileFormat](#fileformat) value indicating the codec used for the output image.  A value of `Auto` will choose the output codec based on the input image type.

Default value: FileFormat.Auto

### MatteColor: Color

A System.Drawing.Color value indicating a background color to be applied when processing an input image with transparency.  When converting to a file format that does not support transparency (e.g. PNG->JPEG), the background color will be Black unless otherwise specified.  When saving as a file format that does support transparency, the transparency will be maintained unless a color is set.

Default value: Color.Empty

### HybridMode: HybridScaleMode

A [HybridScaleMode](#hybridscalemode) value indicating whether hybrid processing is allowed.  Hybrid processing may use the image decoder or another low-quality scaler to shrink an image to an intermediate size before the selected high-quality algorithm is applied to the final resize.  This can result in dramatic performance improvements but with a reduction in image quality.

Default value: HybridScaleMode.FavorQuality

### BlendingMode: GammaMode

A [GammaMode](#gammamode) value indicating whether the scaling algorithm is applied in linear or gamma-corrected colorspace.  Linear processing will yield better quality in almost all cases but with a performance cost.

Default value: GammaMode.Linear

### MetadataNames: IEnumerable&lt;string&gt;

A list of metadata policy names or explicit metadata paths to be copied from the input image to the output image.  This can be useful for preserving author or copyright EXIF tags in the output image.  See the [Windows Photo Metadata Policies](https://msdn.microsoft.com/en-us/library/windows/desktop/ee872003(v=vs.85).aspx) for examples of commonly-used values, or the [Metadata Query Language Overview](https://msdn.microsoft.com/en-us/library/windows/desktop/ee872003(v=vs.85).aspx) for explicit path syntax.

Default value: null

### JpegQuality: int

Sets the quality value passed to the JPEG encoder for the output image.  If this value is set to 0, the quality level will be set automatically according to the output image dimensions.  Typically, this value should be 80 or greater if set explicitly.

Default value: 0

### JpegSubsampleMode: ChromaSubsampleMode

A [ChromaSubsampleMode](#chromasubsamplemode) value indicating how [chroma subsampling](https://en.wikipedia.org/wiki/Chroma_subsampling) is configured in the JPEG encoder for the output image.  If this value is set to `Default`, the chroma subsampling will be set automatically based on the [JpegQuality](#jpegquality-int) setting.

Default value: ChromaSubsampleMode.Default

### Interpolation: InterpolationSettings

An [InterpolationSettings](#interpolationsettings) object specifying details of the sampling algorithm to use for image scaling.  If this value is unset, the algorithm will be chosen automatically based on the ratio of input image size to output image size.

Default value: unset

### UnsharpMask: UnsharpMaskSettings

An [UnsharpMaskSettings](#unsharpmasksettings) object specifying sharpening settings.  If this value is unset, the settings will be chosen automatically based on the ratio of input image size to output image size.

Default value: unset

## CropAnchor 

A flags enumeration for specifying auto-crop anchor.

By default, auto-cropping will maintain the image center by cropping equally from the top, bottom, or sides.  If you wish to direct the auto-cropper to focus on another part of the image, you may specify a vertical and horizontal bias using a combination of values.  Only one horizontal and one vertical value may be combined.

* Center
* Top
* Bottom
* Left
* Right

## CropScaleMode

An enumeration for specifying auto-crop or auto-size behavior.

### Crop

Auto-crop the input image to fit within the given Width and Height while maintaining the aspect ratio of the input image.

### Max

Auto-size the output image to a maximum of the values given for Width and Height while maintaining aspect ratio of the input image.

### Stretch

Allow the output image Width and Height to change the aspect ratio of the input image.  This may result in stretching or distortion of the image.

### Examples

Suppose you have an input image with dimensions of 640x480 and you set the Width and Height of the output image to 100x100.
`Crop` will produce an output image of 100x100, preserving the aspect ratio of the input image by cropping from the sides of the image.  By default, this will crop evenly from the left and right.  You can change that behavior by changing the [Anchor](#anchor-cropanchor) value.
`Max` will produce an output image of 100x75, preserving the aspect ratio of the input image by constraining the dimensions of the output image.
`Stretch` will produce an output image of 100x100 that is squished horizontally.

When using `Crop` mode, you may also choose to specify only one of the Width or Height.  In this case, the undefined dimension will be set automatically to preserve the source image's aspect ratio after taking the Crop setting into account.
Again, using a 640x480 input image as an example, you can expect the following:

Width=100 with the default values Height=0/Crop=Rectangle.Empty will result in an output image of 100x75 with no cropping.
Height=100 with the default values Width=0/Crop=Rectangle.Empty will result in an output image of 133x100 with no cropping.
Width=100/Crop=Rectangle.FromLTRB(0,0,480,480) with the default value Height=0 will result in an output image of 100x100 with the right portion of the image cropped.  You can achieve the same result with Width=100/Height=100/Anchor=CropAnchor.Left.

## HybridScaleMode

An enumeration for specifying the amount of low-quality scaling performed in high-ratio resizing operations.

### FavorQuality

Resize the image to an intermediate size at least 3x the output dimensions with the low-quality scaler.  Perform the final resize with the high-quality scaler.

### FavorSpeed

Resize the image to an intermediate size at least 2x the output dimensions with the low-quality scaler.  Perform the final resize with the high-quality scaler.

### Turbo

Resize the image entirely using the low-quality scaler if possible.  If not possible, perform the minimal amount of work possible in the high-quality scaler.

### Off

Perform the entire resize with the high-quality scaler.  This will yield the best quality image but at a performance cost.

## GammaMode

An enumeration for specifying the light blending mode used for high-quality scaling operations. 

### Linear

Perform the high-quality scaling in [linear light](http://web.archive.org/web/20160826144709/http://www.4p8.com/eric.brasseur/gamma.html) colorspace.  This will give better results in most cases but at a performance cost.

### sRGB

Perform the high-quality scaling in gamma-corrected sRGB colorspace.  This will yield output more similar to other scaling software but will be less correct in most cases.

## ChromaSubsampleMode

An enumeration for specifying the [chroma subsampling](https://en.wikipedia.org/wiki/Chroma_subsampling) mode used by the JPEG encoder. 

### Default

Choose the chroma subsampling mode automatically based on the JpegQuality setting

### Subsample420

4:2:0 chroma subsampling

### Subsample422

4:2:2 chroma subsampling

### Subsample444

No chroma subsampling (4:4:4)

## FileFormat

An enumeration for specifying the file format (codec) used for the output image. 

### Auto

Choose the output file format automatically based on the format of the input image

### Jpeg

JPEG.  Use JpegQuality and JpegSubsampleMode settings to control output.

### Png

24-bit or 32-bit PNG, depending on whether or not the input image contains an alpha channel.

### Png8

8-bit indexed PNG

### Gif

8-bit indexed GIF

### Bmp

24-bit or 32-bit BMP, depending on whether or not the input image contains an alpha channel.

### Tiff

Uncompressed 24-bit or 32-bit TIFF, depending on whether or not the input image contains an alpha channel.

## UnsharpMaskSettings

A structure for specifying the settings used for the post-resize [sharpening](https://en.wikipedia.org/wiki/Unsharp_masking) of the output image.  These settings are designed to function similarly to the Unsharp Mask settings in Photoshop.

### Amount: int

The amount of sharpening applied.  Technically, this is a percentage applied to the luma difference between the original and blurred images.  Typical values are between 25 and 200.

### Radius: double

The radius of the gaussian blur used for the unsharp mask.  More blurring in the mask yields more sharpening in the final image.  Typical values are between 0.3 and 3.0.  Larger radius values can have significant performance cost.

### Threshold

The minimum difference between the original and blurred images for a pixel to be sharpened.  When using larger `Radius` or `Amount` values, a larger `Threshold` value can ensure lines are sharpened while textures are not. Typical values are between 0 and 10.

## InterpolationSettings

A structure for specifying the sampling algorithm used by the high-quality scaler.  There are a number of well-known algorithms preconfigured as static fields, or you can define your own.

Preconfigured values include:

* [NearestNeighbor](http://www.imagemagick.org/Usage/filter/#point) - AKA Point
* [Average](http://www.imagemagick.org/Usage/filter/#box) - AKA Box
* [Linear](http://www.imagemagick.org/Usage/filter/#triangle) - AKA Bilinear/Triangle/Tent
* [Quadratic](http://neildodgson.com/pubs/quad.pdf) - A slightly faster alternative to Catmull-Rom with similar output but a greater chance of artifacts
* [Hermite](http://www.imagemagick.org/Usage/filter/#hermite) - A Cubic filter with B=0, C=0 - Similar to Linear but with slightly smoother output
* [Mitchell](http://www.imagemagick.org/Usage/filter/#mitchell) - AKA Mitchell-Netravali - A Cubic filter with B=1/3, C=1/3
* [CatmullRom](http://www.imagemagick.org/Usage/filter/#catrom-c) - AKA Catrom - A Cubic filter with B=0, C=0.5
* [Cubic](http://www.imagemagick.org/Usage/filter/#cubics) - A Cubic filter with B=0, C=1 - Similar to GDI+'s HighQualityBicubic for high-ratio downscaling
* CubicSmoother - A Cubic filter with B=0, C=0.625 and Blur=1.15 - Similar to Photoshop's Bicubic Smoother and GDI+'s HighQualityBicubic when enlarging
* [Lanczos](http://www.imagemagick.org/Usage/filter/#lanczos) - A 3-lobed Lanczos Windowed Sinc filter
* [Spline36](http://www.panotools.org/dersch/interpolator/interpolator.html) - A 3-lobed piece-wise function with a nice balance between smoothness and sharpness

### WeightingFunction: IInterpolator

A reference to an object implementing IInterpolator, which specifies the sampling range for the filter and a function to generate the weighting value for a given distance.

### Blur: double

A value used to stretch (or compress) the sampling range for the filter.  The default and recommended value is 1.  You may use a value greater than 1 to blur or smooth the sampling function.  Values less than 1 can cause unpleasant artifacts.

## IInterpolator

An interface for defining custom sampling functions.

### Support: double

The support radius of the sampling function.  The sampling window will be twice this value.

### GetValue(double) returns double

The weighting function.  This function accepts a distance from the destination sample's center and returns a weight for the sample at that distance.

## ProcessImageResult

This class encapsulates information about the results of an image processing operation.  It includes the settings used for the processing as well as some basic instrumentation.

### Settings: ProcessImageSettings

The settings used for the processing operation.  If any settings supplied in the input were set to automatic values, this object will reflect the calculated values used.

### Stats: IEnumerable&lt;PixelSourceStats&gt;

A collection of [PixelSourceStats](#pixelsourcestats) objects containing information about the number of pixels processed and time taken in each stage of the pipeline.

## PixelSourceStats

This class encapsulates basic instrumentation for the [IPixelSource](#ipixelsource) used in each stage of the processing pipeline.

### CallCount: int

The number of times CopyPixels() was called on this pixel source.

### PixelCount: int

The total number of pixels requested from the pixel source.

### ProcessingTime: double

The total processing time (in milliseconds) for this pixel source.  Some WIC wrappers may report times that include the processing of chained pixel sources they use and may not, therefore report times accurately.  This information is most useful for diagnosing performance issues in your own pixel sources or transforms.

### SourceName: string

The name of the pixel source as returned by ToString().  The default ToString() implementation returns the class name, but this may be overridden, so the output may not make sense.

## IPixelSource

This interface defines a custom pixel source.  It can either retrieve those pixels from an upstream source or can generate them itself.  For a sample implementation, refer to the [TestPatternPixelSource](../src/MagicScaler/Magic/TestPatternPixelSource.cs) code.

### Width: int

The width of the source image in pixels.

### Height: int

The height of the source image in pixels.

### Format: Guid

The [WIC pixel format](https://msdn.microsoft.com/en-us/library/windows/desktop/ee719797.aspx) GUID of the source image.  For the current version, this must be a value included as one of the [PixelFormats](#pixelformats) static fields.

### CopyPixels(Rectangle, long, long, IntPtr)

Copy pixels from the source image.  The caller will provide a rectangle specifying an area of interest and an IntPtr that points to a destination byte buffer as well as a stride and buffer length.  If writing a custom IPixelSource, it is your responsibility to implement this method to provide pixels to downstream sources and transforms.

## PixelFormats

This static class contains member fields for each of the supported WIC pixel format GUIDs.

### Bgr24bpp

24 bits-per-pixel.  8 bits-per-channel in BGR channel order.

### Bgra32bpp

32 bits-per-pixel.  8 bits-per-channel in BGR channel order, plus an 8-bit alpha channel.

### Grey8bpp

8 bits-per-pixel greyscale.

## IPixelTransform : IPixelSource

This interface inherits from [IPixelSource](#ipixelsource) and is intended to be used to build a custom processing step.  For a sample implementation, refer to the [ColorMatrixTransform](../src/MagicScaler/Magic/ColorMatrixTransform.cs) code.

### Init(IPixelSource)

The pipeline will call this method and pass in the upstream pixel source.  Use this method to configure the transform according to its own settings and the properties of the upstream source.

## ProcessingPipeline

This class represents an image processing pipeline that is configured but not yet executed.  This allows for further customization of the processing, such as filtering, before the pipeline is executed and the output image saved.  It also allows pixels to be pulled from the pipeline rather than written directly to an output image.

### PixelSource: IPixelSource

The [IPixelSource](#ipixelsource) representing the current last step of the pipeline.  The image properties and CopyPixels() results reflect all processing that has been configured up to the current point in the pipeline.

### Settings: ProcessImageSettings

The [ProcessImageSettings](#processimagesettings) object representing the current state of the settings on this pipeline.  Any settings configured with automatic values initially will have their calculated values at this point.

### Stats: IEnumerable&lt;PixelSourceStats&gt;

A collection of the [PixelSourceStats](#pixelsourcestats) for all processing up to this point.  Until CopyPixels() is called on the PixelSource, there will be no stats to report.

### AddTransform(IPixelTransform)

Adds an [IPixelTransform](#ipixeltransform) to the pipeline.  This allows for custom filtering of the pipeline output before the final image is saved.

### ExecutePipeline(Stream)

This extension method on the [MagicImageProcessor](#magicimageprocessor) requests all pixels from the pipeline and saves the output to a stream.  You may use this after configuring the pipeline (e.g. adding filters) if you do not wish to request the pixels from the PixelSource.

## ImageFileInfo

This class reads and exposes basic information about an image file from its header.

### ContainerType: FileFormat

The [FileFormat](#fileformat) of the image.  This is based on the contents of the image, not its file name/extension.

### FileDate: DateTime

The last modified date of the image file, if available.

### FileSize: int

The size of the image file, in bytes, if available.

### Frames: FrameInfo[]

An array of [FrameInfo](#frameinfo) objects, with details about each image frame in the file.

## FrameInfo

This class describes a single image frame from within an image file.  All valid images have at least one frame, some (e.g. TIFF and GIF) may have many.

### Width: int

The width of the image frame in pixels.

### Height: int

The height of the image frame in pixels.

### HasAlpha: bool

True if the image frame has a separate alpha channel or if it is in an indexed format and any of the color values have a transparency value set.

### ExifOrientation: Orientation

The [Exif Orientation](http://www.impulseadventure.com/photo/exif-orientation.html) value stored for this image frame.  The Width and Height values are pre-corrected according to this value, so you can ignore it if you are using MagicScaler to process the image, as it performs orientation correction automatically.  The integer values defined in the Orientation enumeration match the stored Exif values.

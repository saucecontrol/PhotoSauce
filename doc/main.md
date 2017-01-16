##MagicImageProcessor

The main MagicScaler image processor.

###ProcessImage(string, Stream, ProcessImageSettings)

Accepts a file path for the input image, a stream for the output image, and a ProcessImageSettings object for settings.  The output stream must allow Seek and Write.

###ProcessImage(byte[], Stream, ProcessImageSettings)

Accepts a byte array for the input image, a stream for the output image, and a ProcessImageSettings object for settings.  The output stream must allow Seek and Write.

###ProcessImage(Stream, Stream, ProcessImageSettings)

Accepts a stream for the input image, a stream for the output image, and a ProcessImageSettings object for settings.  The output stream must allow Seek and Write.  The input stream must allow Seek and Read.

##GdiImageProcessor

This class is included only for testing/benchmarking purposes.  It will be removed in a future version and should not be used for production code.

##WicImageProcessor

This class is included only for testing/benchmarking purposes.  It will be removed in a future version and should not be used for production code.

##ProcessImageSettings

Settings for the ProcessImage operation

###FrameIndex: int

The frame number (starting from 0) to read from a multi-frame file, such as a multi-page TIFF or animated GIF.  For single-frame images, 0 is the only valid value.

Default Value: 0

###Width: int

The output image width in pixels.  If auto-cropping is enabled, a value of 0 will set the width automatically based on the output height.  Width and Height may not both be set to 0.

Default Value: 0

###Height: int

The output image height in pixels.  If auto-cropping is enabled, a value of 0 will set the height automatically based on the output width.  Width and Height may not both be set to 0.

Default Value: 0

###Sharpen: bool

Indicates whether an [unsharp mask](https://en.wikipedia.org/wiki/Unsharp_masking) operation should be performed on the image following the resize.  The sharpening settings are controlled by the [UnsharpMask](#UnsharpMask) property.

Default value: false

###ResizeMode: CropScaleMode

A [CropScaleMode](#CropScaleMode) value indicating whether auto-cropping should be performed or whether the resized image may have a different aspect ratio.  Auto-cropping is performed only if a [Crop](#Crop) value is not explicitly set.

Default value: Crop

###Crop: Rectangle

A System.Drawing.Rectangle that specifies which part of the input image should be included.  If the rectangle is empty and the [ResizeMode](#ResizeMode) is set to `Crop`, the image will be cropped automatically.  Points given for this rectangle must be expressed in terms of the input image.

Default value: Rectangle.Empty

###Anchor: CropAnchor

A [CropAnchor](#CropAnchor) value indicating the position of the auto-crop rectangle.  Values may be combined to specify a vertical and horizontal position.  Ex: `myCrop = CropAnchor.Top | CropAnchor.Left`.

Default value: CropAnchor.Center

###SaveFormat: FileFormat

A [FileFormat](#FileFormat) value indicating the codec used for the output image.  A value of `Auto` will choose the output codec based on the input image type.

Default value: FileFormat.Auto

###MatteColor: Color

A System.Drawing.Color value indicating a background color to be applied when processing an input image with transparency.  When converting to a file format that does not support transparency (e.g. PNG->JPEG), the background color will be Black unless otherwise specified.  When saving as a file format that does support transparency, the transparency will be maintained unless a color is set.

Default value: Color.Empty

###HybridMode: HybridScaleMode

A [HybridScaleMode](#HybridScaleMode) value indicating whether hybrid processing is allowed.  Hybrid processing may use the image decoder or another low-quality scaler to shrink an image to an intermediate size before the selected high-quality algorithm is applied to the final resize.  This can result in dramatic performance improvements but with a reduction in image quality.

Default value: HybridScaleMode.FavorQuality

###BlendingMode: GammaMode

A [GammaMode](#GammaMode) value indicating whether the scaling algorithm is applied in linear or gamma-corrected colorspace.  Linear processing will yield better quality in almost all cases but with a performance cost.

Default value: GammaMode.Linear

###MetadataNames: IEnumerable<string>

A list of metadata policy names or explicit metadata paths to be copied from the input image to the output image.  This can be useful for preserving author or copyright EXIF tags in the output image.  See the [Windows Photo Metadata Policies](https://msdn.microsoft.com/en-us/library/windows/desktop/ee872003(v=vs.85).aspx) for examples of commonly-used values, or the [Metadata Query Language Overview](https://msdn.microsoft.com/en-us/library/windows/desktop/ee872003(v=vs.85).aspx) for explicit path syntax.

Default value: null

###JpegQuality: int

Sets the quality value passed to the JPEG encoder for the output image.  If this value is set to 0, the quality level will be set automatically according to the output image dimensions.  Typically, this value should be 80 or greater if set explicitly.

Default value: 0

###JpegSubsampleMode: ChromaSubsampleMode

A [ChromaSubsampleMode](#ChromaSubsampleMode) value indicating how [chroma subsampling](https://en.wikipedia.org/wiki/Chroma_subsampling) is configured in the JPEG encoder for the output image.  If this value is set to `Default`, the chroma subsampling will be set automatically based on the [JpegQuality](#JpegQuality) setting.

Default value: ChromaSubsampleMode.Default

###Interpolation: InterpolationSettings

An [InterpolationSettings](#InterpolationSettings) object specifying details of the sampling algorithm to use for image scaling.  If this value is unset, the algorithm will be chosen automatically based on the ratio of input image size to output image size.

Default value: unset

###UnsharpMask: UnsharpMaskSettings

An [UnsharpMaskSettings](#UnsharpMaskSettings) object specifying sharpening settings.  If this value is unset, the settings will be chosen automatically based on the ratio of input image size to output image size.

Default value: unset

##CropAnchor 

A flags enumeration for specifying auto-crop anchor.

By default, auto-cropping will maintain the image center by cropping equally from the top, bottom, or sides.  If you wish to direct the auto-cropper to focus on another part of the image, you may specify a vertical and horizontal bias using a combination of values.  Only one horizontal and one vertical value may be combined.

* Center
* Top
* Bottom
* Left
* Right

##CropScaleMode

An enumeration for specifying auto-crop or auto-size behavior.

###Crop

Auto-crop the input image to fit within the given Width and Height while maintaining the aspect ratio of the input image.

###Max

Auto-size the output image to a maximum of the values given for Width and Height while maintaining aspect ratio of the input image.

###Stretch

Allow the output image Width and Height to change the aspect ratio of the input image.  This may result in stretching or distortion of the image.

###Examples

Suppose you have an input image with dimensions of 640x480 and you set the Width and Height of the output image to 100x100.
`Crop` will produce an output image of 100x100, preserving the aspect ratio of the input image by cropping from the sides of the image.  By default, this will crop evenly from the left and right.  You can change that behavior by changing the [Anchor](#CropAnchor) value.
`Max` will produce an output image of 100x75, preserving the aspect ratio of the input image by contstraining the dimensions of the output image.
`Stretch` will produce an output image of 100x100 that is squished horizontally.

When using `Crop` mode, you may also choose to specify only one of the Width or Height.  In this case, and the undefined dimension will be set automatically to preserve the source image's aspect ratio after taking the Crop setting into account.
Again, using a 640x480 input image as an example, you can expect the following:

Width=100 with the default values Height=0/Crop=Rectangle.Empty will result in an output image of 100x75 with no cropping.
Height=100 with the default values Width=0/Crop=Rectangle.Empty will result in an output image of 133x100 with no cropping.
Width=100/Crop=Rectangle.FromLTRB(0,0,480,480) with the default value Height=0 will result in an output image of 100x100 with the right portion of the image cropped.  You can achieve the same result with Width=100/Height=100/Anchor=CropAnchor.Left.

##HybridScaleMode

An enumeration for specifying the amount of low-quality scaling performed in high-ratio resizing operations.

###FavorQuality

Resize the image to an intermediate size at least 3x the output dimensions with the low-quality scaler.  Perform the final resize with the high-quality scaler.

###FavorSpeed

Resize the image to an intermediate size at least 2x the output dimensions with the low-quality scaler.  Perform the final resize with the high-quality scaler.

###Turbo

Resize the image entirely using the low-quality scaler if possible.  If not possible, perform the minimal amount of work possible in the high-quality scaler.

###Off

Perform the entire resize with the high-quality scaler.  This will yield the best quality image but at a performance cost.

##GammaMode

An enumeration for specifying the light blending mode used for high-quality scaling operations. 

###Linear

Perform the high-quality scaling in [linear light](http://web.archive.org/web/20160826144709/http://www.4p8.com/eric.brasseur/gamma.html) colorspace.  This will give better results in most cases but at a performance cost.

###sRGB

Perform the high-quality scaling in gamma-corrected sRGB colorspace.  This will yield output more similar to other scaling software but will be less correct in most cases.

##ChromaSubsampleMode

An enumeration for specifying the [chroma subsampling](https://en.wikipedia.org/wiki/Chroma_subsampling) mode used by the JPEG encoder. 

###Default

Choose the chroma subsampling mode automatically based on the JpegQuality setting

###Subsample420

4:2:0 chroma subsampling

###Subsample422

4:2:2 chroma subsampling

###Subsample444

No chroma subsampling (4:4:4)

##FileFormat

An enumeration for specifying the file format (codec) used for the output image. 

###Auto

Choose the output file format automatically based on the format of the input image

###Jpeg

JPEG.  Use JpegQuality and JpegSubsampleMode settings to control output.

###Png

24-bit or 32-bit PNG, depending on whether or not the input image contains an alpha channel.

###Png8

8-bit indexed PNG

###Gif

8-bit indexed GIF

###Bmp

24-bit or 32-bit BMP, depending on whether or not the input image contains an alpha channel.

###Tiff

Uncompressed 24-bit or 32-bit TIFF, depending on whether or not the input image contains an alpha channel.

##UnsharpMaskSettings

A structure for specifying the settings used for the post-resize [sharpening](https://en.wikipedia.org/wiki/Unsharp_masking) of the output image.  These settings are designed to function similarly to the Unsharp Mask settings in Photoshop.

###Amount: int

The amount of sharpening applied.  Technically, this is a percentage applied to the luma difference between the original and blurred images.  Typical values are between 25 and 200.

###Radius: double

The radius of the gaussian blur used for the unsharp mask.  More blurring in the mask yields more sharpening in the final image.  Typical values are between 0.3 and 3.0.  Larger radius values can have significant performance cost.

###Threshold

The minimum difference between the original and blurred images for a pixel to be sharpened.  When using larger `Radius` or `Amount` values, a larger `Threshold` value can ensure lines are sharpened while textures are not. Typical values are between 0 and 10.

##InterpolationSettings

A structure for specifying the sampling algorithm used by the high-quality scaler.  There are a number of well-known algoritms preconfigured as static fields, or you can define your own.

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

###WeightingFunction: IInterpolator

A reference to an object implementing IInterpolator, which specifies the sampling range for the filter and a function to generate the weighting value for a given distance.

###Blur: double

A value used to stretch (or compress) the sampling range for the filter.  The default and recommnded value is 1.  You may use a value greater than 1 to blur or smooth the sampling function.  Values less than 1 can cause unpleasant artifacts.

##IInterpolator

An interface for defining custom sampling functions.

###Support: double

The support radius of the sampling function.  The sampling window will be twice this value.

###GetValue(double) returns double

The weighting function.  This function accepts a distance from the destination sample's center and returns a weight for the sample at that distance.

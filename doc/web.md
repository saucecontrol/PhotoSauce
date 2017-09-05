# WebRSize Configuration Guide

In order to reduce overhead and improve security, WebRSize requires explicit opt-in for the image folders you want processed.  WebRSize will not perform any processing at all unless at least one image folder is configured.  You must register a WebRSize `ConfigSection` to store this configuration.

```
<configSections>
  <section name="webrsize" type="PhotoSauce.WebRSize.WebRSizeSection" />
</configSections>
```

A minimal configuration must specify the disk cache location and at least one image folder.

```
<webrsize>
  <diskCache path="/webrsizecache" />
  <imageFolders>
    <add name="images" path="/images/" />
  </imageFolders>
</webrsize>
```

The folder specified in the `diskCache` element must exist, and the App Pool identity must have write access to that folder.  Requests matching paths specified in the `imageFolders` element are eligible for processing by WebRSize.  All other requests pass through WebRSize unchanged.

Processing and caching are managed by an `IHttpModule` implementation called `WebRSizeModule`.  This module examines inbound requests matching the configured image folder paths and either serves a cached already-processed image or processes the image on the fly and schedules a copy to be saved to the disk cache.

The `WebRSizeModule` must receive event notifications for static image requests in your configured folders in order to do its job.  There are multiple ways to accomplish this.

1. The module will automatically register itself by using the assembly `PreApplicationStartMethodAttribute`.  For some configurations, this may be adequate.  However, modules registered programmatically are set up with the `ManagedHandler` [PreCondition](https://blogs.msdn.microsoft.com/david.wang/2006/03/19/iis7-preconditions-and-the-integrated-pipeline/), which means the module will only receive even notifications if a managed `IHttpHandler` is associated with the file extension being requested.  For most IIS configurations, image file extensions will be mapped to the unmanaged (and very efficient) IIS static file handler.  In these cases, the self-registered module will not receive event notifications and won't work.

2. You can register the module explicitly in the `system.webServer` section.

    ```
    <system.webServer>
      <modules>
        <add name="WebRSize" type="PhotoSauce.WebRSize.WebRSizeModule" />
      </modules>
    </system.webServer>
    ```
Omit the `preCondition` attribute on the module entry to ensure it will see all requests, managed or not.  The `WebRSizeModule` detects when it has been manually registered and will skip self-registration.  This option will always work and is the safe way to go if you are in doubt.

3. You can set up an explicit handler mapping for image file extension you want to process that maps to a managed `IHttpHandler`.

    ```
    <system.webServer>
      <handlers>
        <add name="ManagedJpeg" path="*.jpg" verb="GET" type="System.Web.StaticFileHandler" />
      </handlers>
    </system.webServer>
    ```
This is common in scenarios where images will be served from a `VirtualPathProvider` that uses a database or cloud storage.  The built-in `StaticFileHandler` will request files from the registered `VirtualPathProvider`, and because the handler is managed, the self-registered `WebRSizeModule` instance will be able to intercept image processing requests.

## Configuration Reference

### Disk Cache

The `diskCache` element configures local disk caching for WebRSize. Disk caching is mandatory, so this configuration element is required.

#### Cache Root

The `path` attribute specifies the site-relative path to the local root cache folder.  The folder must exist, and the App Pool Identity must have write access to the folder.

If you perform regular maintenance on the disk cache folder, such as deleting subfolders, it is recommended that you create the cache folder outside your web application root and create an [NTFS junction](https://en.wikipedia.org/wiki/NTFS_junction_point) within the app that points to the external cache folder.  This prevents unwanted App Pool recycling when subfolders are deleted.

#### Cache File Naming

The `namingStrategy` attribute specifies a `Type` name of a class that inherits from `CacheFileNamingStrategyBase`.  There are three preconfigured naming strategies included with WebRSize:

* `PhotoSauce.WebRSize.CacheFileNamingStrategyMirror` mirrors the subfolder and file structure from the source `imageFolder`.  If your master images are well-organized, with only a few images per folder, this makes it easy to see what's in the cache and to clean up cache files that belong to master images when they are deleted.
* `PhotoSauce.WebRSize.CacheFileNamingStrategyDistribute1K` distributes cache files across up to 1024 folders within the cache root based on a hash of the input path.  All cache files belonging to a single master image will be placed in the same cache folder, but the folders are not easy to guess.  Use this strategy if the folder structure for your master images is unwieldy or if you have so many files per folder that storing all their cached variants in one folder would result in too many files.
* `PhotoSauce.WebRSize.CacheFileNamingStrategyDistribute1M` distributes cache files across up to 1024 folders within the cache root and then up to another 1024 files within each of those folders, giving you up to 1 MegaFolders (I just made that up).  Choose this one for the same reasons as above but if your image library is so large that 1024 folders would still have too many files in each.

The file names generated by each of the included naming strategies are the same and look something like this:
`H3BD4R4N.test1.121x150.jpg`

The pattern used is as follows:
`[40-Bit Settings Hash Base32 Encoded].[Original File Name].[Width]x[Height].[Extension]`

The hash is used as the first part of the file name to prevent performance issues associated with 8.3-compatible name collisions.  If you have different preferences, you may create your own naming strategy by inheriting the `CacheFileNamingStrategyBase` class and implementing the abstract `GetCacheFilePath` method.

Note that WebRSize uses a normalized version of the settings when creating the Settings Hash so that parameter combinations that yield the same file net results all share the same cache file.  For example, imagine you have a master JPEG image that is 3000x2000 pixels.  The following query string parameter sets would all generate the same image, so they would all serve the same file from the cache.
```
w=300
h=200
w=300&h=200
w=300&h=300&mode=max
w=300&crop=0,0,3000,2000
w=300&format=jpg
w=300&format=jpeg
w=300&sharpen=true
w=300&gamma=linear
... and many more
```

### Image Folders

The `imageFolders` element defines the collection of source image folders WebRSize can process.  Use the `add` element to define an `ImageFolder` and configure its settings.

#### Path

The `path` attribute defines a base folder that contains source images to be processed.  The path is specified as a site-relative folder, like `/images/`.  Any requests matching this path are examined for processing by WebRSize.  It is best if this path contains only images, as WebRSize does not examine the file extension of the request to before trying to decode the requested file as an image.  Due to the fact that WIC allows codec plugins, there is no good way of knowing in advance whether or not a file is an image.

This is the only attribute that is required for an `ImageFolder` definition.

#### Force Processing

The `forceProcessing` attribute accepts a boolean value to enable or disable mandatory processing of images in the `ImageFolder`.  If, for example, a source image is requested with no query string parameters, you can force WebRSize to process and re-encode the image using its default settings or your own configured defaults.  This allows you to store high-quality master images but makes sure a more web-friendly copy is served, even at full resolution.

Default Value: false

#### Allow Enlarge

The `allowEnlarge` attribute accepts a boolean value indicating whether or not WebRSize is allowed to serve a processed image that is larger than the source.  Most web browsers do a fine job of enlarging images, so enlarging them on the server is generally a waste of server resources and bandwidth.  When `allowEnlarge` is false, if a request specifies dimensions greater than those of the source image, WebRSize will change the settings to return the image in its original size or a cropped version thereof.

Default Value: false

#### Max Pixels

The `maxPixels` attribute allows you to limit the size of images served by WebRSize.  The limit is defined as total pixels rather than a hard limit on width or height.  A request whose width*height exceeds the `maxPixels` value will generate a 400 (Bad Request) response.

Default Value 10,000,000

#### Default Settings

The `defaultSettings` element allows you to define a collection of key/value pairs that specify settings you would like applied to all WebRSize requests in the absence of an override on the query string.  For example, if you wish to disable sharpening on an entire image folder, you need not append the `sharpen=false` value to all request query strings.  You can configure it as a folder default.

```
<webrsize>
  <diskCache path="/webrsizecache" />
  <imageFolders>
    <add name="images" path="/images/" forceProcessing="true">
      <defaultSettings>
        <add key="width" value="300"/>
        <add key="sharpen" value="false"/>
      </defaultSettings>
    </add>
  </imageFolders>
</webrsize>
```

Any parameter value that would be valid on the query string of a request can be specified as a default for an `ImageFolder`.  If a value has a default set but is also specified on the query string of a request, the default is overridden.

## Query String Parameters

The following MagicScaler `ProcessImageSettings` values can be set from the query string when processing an image through a configured `ImageFolder`.  See the [MagicScaler documentation](main.md) for more details.

| ProcessImageSettings<br />Property| QueryString<br />Name | QueryString<br />Values/Type |
|---|---|---|
| FrameIndex | frame<br />page | int |
| Width | width<br />w | int |
| Height | height<br />h | int |
| Crop | crop | [left(int)],[top(int)],[width(int)],[height(int)] |
| CropAnchor | anchor | [vertical(center, top, bottom)]\|[horizontal(center, left, right)] |
| ResizeMode | mode | crop, max, stretch |
| Sharpen | sharpen | bool |
| HybridMode | hybrid | favorquality, favorspeed, turbo, off |
| SaveFormat | format | jpg, jpeg, png, png8, gif, bmp, tiff |
| JpegQuality | quality | int |
| JpegSubsampleMode | subsample | 420, 422, 444 |
| MatteColor | bgcolor<br />bg | [CSS3 named color], [rgba hex values] |
| Interpolation | filter | nearestneighbor, average, linear, quadratic, catrom, cubic, lanczos, spline36 |

## Advanced Topics

### Output Caching

When WebRSize determines that an image processing request can be served from the disk cache, it rewrites the request to the cache location so that IIS can map its unmanaged static file handler to serve the image.  This is done as quickly and efficiently as possible, but it misses out on one performance advantage static images normally have.

Normally static images can be cached and served by the `http.sys` kernel cache.  Unfortunately, a couple of properties of processed image requests make `http.sys` consider those requests [ineligible for output caching](https://support.microsoft.com/en-us/help/817445/instances-in-which-http.sys-does-not-cache-content).  In our case, it is both the presence of a query string and the fact that the request URL is rewritten that cause it to be ineligible.  In order to force those requests to be eligible for kernel caching, you must explicitly configure a `kernelCachePolicy`.

This sample policy ensures that any requests for .jpg files can be kernel-cached as long as the URL (including query string) matches.  This will allow your processed images to be served with performance equal to that of static image files once they've been cached to disk.

```
<system.webServer>
  <caching>
    <profiles>
      <add extension=".jpg" kernelCachePolicy="CacheUntilChange" location="Any" />
    </profiles>
  </caching>
</system.webServer>
```

You can verify kernel caching is working for processed image requests with the `netsh http show cachestate` command.  The output looks something like this when items are cached:

```
PS C:\> netsh http show cachestate

Snapshot of HTTP response cache:
--------------------------------

URL: http://localhost/images/test1.jpg?w=150&h=150&mode=max
    Status code: 200
    HTTP verb: GET
    Cache policy type: User invalidates
    Creation time: 2015.5.4:23.9.11:0
    Request queue name: webrsize
    Content type: image/jpeg
    Content encoding: (null)
    Headers length: 212
    Content length: 21335
    Hit count: 3
    Force disconnect after serving: FALSE

...
```

There are several other restrictions that affect caching, so you may not see many (or any) responses cached, even if you have a policy configured.  By default, a single URL has to be requested more than twice within a 10-second window to be considered cache-worthy, and the response must be smaller than 256KB.

The kernel cache is configurable in the [`caching` element](https://blogs.iis.net/ksingla/caching-in-iis7), the [`serverRuntime` element](https://www.iis.net/configreference/system.webserver/serverruntime) and the [registry](https://support.microsoft.com/en-us/help/820129/http.sys-registry-settings-for-windows) if you wish to tweak its default settings.

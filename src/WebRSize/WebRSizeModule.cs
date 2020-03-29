using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Hosting;
using System.Threading.Tasks;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.WebRSize
{
	/// <summary>An <see cref="IHttpModule" /> implementation that intercepts image processing requests and either rewrites them to the cached location or dispatches them to the dynamic processing handler.</summary>
	public class WebRSizeModule : IHttpModule
	{
		internal const string ProcessImageSettingsKey = "wsmssettings";
		internal const string CachePathKey            = "wscachepath";
		internal const string NotFoundPath            = "/404/notfound.png";

		private static readonly bool diskCacheEnabled = WebRSizeConfig.Current.DiskCache.Enabled;
		private static readonly ImageFolder[] imageFolders = WebRSizeConfig.Current.ImageFolders.OfType<ImageFolder>().ToArray();
		private static readonly Lazy<ICacheFileNamingStrategy> namingStrategy = new Lazy<ICacheFileNamingStrategy>(() => (ICacheFileNamingStrategy)Activator.CreateInstance(WebRSizeConfig.Current.DiskCache.NamingStrategy));

		private async Task mapRequest(object sender, EventArgs e)
		{
			var ctx = ((HttpApplication)sender).Context;
			var vpp = HostingEnvironment.VirtualPathProvider;

			string path = ctx.Request.Path;
			bool exists = vpp is CachingAsyncVirtualPathProvider vppAE ? await vppAE.FileExistsAsync(path).ConfigureAwait(false) : vpp.FileExists(path);

			var folderConfig = imageFolders.FirstOrDefault(f => ctx.Request.Path.StartsWith(f.Path, StringComparison.OrdinalIgnoreCase));
			if (folderConfig is null)
				return;

			var dic = ctx.Request.QueryString.ToDictionary();
			if (folderConfig.DefaultSettings.Count > 0)
				dic = folderConfig.DefaultSettings.ToDictionary().Coalesce(dic);

			var s = ProcessImageSettings.FromDictionary(dic);

			int rw = s.Width, rh = s.Height;
			if (double.TryParse(dic.GetValueOrDefault("devicepixelratio") ?? dic.GetValueOrDefault("dpr"), out double dpr))
			{
				dpr = dpr.Clamp(1d, 5d);
				s.Width = (int)Math.Floor(s.Width * dpr);
				s.Height = (int)Math.Floor(s.Height * dpr);
			}

			if (exists && s.IsEmpty && !folderConfig.ForceProcessing)
				return;

			var ifi = default(ImageFileInfo);
			if (!exists)
			{
				ctx.Response.TrySkipIisCustomErrors = true;
				ctx.Response.StatusCode = 404;
				path = NotFoundPath;

				if (s.Width <= 0 && s.Height <= 0)
					s.Width = s.Height = 100;

				ifi = new ImageFileInfo(s.Width > 0 ? s.Width : s.Height, s.Height > 0 ? s.Height : s.Width);
				s.SaveFormat = FileFormat.Png;
			}

			ifi ??= vpp is CachingAsyncVirtualPathProvider vppAF ? await vppAF.GetImageInfoAsync(path).ConfigureAwait(false) : await CacheHelper.GetImageInfoAsync(vpp, path).ConfigureAwait(false);
			s.NormalizeFrom(ifi);

			if (!folderConfig.AllowEnlarge && s.ResizeMode != CropScaleMode.Max)
			{
				var frame = ifi.Frames[s.FrameIndex];
				if (s.Width > frame.Width)
				{
					s.Width = frame.Width;
					s.Height = 0;
					s.Fixup(frame.Width, frame.Height);
				}
				if (s.Height > frame.Height)
				{
					s.Width = 0;
					s.Height = frame.Height;
					s.Fixup(frame.Width, frame.Height);
				}
			}

			if (s.Width * s.Height > folderConfig.MaxPixels)
			{
				ctx.Response.StatusCode = 400;
				ctx.ApplicationInstance.CompleteRequest();
				return;
			}

			if (dpr > 0d && !dic.ContainsKey("quality") && !dic.ContainsKey("q"))
			{
				dpr = Math.Max(Math.Max((double)s.Width / (rw > 0 ? rw : s.Width), (double)s.Height / (rh > 0 ? rh : s.Height)), 1d);
				s.JpegQuality -= (int)Math.Round((dpr - 1d) * 10);
			}

			string cacheVPath = namingStrategy.Value.GetCacheFilePath(path, s);
			string cachePath = HostingEnvironment.MapPath(cacheVPath);

			if (diskCacheEnabled && File.Exists(cachePath))
			{
				ctx.RewritePath(cacheVPath, null, string.Empty, false);
				return;
			}

			if (path == NotFoundPath)
				ctx.RewritePath(NotFoundPath);

			ctx.Items[ProcessImageSettingsKey] = s;
			ctx.Items[CachePathKey] = cachePath;
			ctx.RemapHandler(WebRSizeHandler.Instance);
		}

		/// <inheritdoc />
		public void Init(HttpApplication app)
		{
			// Init() may be called multiple times by IIS and needs to bind the same handlers on each call.
			// However, we may also load the module multiple times and need to make sure only one loaded
			// instance is processing requests. This check catches the second case while allowing the first.
			if (app.Context.Items[nameof(WebRSizeModule)] != null)
				return;

			app.Context.Items[nameof(WebRSizeModule)] = this;

			if (!imageFolders.Any())
				return;

			var mapHelper = new EventHandlerTaskAsyncHelper(mapRequest);
			app.AddOnPostAuthorizeRequestAsync(mapHelper.BeginEventHandler, mapHelper.EndEventHandler);
		}

		/// <inheritdoc />
		public void Dispose() { }
	}
}

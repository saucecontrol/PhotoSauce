using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Hosting;
using System.Threading.Tasks;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.WebRSize
{
	public class WebRSizeModule : IHttpModule
	{
		public const string ProcessImageSettingsKey = "wsmssettings";
		public const string CachePathKey            = "wscachepath";

		private static readonly WebRSizeHandler handler = new WebRSizeHandler();
		private static readonly ImageFolder[] imageFolders = WebRSizeConfig.Current.ImageFolders.OfType<ImageFolder>().ToArray();
		private static readonly Lazy<ICacheFileNamingStrategy> namingStrategy = new Lazy<ICacheFileNamingStrategy>(() => (ICacheFileNamingStrategy)Activator.CreateInstance(WebRSizeConfig.Current.DiskCache.NamingStrategy));

		private async Task mapRequest(object sender, EventArgs e)
		{
			var ctx = ((HttpApplication)sender).Context;

			var folderConfig = imageFolders.FirstOrDefault(f => ctx.Request.Path.StartsWith(f.Path, StringComparison.OrdinalIgnoreCase));
			if (folderConfig == null)
				return;

			var dic = folderConfig.DefaultSettings.ToDictionary().Coalesce(ctx.Request.QueryString.ToDictionary());
			var s = ProcessImageSettings.FromDictionary(dic);

			string path = ctx.Request.Path;
			var vpp = HostingEnvironment.VirtualPathProvider;
			var vppAsync = vpp as CachingAsyncVirtualPathProvider;
			bool exists = vppAsync != null ? await vppAsync.FileExistsAsync(path) : vpp.FileExists(path);
			if (!exists)
			{
				ctx.Response.TrySkipIisCustomErrors = true;
				ctx.Response.StatusCode = 404;
				path = "/404/notfound.png";

				if (s.Width == 0 && s.Height == 0)
					s.Width = s.Height = 100;
				else if (s.Width <= 0 || s.Height <= 0)
					s.Fixup(s.Width > 0 ? s.Width : s.Height, s.Height > 0 ? s.Height : s.Width);
			}
			else if (s.Width == 0 && s.Height == 0 && s.Crop.IsEmpty && s.SaveFormat == FileFormat.Auto && !folderConfig.ForceProcessing)
			{
				return;
			}
			else
			{
				var ifi = await CacheHelper.GetImageInfoAsync(path);
				s.NormalizeFrom(ifi);

				if (!folderConfig.AllowEnlarge)
				{
					var frame = ifi.Frames[s.FrameIndex];
					int iw = frame.Rotated90 ? frame.Height : frame.Width, ih = frame.Rotated90 ? frame.Width : frame.Height;
					if (s.Width > iw)
					{
						s.Width = Math.Min(s.Width, iw);
						s.Height = 0;
						s.Fixup(iw, ih);
					}
					if (s.Height > ih)
					{
						s.Height = Math.Min(s.Height, ih);
						s.Width = 0;
						s.Fixup(iw, ih);
					}
				}
			}

			var cacheVPath = namingStrategy.Value.GetCacheFilePath(path, s);
			var cachePath = HostingEnvironment.MapPath(cacheVPath);

			if (File.Exists(cachePath))
			{
				ctx.RewritePath(cacheVPath, null, string.Empty, false);
				return;
			}

			if (s.Width * s.Height > folderConfig.MaxPixels)
			{
				ctx.Response.StatusCode = 400;
				ctx.ApplicationInstance.CompleteRequest();
				return;
			}

			ctx.Items[ProcessImageSettingsKey] = s;
			ctx.Items[CachePathKey] = cachePath;
			ctx.RemapHandler(handler);
		}

		public void Init(HttpApplication app)
		{
			// Init() may be called multiple times by IIS and needs to bind the same handlers on each call.
			// However, we may also load the module multiple times and need to make sure only one loaded
			// instance is processing requests. This check catches the second case while allowing the first.
			if (app.Context.Items[nameof(WebRSizeModule)] != null)
				return;

			app.Context.Items[nameof(WebRSizeModule)] = true;

			if (!imageFolders.Any())
				return;

			var mapHelper = new EventHandlerTaskAsyncHelper(mapRequest);
			app.AddOnPostAuthorizeRequestAsync(mapHelper.BeginEventHandler, mapHelper.EndEventHandler);
		}

		public void Dispose() { }
	}
}

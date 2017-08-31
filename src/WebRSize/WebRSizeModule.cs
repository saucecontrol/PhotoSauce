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
		internal const string ProcessImageSettingsKey = "wsmssettings";
		internal const string CachePathKey            = "wscachepath";
		internal const string NotFoundPath            = "/404/notfound.png";

		private static readonly WebRSizeHandler handler = new WebRSizeHandler();
		private static readonly ImageFolder[] imageFolders = WebRSizeConfig.Current.ImageFolders.OfType<ImageFolder>().ToArray();
		private static readonly Lazy<ICacheFileNamingStrategy> namingStrategy = new Lazy<ICacheFileNamingStrategy>(() => (ICacheFileNamingStrategy)Activator.CreateInstance(WebRSizeConfig.Current.DiskCache.NamingStrategy));

		private async Task mapRequest(object sender, EventArgs e)
		{
			var ctx = ((HttpApplication)sender).Context;
			var vpp = HostingEnvironment.VirtualPathProvider;
			var vppAsync = vpp as CachingAsyncVirtualPathProvider;

			string path = ctx.Request.Path;
			bool exists = vppAsync != null ? await vppAsync.FileExistsAsync(path) : vpp.FileExists(path);

			var folderConfig = imageFolders.FirstOrDefault(f => ctx.Request.Path.StartsWith(f.Path, StringComparison.OrdinalIgnoreCase));
			if (folderConfig == null)
				return;

			var dic = ctx.Request.QueryString.ToDictionary();
			if (folderConfig.DefaultSettingsDictionary.Count > 0)
				dic = folderConfig.DefaultSettingsDictionary.Coalesce(dic);

			var s = ProcessImageSettings.FromDictionary(dic);
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
			}

			ifi = ifi ?? await CacheHelper.GetImageInfoAsync(path);
			s.NormalizeFrom(ifi);

			if (!folderConfig.AllowEnlarge)
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

			var cacheVPath = namingStrategy.Value.GetCacheFilePath(path, s);
			var cachePath = HostingEnvironment.MapPath(cacheVPath);

			if (File.Exists(cachePath))
			{
				ctx.RewritePath(cacheVPath, null, string.Empty, false);
				return;
			}

			if (path == NotFoundPath)
				ctx.RewritePath(NotFoundPath);

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

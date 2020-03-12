using System;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.WebRSize
{
	internal static class CacheHelper
	{
		public static class CompletedTask<T> { public static readonly Task<T> Default = Task.FromResult(default(T)); }

		private static readonly ConcurrentDictionary<string, object> cacheItemLocks = new ConcurrentDictionary<string, object>();

		public static CacheDependency MakeVirtualPathDependency(params string[] paths) => HostingEnvironment.VirtualPathProvider.GetCacheDependency(string.Empty, paths, DateTime.UtcNow);

		public static Task<T> GetOrAddAsync<T>(string cacheKey, Func<Task<T>> getValue = null, Func<CacheDependency> getDepedency = null)
		{
			if (HttpRuntime.Cache[cacheKey] is Task<T> val)
				return val;

			if (getValue is null)
				return CompletedTask<T>.Default;

			lock (cacheItemLocks.GetOrAdd(cacheKey, __ => new object()))
			{
				val = HttpRuntime.Cache[cacheKey] as Task<T>;
				if (val is null)
				{
					val = getValue();
					var dependency = getDepedency is null ? null : getDepedency();

					HttpRuntime.Cache.Insert(cacheKey, val, dependency, DateTime.UtcNow.AddSeconds(60), Cache.NoSlidingExpiration);
					val.ContinueWith(_ => HttpRuntime.Cache.Remove(cacheKey), TaskContinuationOptions.NotOnRanToCompletion);
				}
			}
			cacheItemLocks.TryRemove(cacheKey, out _);

			return val;
		}

		public static Task<ImageFileInfo> GetImageInfoAsync(VirtualPathProvider vpp, string path)
		{
			return GetOrAddAsync(string.Concat("wsvppfi_", path), async () => {
				var file = vpp is CachingAsyncVirtualPathProvider vppAsync ? await vppAsync.GetFileAsync(path).ConfigureAwait(false) : vpp.GetFile(path);
				var afile = file as AsyncVirtualFile;

				using var stream = afile != null ? await afile.OpenAsync().ConfigureAwait(false) : file.Open();
				return ImageFileInfo.Load(stream, afile != null ? afile.LastModified : DateTime.MinValue);
			}, () => MakeVirtualPathDependency(path));
		}
	}
}
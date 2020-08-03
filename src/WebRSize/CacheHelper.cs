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

		private static readonly ConcurrentDictionary<string, object> cacheItemLocks = new ();

		public static CacheDependency MakeVirtualPathDependency(params string[] paths) => HostingEnvironment.VirtualPathProvider.GetCacheDependency(string.Empty, paths, DateTime.UtcNow);

		public static Task<T> GetOrAddAsync<T>(string cacheKey, Func<Task<T>> getValue = null, Func<CacheDependency> getDepedency = null)
		{
			if (HttpRuntime.Cache[cacheKey] is Task<T> task)
				return task;

			if (getValue is null)
				return CompletedTask<T>.Default;

			lock (cacheItemLocks.GetOrAdd(cacheKey, _ => new object()))
			{
				task = HttpRuntime.Cache[cacheKey] as Task<T>;
				if (task is null)
				{
					task = getValue();
					var dependency = getDepedency is null ? null : getDepedency();

					HttpRuntime.Cache.Insert(cacheKey, task, dependency, DateTime.UtcNow.AddSeconds(60), Cache.NoSlidingExpiration);
					task.ContinueWith((_, k) => HttpRuntime.Cache.Remove((string)k), cacheKey, TaskContinuationOptions.NotOnRanToCompletion);
				}
			}
			cacheItemLocks.TryRemove(cacheKey, out _);

			return task;
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
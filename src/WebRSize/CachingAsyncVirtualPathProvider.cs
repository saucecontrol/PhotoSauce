using System;
using System.IO;
using System.Linq;
using System.Web.Caching;
using System.Web.Hosting;
using System.Collections;
using System.Threading.Tasks;

namespace PhotoSauce.WebRSize
{
	public class CachingAsyncVirtualPathProvider : VirtualPathProvider
	{
		protected static readonly Task<bool> True = Task.FromResult(true);
		protected static readonly Task<bool> False = Task.FromResult(false);

		public CachingAsyncVirtualPathProvider(): base() { }

		protected CachingAsyncVirtualPathProvider AsyncPrevious { get; private set; }

		protected override void Initialize() => AsyncPrevious = Previous as CachingAsyncVirtualPathProvider;

		protected virtual bool IsPathCaptured(string virtualPath) => false;

		protected virtual bool FileExistsInternal(string virtualPath) => FileExistsAsyncInternal(virtualPath).Result;

		protected virtual bool DirectoryExistsInternal(string virtualDir) => DirectoryExistsAsyncInternal(virtualDir).Result;

		protected virtual VirtualFile GetFileInternal(string virtualPath) => GetFileAsyncInternal(virtualPath).Result;

		protected virtual VirtualDirectory GetDirectoryInternal(string virtualDir) => GetDirectoryAsyncInternal(virtualDir).Result;

		protected virtual CacheDependency GetCacheDependencyInternal(string virtualPath, IEnumerable virtualPathDependencies, DateTime utcStart)
		{
			return new CacheDependency(new[] { virtualPath }, virtualPathDependencies.Cast<string>().ToArray(), utcStart);
		}

		protected virtual Task<bool> FileExistsAsyncInternal(string virtualPath) => False;

		protected virtual Task<bool> DirectoryExistsAsyncInternal(string virtualDir) => False;

		protected virtual Task<VirtualFile> GetFileAsyncInternal(string virtualPath) => CacheHelper.CompletedTask<VirtualFile>.Default;

		protected virtual Task<VirtualDirectory> GetDirectoryAsyncInternal(string virtualDir) => CacheHelper.CompletedTask<VirtualDirectory>.Default;

		public override bool FileExists(string virtualPath) => IsPathCaptured(virtualPath) ? FileExistsInternal(virtualPath) : Previous.FileExists(virtualPath);

		public override bool DirectoryExists(string virtualDir) => IsPathCaptured(virtualDir) ? DirectoryExistsInternal(virtualDir) : Previous.DirectoryExists(virtualDir);

		public override VirtualFile GetFile(string virtualPath) => IsPathCaptured(virtualPath) ? GetFileInternal(virtualPath) : Previous.GetFile(virtualPath);

		public override VirtualDirectory GetDirectory(string virtualDir) => IsPathCaptured(virtualDir) ? GetDirectoryInternal(virtualDir) : Previous.GetDirectory(virtualDir);

		public override CacheDependency GetCacheDependency(string virtualPath, IEnumerable virtualPathDependencies, DateTime utcStart)
		{
			if (IsPathCaptured(virtualPath) || virtualPathDependencies.Cast<string>().Any(p => IsPathCaptured(p)))
				return GetCacheDependencyInternal(virtualPath, virtualPathDependencies, utcStart);

			return Previous.GetCacheDependency(virtualPath, virtualPathDependencies, utcStart);
		}

		public virtual Task<bool> FileExistsAsync(string virtualPath)
		{
			if (IsPathCaptured(virtualPath))
				return CacheHelper.GetOrAddAsync(string.Concat("wsvppfe_", virtualPath), () => FileExistsAsyncInternal(virtualPath));

			return AsyncPrevious?.FileExistsAsync(virtualPath) ?? (Previous.FileExists(virtualPath) ? True : False);
		}

		public virtual Task<bool> DirectoryExistsAsync(string virtualDir)
		{
			if (IsPathCaptured(virtualDir))
				return CacheHelper.GetOrAddAsync(string.Concat("wsvppde_", virtualDir), () => DirectoryExistsAsyncInternal(virtualDir));

			return AsyncPrevious?.DirectoryExistsAsync(virtualDir) ?? (Previous.DirectoryExists(virtualDir) ? True : False);
		}

		public virtual Task<VirtualFile> GetFileAsync(string virtualPath)
		{
			if (IsPathCaptured(virtualPath))
				return CacheHelper.GetOrAddAsync(string.Concat("wsvppf_", virtualPath), () => GetFileAsyncInternal(virtualPath));

			return AsyncPrevious?.GetFileAsync(virtualPath) ?? Task.FromResult(Previous.GetFile(virtualPath));
		}

		public virtual Task<VirtualDirectory> GetDirectoryAsync(string virtualDir)
		{
			if (IsPathCaptured(virtualDir))
				return CacheHelper.GetOrAddAsync(string.Concat("wsvppd_", virtualDir), () => GetDirectoryAsyncInternal(virtualDir));

			return AsyncPrevious?.GetDirectoryAsync(virtualDir) ?? Task.FromResult(Previous.GetDirectory(virtualDir));
		}
	}

	public class DiskCachedVirtualFile : VirtualFile
	{
		private string cachePath;

		public DiskCachedVirtualFile(string virtualPath, string cachePath) : base(virtualPath) => this.cachePath = cachePath;

		public override Stream Open() => new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
	}

	public class MemoryCachedVirtualFile : VirtualFile
	{
		private byte[] data;

		public MemoryCachedVirtualFile(string virtualPath, byte[] data) : base(virtualPath) => this.data = data;

		public override Stream Open() => new MemoryStream(data);
	}

	public abstract class AsyncVirtualFile : VirtualFile
	{
		public AsyncVirtualFile(string virtualPath) : base(virtualPath) { }

		public DateTime LastModified { get; protected set; }

		public abstract Task<Stream> OpenAsync();

		public override Stream Open() => OpenAsync().Result;
	}
}
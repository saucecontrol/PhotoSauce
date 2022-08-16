// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Web.Caching;
using System.Web.Hosting;
using System.Collections;
using System.Threading.Tasks;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.WebRSize;

/// <summary>A base <see cref="VirtualPathProvider"/> that adds memory caching and async operation.</summary>
public class CachingAsyncVirtualPathProvider : VirtualPathProvider
{
	/// <summary>An already-completed <see cref="Task"/> with the value: true.</summary>
	protected static readonly Task<bool> True = Task.FromResult(true);
	/// <summary>An already-completed <see cref="Task"/> with the value: false.</summary>
	protected static readonly Task<bool> False = Task.FromResult(false);

	/// <inheritdoc />
	public CachingAsyncVirtualPathProvider(): base() { }

	/// <inheritdoc cref="VirtualPathProvider.Previous" />
	protected CachingAsyncVirtualPathProvider AsyncPrevious { get; private set; }

	/// <inheritdoc />
	protected override void Initialize() => AsyncPrevious = Previous as CachingAsyncVirtualPathProvider;

	/// <summary>Determines whether the given <paramref name="virtualPath"/> is handled by this <see cref="VirtualPathProvider"/> or another.</summary>
	/// <param name="virtualPath">The Virtual Path to test.</param>
	/// <returns>True if the path is handled by this <see cref="VirtualPathProvider"/>, otherwise false.</returns>
	protected virtual bool IsPathCaptured(string virtualPath) => false;

	/// <inheritdoc cref="FileExists(string)" />
	protected virtual bool FileExistsInternal(string virtualPath) => FileExistsAsyncInternal(virtualPath).GetAwaiter().GetResult();

	/// <inheritdoc cref="DirectoryExists(string)" />
	protected virtual bool DirectoryExistsInternal(string virtualDir) => DirectoryExistsAsyncInternal(virtualDir).GetAwaiter().GetResult();

	/// <inheritdoc cref="GetFile(string)" />
	protected virtual VirtualFile GetFileInternal(string virtualPath) => GetFileAsyncInternal(virtualPath).GetAwaiter().GetResult();

	/// <inheritdoc cref="GetDirectory(string)" />
	protected virtual VirtualDirectory GetDirectoryInternal(string virtualDir) => GetDirectoryAsyncInternal(virtualDir).GetAwaiter().GetResult();

	/// <inheritdoc cref="GetCacheDependency(string, IEnumerable, DateTime)" />
	protected virtual CacheDependency GetCacheDependencyInternal(string virtualPath, IEnumerable virtualPathDependencies, DateTime utcStart) =>
		new CacheDependency(new[] { virtualPath }, virtualPathDependencies.Cast<string>().ToArray(), utcStart);

	/// <inheritdoc cref="FileExists(string)" />
	protected virtual Task<bool> FileExistsAsyncInternal(string virtualPath) => False;

	/// <inheritdoc cref="DirectoryExists(string)" />
	protected virtual Task<bool> DirectoryExistsAsyncInternal(string virtualDir) => False;

	/// <inheritdoc cref="GetFile(string)" />
	protected virtual Task<VirtualFile> GetFileAsyncInternal(string virtualPath) => CacheHelper.CompletedTask<VirtualFile>.Default;

	/// <inheritdoc cref="GetDirectory(string)" />
	protected virtual Task<VirtualDirectory> GetDirectoryAsyncInternal(string virtualDir) => CacheHelper.CompletedTask<VirtualDirectory>.Default;

	/// <inheritdoc cref="GetImageInfoAsync(string)" />
	protected virtual Task<ImageFileInfo> GetImageInfoAsyncInternal(string virtualPath) => CacheHelper.GetImageInfoAsync(this, virtualPath);

	/// <inheritdoc />
	public override bool FileExists(string virtualPath) => IsPathCaptured(virtualPath) ? FileExistsInternal(virtualPath) : Previous.FileExists(virtualPath);

	/// <inheritdoc />
	public override bool DirectoryExists(string virtualDir) => IsPathCaptured(virtualDir) ? DirectoryExistsInternal(virtualDir) : Previous.DirectoryExists(virtualDir);

	/// <inheritdoc />
	public override VirtualFile GetFile(string virtualPath) => IsPathCaptured(virtualPath) ? GetFileInternal(virtualPath) : Previous.GetFile(virtualPath);

	/// <inheritdoc />
	public override VirtualDirectory GetDirectory(string virtualDir) => IsPathCaptured(virtualDir) ? GetDirectoryInternal(virtualDir) : Previous.GetDirectory(virtualDir);

	/// <inheritdoc />
	public override CacheDependency GetCacheDependency(string virtualPath, IEnumerable virtualPathDependencies, DateTime utcStart)
	{
		if (IsPathCaptured(virtualPath) || virtualPathDependencies.Cast<string>().Any(p => IsPathCaptured(p)))
			return GetCacheDependencyInternal(virtualPath, virtualPathDependencies, utcStart);

		return Previous.GetCacheDependency(virtualPath, virtualPathDependencies, utcStart);
	}

	/// <inheritdoc cref="FileExists(string)" />
	public virtual Task<bool> FileExistsAsync(string virtualPath)
	{
		if (IsPathCaptured(virtualPath))
			return CacheHelper.GetOrAddAsync(string.Concat("wsvppfe_", virtualPath), () => FileExistsAsyncInternal(virtualPath));

		return AsyncPrevious?.FileExistsAsync(virtualPath) ?? (Previous.FileExists(virtualPath) ? True : False);
	}

	/// <inheritdoc cref="DirectoryExists(string)" />
	public virtual Task<bool> DirectoryExistsAsync(string virtualDir)
	{
		if (IsPathCaptured(virtualDir))
			return CacheHelper.GetOrAddAsync(string.Concat("wsvppde_", virtualDir), () => DirectoryExistsAsyncInternal(virtualDir));

		return AsyncPrevious?.DirectoryExistsAsync(virtualDir) ?? (Previous.DirectoryExists(virtualDir) ? True : False);
	}

	/// <inheritdoc cref="GetFile(string)" />
	public virtual Task<VirtualFile> GetFileAsync(string virtualPath)
	{
		if (IsPathCaptured(virtualPath))
			return CacheHelper.GetOrAddAsync(string.Concat("wsvppf_", virtualPath), () => GetFileAsyncInternal(virtualPath));

		return AsyncPrevious?.GetFileAsync(virtualPath) ?? Task.FromResult(Previous.GetFile(virtualPath));
	}

	/// <inheritdoc cref="GetDirectory(string)" />
	public virtual Task<VirtualDirectory> GetDirectoryAsync(string virtualDir)
	{
		if (IsPathCaptured(virtualDir))
			return CacheHelper.GetOrAddAsync(string.Concat("wsvppd_", virtualDir), () => GetDirectoryAsyncInternal(virtualDir));

		return AsyncPrevious?.GetDirectoryAsync(virtualDir) ?? Task.FromResult(Previous.GetDirectory(virtualDir));
	}

	/// <summary>Get the basic metadata associated with an image file at the given <paramref name="virtualPath" />.</summary>
	/// <param name="virtualPath">The virtual path of the image file.</param>
	/// <returns>The <see cref="ImageFileInfo" />.</returns>
	public virtual Task<ImageFileInfo> GetImageInfoAsync(string virtualPath)
	{
		if (IsPathCaptured(virtualPath))
			return CacheHelper.GetOrAddAsync(string.Concat("wsvppfi_", virtualPath), () => GetImageInfoAsyncInternal(virtualPath));

		return AsyncPrevious?.GetImageInfoAsync(virtualPath) ?? CacheHelper.GetImageInfoAsync(Previous, virtualPath);
	}
}

/// <inheritdoc />
public class DiskCachedVirtualFile : VirtualFile
{
	private readonly string cachePath;

	/// <inheritdoc cref="VirtualFile(string)" />
	public DiskCachedVirtualFile(string virtualPath, string cachePath) : base(virtualPath) => this.cachePath = cachePath;

	/// <inheritdoc />
	public override Stream Open() => new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
}

/// <inheritdoc />
public class MemoryCachedVirtualFile : VirtualFile
{
	private readonly byte[] data;

	/// <inheritdoc cref="VirtualFile(string)" />
	public MemoryCachedVirtualFile(string virtualPath, byte[] data) : base(virtualPath) => this.data = data;

	/// <inheritdoc />
	public override Stream Open() => new MemoryStream(data);
}

/// <inheritdoc />
public abstract class AsyncVirtualFile : VirtualFile
{
	/// <inheritdoc />
	protected AsyncVirtualFile(string virtualPath) : base(virtualPath) { }

	/// <inheritdoc cref="FileSystemInfo.LastAccessTimeUtc" />
	public DateTime LastModified { get; protected set; }

	/// <inheritdoc cref="Open" />
	public abstract Task<Stream> OpenAsync();

	/// <inheritdoc />
	public override Stream Open() => OpenAsync().GetAwaiter().GetResult();
}
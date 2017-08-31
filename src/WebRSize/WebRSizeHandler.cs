using System;
using System.IO;
using System.Web;
using System.Web.Hosting;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.WebRSize
{
	public class WebRSizeHandler : HttpTaskAsyncHandler
	{
		private struct QueueReleaser : IDisposable { public void Dispose() => sem.Release(); }

		private static readonly SemaphoreSlim sem = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
		private static readonly ConcurrentDictionary<string, Task<ArraySegment<byte>>> tdic = new ConcurrentDictionary<string, Task<ArraySegment<byte>>>();
		private static readonly Lazy<Type> mpbvfType = new Lazy<Type>(() => Assembly.GetAssembly(typeof(HostingEnvironment)).GetType("System.Web.Hosting.MapPathBasedVirtualFile", true));

		private static Task<QueueReleaser> enterWorkQueueAsync()
		{
			var wait = sem.WaitAsync();
			return wait.IsCompleted ? CacheHelper.CompletedTask<QueueReleaser>.Default : wait.ContinueWith(_ => new QueueReleaser(), TaskContinuationOptions.ExecuteSynchronously);
		}

		private static void saveResult(TaskCompletionSource<ArraySegment<byte>> tcs, MemoryStream oimg, string cachePath, DateTime lastWrite)
		{
			oimg.TryGetBuffer(out var bytes);
			tcs.SetResult(bytes);

			HostingEnvironment.QueueBackgroundWorkItem(async __ => { try {
				var fi = new FileInfo(cachePath);
				if (!fi.Exists)
				{
					if (!fi.Directory.Exists)
						fi.Directory.Create();

					string tmpPath = $"{cachePath}.tmp";
					using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
						await fs.WriteAsync(bytes.Array, bytes.Offset, bytes.Count);

					if (lastWrite > DateTime.MinValue)
						File.SetLastWriteTimeUtc(tmpPath, lastWrite);

					File.Move(tmpPath, cachePath);
				}

				tdic.TryRemove(cachePath, out _);
			} catch { }});
		}

		private static async Task process(TaskCompletionSource<ArraySegment<byte>> tcs, string reqPath, string cachePath, ProcessImageSettings s)
		{
			try
			{
				if (reqPath == WebRSizeModule.NotFoundPath)
				{
					using (await enterWorkQueueAsync())
					using (var oimg = new MemoryStream(8192))
					{
						GdiImageProcessor.CreateBrokenImage(oimg, s);
						oimg.Position = 0;
						saveResult(tcs, oimg, cachePath, DateTime.MinValue);
						return;
					}
				}

				var vpp = HostingEnvironment.VirtualPathProvider;
				var vppAsync = vpp as CachingAsyncVirtualPathProvider;

				var file = vppAsync != null ? await vppAsync.GetFileAsync(reqPath) : vpp.GetFile(reqPath);
				var afile = file as AsyncVirtualFile;

				var lastWrite = afile?.LastModified ?? DateTime.MinValue;
				if (lastWrite == DateTime.MinValue && mpbvfType.Value.IsAssignableFrom(file.GetType()))
					lastWrite = File.GetLastWriteTimeUtc(HostingEnvironment.MapPath(reqPath));

				using (var iimg = afile != null ? await afile.OpenAsync() : file.Open())
				using (await enterWorkQueueAsync())
				using (var oimg = new MemoryStream(16384))
				{
					MagicImageProcessor.ProcessImage(iimg, oimg, s);

					saveResult(tcs, oimg, cachePath, lastWrite);
				}
			}
			catch (Exception ex)
			{
				tcs.SetException(ex);

				tdic.TryRemove(cachePath, out _);
			}
		}

		public override async Task ProcessRequestAsync(HttpContext ctx)
		{
			var s = ctx.Items[WebRSizeModule.ProcessImageSettingsKey] as ProcessImageSettings;
			var cachePath = ctx.Items[WebRSizeModule.CachePathKey] as string;

			var tsource = default(TaskCompletionSource<ArraySegment<byte>>);
			var task = tdic.GetOrAdd(cachePath, _ => {
				tsource = new TaskCompletionSource<ArraySegment<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
				return tsource.Task;
			});

			if (tsource?.Task == task)
			{
				ctx.Trace.Write("ProcessImage Begin");
				await process(tsource, ctx.Request.Path, cachePath, s);
				ctx.Trace.Write("ProcessImage End");
			}

			var res = await task;

			if (!ctx.Response.IsClientConnected)
				return;

			ctx.Response.BufferOutput = false;
			ctx.Response.ContentType = MimeMapping.GetMimeMapping(Path.GetFileName(cachePath));
			ctx.Response.AddHeader("Content-Length", res.Count.ToString());
			ctx.Response.Cache.SetLastModifiedFromFileDependencies();

			await ctx.Response.OutputStream.WriteAsync(res.Array, res.Offset, res.Count);
		}

		public override bool IsReusable => true;
	}
}

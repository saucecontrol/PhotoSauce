using System;
using System.IO;
using System.Web;
using System.Web.Hosting;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.WebRSize
{
	/// <summary>An <see cref="IHttpHandler" /> implementation that performs a dynamic image processing operation, returns the result to the client, and queues caching the result.</summary>
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
					{
						await fs.WriteAsync(bytes.Array, bytes.Offset, bytes.Count);
						await fs.FlushAsync();
					}

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
#pragma warning disable 0618 // obsolete
						GdiImageProcessor.CreateBrokenImage(oimg, s);
#pragma warning restore 0618
						oimg.Position = 0;
						saveResult(tcs, oimg, cachePath, DateTime.MinValue);
						return;
					}
				}

				var vpp = HostingEnvironment.VirtualPathProvider;

				var file = vpp is CachingAsyncVirtualPathProvider vppAsync ? await vppAsync.GetFileAsync(reqPath) : vpp.GetFile(reqPath);
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

		/// <inheritdoc />
		public override async Task ProcessRequestAsync(HttpContext ctx)
		{
			string cachePath = ctx.Items[WebRSizeModule.CachePathKey] as string;
			var s = ctx.Items[WebRSizeModule.ProcessImageSettingsKey] as ProcessImageSettings;

			var tsource = default(TaskCompletionSource<ArraySegment<byte>>);
			var task = tdic.GetOrAdd(cachePath, _ => {
				tsource = new TaskCompletionSource<ArraySegment<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
				return tsource.Task;
			});

			if (tsource?.Task == task)
			{
				ctx.Trace.Write(nameof(WebRSize), nameof(MagicImageProcessor.ProcessImage) + " Begin");
				await process(tsource, ctx.Request.Path, cachePath, s);
				ctx.Trace.Write(nameof(WebRSize), nameof(MagicImageProcessor.ProcessImage) + " End");
			}

			var img = await task;
			var res = ctx.Response;

			if (!res.IsClientConnected)
				return;

			try
			{
				res.BufferOutput = false;
				res.ContentType = MimeMapping.GetMimeMapping(Path.GetFileName(cachePath));
				res.AddHeader("Content-Length", img.Count.ToString());

				await res.OutputStream.WriteAsync(img.Array, img.Offset, img.Count);
				await res.OutputStream.FlushAsync();
			}
			catch (HttpException ex) when (new StackTrace(ex).GetFrame(0)?.GetMethod().Name == "RaiseCommunicationError")
			{
				// no problem here.  client just disconnected before transmission completed.
			}
		}

		/// <inheritdoc />
		public override bool IsReusable => true;
	}
}

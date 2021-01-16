// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System.Web;
using System.Threading;

[assembly: PreApplicationStartMethod(typeof(PhotoSauce.WebRSize.PreApplicationStart), nameof(PhotoSauce.WebRSize.PreApplicationStart.Start))]

namespace PhotoSauce.WebRSize
{
	/// <inheritdoc cref="PreApplicationStartMethodAttribute" />
	public static class PreApplicationStart
	{
		private static volatile int initialized = 0;

		/// <summary>PreApplicationStartMethod</summary>
		public static void Start()
		{
			if (Interlocked.Exchange(ref initialized, 1) == 0)
				HttpApplication.RegisterModule(typeof(WebRSizeModule));
		}
	}
}

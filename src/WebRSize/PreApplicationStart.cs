using System.Web;
using System.Threading;

[assembly: PreApplicationStartMethod(typeof(PhotoSauce.WebRSize.PreApplicationStart), "Start")]

namespace PhotoSauce.WebRSize
{
	public static class PreApplicationStart
	{
		private static volatile int initialized = 0;

		public static void Start()
		{
			if (Interlocked.Exchange(ref initialized, 1) == 0)
				HttpApplication.RegisterModule(typeof(WebRSizeModule));
		}
	}
}

using System.IO;
using System.Web;
using System.Text;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.WebRSize
{
	public interface ICacheFileNamingStrategy
	{
		string GetCacheFilePath(string inPath, ProcessImageSettings settings);
	}

	public abstract class CacheFileNamingStrategyBase : ICacheFileNamingStrategy
	{
		private static readonly string cacheRoot = VirtualPathUtility.AppendTrailingSlash(VirtualPathUtility.ToAppRelative(WebRSizeConfig.Current.DiskCache.Path));

		protected string WrapFileName(string newPath, ProcessImageSettings settings, bool includeResolution = true)
		{
			string file = VirtualPathUtility.GetFileName(newPath);
			var sb = new StringBuilder(128);
			sb.Append(VirtualPathUtility.GetDirectory(newPath).Substring(1));
			sb.Append('/');
			sb.Append(settings.GetCacheHash());
			sb.Append('.');
			sb.Append(Path.GetFileNameWithoutExtension(file));

			if (includeResolution)
				sb.AppendFormat(".{0}x{1}", settings.Width, settings.Height);

			sb.Append('.');
			sb.Append(settings.SaveFormat.GetFileExtension(Path.GetExtension(file)));

			return VirtualPathUtility.Combine(cacheRoot, sb.ToString());
		}

		public abstract string GetCacheFilePath(string inPath, ProcessImageSettings settings);
	}

	public class CacheFileNamingStrategyMirror : CacheFileNamingStrategyBase
	{
		public override string GetCacheFilePath(string inPath, ProcessImageSettings settings) => WrapFileName(inPath, settings);
	}

	public class CacheFileNamingStrategyDistribute1K : CacheFileNamingStrategyBase
	{
		public override string GetCacheFilePath(string inPath, ProcessImageSettings settings)
		{
			string hash = CacheHash.Create(inPath);
			return WrapFileName($"/{hash.Substring(0, 2)}/{VirtualPathUtility.GetFileName(inPath)}", settings);
		}
	}

	public class CacheFileNamingStrategyDistribute1M : CacheFileNamingStrategyBase
	{
		public override string GetCacheFilePath(string inPath, ProcessImageSettings settings)
		{
			string hash = CacheHash.Create(inPath);
			return WrapFileName($"/{hash.Substring(0, 2)}/{hash.Substring(2, 2)}/{VirtualPathUtility.GetFileName(inPath)}", settings);
		}
	}
}

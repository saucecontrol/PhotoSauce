using System.Web;

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
			file = $"{settings.GetCacheHash()}.{file}";
			if (includeResolution)
				file = file.Insert(file.LastIndexOf('.'), $".{settings.Width}x{settings.Height}");

			return VirtualPathUtility.Combine(cacheRoot, $"{VirtualPathUtility.GetDirectory(newPath).Substring(1)}/{file}");
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

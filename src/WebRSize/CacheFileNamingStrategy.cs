using System.IO;
using System.Web;
using System.Text;

using PhotoSauce.MagicScaler;

namespace PhotoSauce.WebRSize
{
	/// <summary>Provides a mechanism for customizing cache file names.</summary>
	public interface ICacheFileNamingStrategy
	{
		/// <summary>Generates a cache file name for the given path and settings.</summary>
		/// <param name="inPath">The original image path.</param>
		/// <param name="settings">The settings to be used for processing.</param>
		/// <returns>The generated cache path.</returns>
		string GetCacheFilePath(string inPath, ProcessImageSettings settings);
	}

	/// <summary>Provides a partial base implementation for <see cref="ICacheFileNamingStrategy" />.</summary>
	public abstract class CacheFileNamingStrategyBase : ICacheFileNamingStrategy
	{
		private static readonly string cacheRoot = VirtualPathUtility.AppendTrailingSlash(VirtualPathUtility.ToAppRelative(WebRSizeConfig.Current.DiskCache.Path));

		/// <summary>Updates a cache file path by appending the settings hash and optionally output size to the original file name.</summary>
		/// <param name="newPath">The undecorated cache file path.</param>
		/// <param name="settings">The processing settings.</param>
		/// <param name="includeResolution">True to include the output width and height in the file name, false to omit.</param>
		/// <returns>The updated cache file path.</returns>
		protected static string WrapFileName(string newPath, ProcessImageSettings settings, bool includeResolution = true)
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

		/// <inheritdoc />
		public abstract string GetCacheFilePath(string inPath, ProcessImageSettings settings);
	}

	/// <summary>Generates cache paths that mirror the folder structure of the original images.</summary>
	public class CacheFileNamingStrategyMirror : CacheFileNamingStrategyBase
	{
		/// <inheritdoc />
		public override string GetCacheFilePath(string inPath, ProcessImageSettings settings) => WrapFileName(inPath, settings);
	}

	/// <summary>Generates cache paths by distibuting them over up to 1024 subfolders.</summary>
	public class CacheFileNamingStrategyDistribute1K : CacheFileNamingStrategyBase
	{
		/// <inheritdoc />
		public override string GetCacheFilePath(string inPath, ProcessImageSettings settings)
		{
			string hash = CacheHash.Create(inPath);
			return WrapFileName($"/{hash[0]}{hash[1]}/{VirtualPathUtility.GetFileName(inPath)}", settings);
		}
	}

	/// <summary>Generates cache paths by distibuting them over up to 1024 subfolders with up to 1024 subfolders each.</summary>
	public class CacheFileNamingStrategyDistribute1M : CacheFileNamingStrategyBase
	{
		/// <inheritdoc />
		public override string GetCacheFilePath(string inPath, ProcessImageSettings settings)
		{
			string hash = CacheHash.Create(inPath);
			return WrapFileName($"/{hash[0]}{hash[1]}/{hash[2]}{hash[3]}/{VirtualPathUtility.GetFileName(inPath)}", settings);
		}
	}
}

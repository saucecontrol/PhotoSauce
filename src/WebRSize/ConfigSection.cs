using System;
using System.Linq;
using System.Configuration;
using System.Globalization;
using System.ComponentModel;
using System.Web.Compilation;
using System.Web.Configuration;
using System.Collections.Generic;

namespace PhotoSauce.WebRSize
{
	public class WebRSizeSection : ConfigurationSection
	{
		[ConfigurationProperty("diskCache", IsRequired = true)]
		public DiskCacheElement DiskCache => (DiskCacheElement)base["diskCache"];

		[ConfigurationProperty("imageFolders", IsRequired = true), ConfigurationCollection(typeof(NameKeyedConfigurationElemementCollection<ImageFolder>))]
		public NameKeyedConfigurationElemementCollection<ImageFolder> ImageFolders => (NameKeyedConfigurationElemementCollection<ImageFolder>)base["imageFolders"];
	}

	public class DiskCacheElement : ConfigurationElement
	{
		[ConfigurationProperty("enabled", IsRequired = false, DefaultValue = true)]
		public bool Enabled => (bool)base["enabled"];

		[ConfigurationProperty("path", IsRequired = true)]
		public string Path => (string)base["path"];

		[ConfigurationProperty("namingStrategy", IsRequired = false, DefaultValue = typeof(CacheFileNamingStrategyMirror)), TypeConverter(typeof(SimplifiedTypeNameConverter))]
		public Type NamingStrategy => (Type)base["namingStrategy"];
	}

	public class ImageFolder : NameKeyedConfigurationElement
	{
		[ConfigurationProperty("path", IsRequired = true)]
		public string Path => (string)base["path"];

		[ConfigurationProperty("forceProcessing", IsRequired = false, DefaultValue = false)]
		public bool ForceProcessing => (bool)base["forceProcessing"];

		[ConfigurationProperty("allowEnlarge", IsRequired = false, DefaultValue = false)]
		public bool AllowEnlarge => (bool)base["allowEnlarge"];

		[ConfigurationProperty("maxPixels", IsRequired = false, DefaultValue = 10000000)]
		public int MaxPixels => (int)base["maxPixels"];

		[ConfigurationProperty("defaultSettings"), ConfigurationCollection(typeof(KeyValueConfigurationCollection))]
		public KeyValueConfigurationCollection DefaultSettings => (KeyValueConfigurationCollection)base["defaultSettings"];
	}

	public class NameKeyedConfigurationElement : ConfigurationElement
	{
		[ConfigurationProperty("name", IsRequired = true, IsKey = true), TypeConverter(typeof(WhiteSpaceTrimStringConverter))]
		public string Name => (string)base["name"];
	}

	public class NameKeyedConfigurationElemementCollection<T> : ConfigurationElementCollection, IEnumerable<T> where T : NameKeyedConfigurationElement, new()
	{
		public NameKeyedConfigurationElemementCollection() : base(StringComparer.OrdinalIgnoreCase) { }

		public new T this[string key] => BaseGet(key) as T;

		protected override ConfigurationElement CreateNewElement() => new T();

		protected override object GetElementKey(ConfigurationElement element) => ((NameKeyedConfigurationElement)element).Name;

		public new IEnumerator<T> GetEnumerator() => this.OfType<T>().GetEnumerator();
	}

	internal class SimplifiedTypeNameConverter : ConfigurationConverterBase
	{
		public override object ConvertFrom(ITypeDescriptorContext ctx, CultureInfo ci, object data) => BuildManager.GetType(data.ToString(), true);

		public override object ConvertTo(ITypeDescriptorContext ctx, CultureInfo ci, object value, Type type) => value != null ? ((Type)value).AssemblyQualifiedName : null;
	}

	public static class WebRSizeConfig
	{
		public static WebRSizeSection Current { get; } = WebConfigurationManager.GetWebApplicationSection("webrsize") as WebRSizeSection ?? new WebRSizeSection();
	}
}
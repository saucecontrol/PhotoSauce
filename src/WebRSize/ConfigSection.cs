// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Linq;
using System.Configuration;
using System.Globalization;
using System.ComponentModel;
using System.Web.Compilation;
using System.Web.Configuration;
using System.Collections.Generic;

namespace PhotoSauce.WebRSize;

/// <inheritdoc />
public class WebRSizeSection : ConfigurationSection
{
	const string diskCache = nameof(diskCache);
	const string imageFolders = nameof(imageFolders);

	/// <summary>Represents the <see cref="diskCache"/> element.</summary>
	[ConfigurationProperty(diskCache, IsRequired = true)]
	public DiskCacheElement DiskCache => (DiskCacheElement)base[diskCache];

	/// <summary>Represents the <see cref="imageFolders"/> element.</summary>
	[ConfigurationProperty(imageFolders, IsRequired = true), ConfigurationCollection(typeof(NameKeyedConfigurationElemementCollection<ImageFolder>))]
	public NameKeyedConfigurationElemementCollection<ImageFolder> ImageFolders => (NameKeyedConfigurationElemementCollection<ImageFolder>)base[imageFolders];
}

/// <inheritdoc />
public class DiskCacheElement : ConfigurationElement
{
	const string enabled = nameof(enabled);
	const string path = nameof(path);
	const string namingStrategy = nameof(namingStrategy);

	/// <summary>Represents the <see cref="enabled"/> attribute.</summary>
	[ConfigurationProperty(enabled, IsRequired = false, DefaultValue = true)]
	public bool Enabled => (bool)base[enabled];

	/// <summary>Represents the <see cref="path"/> attribute.</summary>
	[ConfigurationProperty(path, IsRequired = true)]
	public string Path => (string)base[path];

	/// <summary>Represents the <see cref="namingStrategy"/> attribute.</summary>
	[ConfigurationProperty(namingStrategy, IsRequired = false, DefaultValue = typeof(CacheFileNamingStrategyMirror)), TypeConverter(typeof(SimplifiedTypeNameConverter))]
	public Type NamingStrategy => (Type)base[namingStrategy];
}

/// <inheritdoc />
public class ImageFolder : NameKeyedConfigurationElement
{
	const string path = nameof(path);
	const string forceProcessing = nameof(forceProcessing);
	const string allowEnlarge = nameof(allowEnlarge);
	const string maxPixels = nameof(maxPixels);
	const string defaultSettings = nameof(defaultSettings);

	/// <summary>Represents the <see cref="path"/> attribute.</summary>
	[ConfigurationProperty(path, IsRequired = true)]
	public string Path => (string)base[path];

	/// <summary>Represents the <see cref="forceProcessing"/> attribute.</summary>
	[ConfigurationProperty(forceProcessing, IsRequired = false, DefaultValue = false)]
	public bool ForceProcessing => (bool)base[forceProcessing];

	/// <summary>Represents the <see cref="allowEnlarge"/> attribute.</summary>
	[ConfigurationProperty(allowEnlarge, IsRequired = false, DefaultValue = false)]
	public bool AllowEnlarge => (bool)base[allowEnlarge];

	/// <summary>Represents the <see cref="maxPixels"/> attribute.</summary>
	[ConfigurationProperty(maxPixels, IsRequired = false, DefaultValue = 10000000)]
	public int MaxPixels => (int)base[maxPixels];

	/// <summary>Represents the <see cref="defaultSettings"/> element.</summary>
	[ConfigurationProperty(defaultSettings), ConfigurationCollection(typeof(KeyValueConfigurationCollection))]
	public KeyValueConfigurationCollection DefaultSettings => (KeyValueConfigurationCollection)base[defaultSettings];
}

/// <inheritdoc />
public class NameKeyedConfigurationElement : ConfigurationElement
{
	const string name = nameof(name);

	/// <summary>Represents the <see cref="name"/> attribute.</summary>
	[ConfigurationProperty(name, IsRequired = true, IsKey = true), TypeConverter(typeof(WhiteSpaceTrimStringConverter))]
	public string Name => (string)base[name];
}

/// <inheritdoc />
public class NameKeyedConfigurationElemementCollection<T> : ConfigurationElementCollection, IEnumerable<T> where T : NameKeyedConfigurationElement, new()
{
	/// <inheritdoc />
	public NameKeyedConfigurationElemementCollection() : base(StringComparer.OrdinalIgnoreCase) { }

	/// <inheritdoc cref="ConfigurationElementCollection.BaseGet(object)" />
	public new T this[string key] => BaseGet(key) as T;

	/// <inheritdoc />
	protected override ConfigurationElement CreateNewElement() => new T();

	/// <inheritdoc />
	protected override object GetElementKey(ConfigurationElement element) => ((NameKeyedConfigurationElement)element).Name;

	/// <inheritdoc />
	public new IEnumerator<T> GetEnumerator() => this.OfType<T>().GetEnumerator();
}

internal class SimplifiedTypeNameConverter : ConfigurationConverterBase
{
	public override object ConvertFrom(ITypeDescriptorContext ctx, CultureInfo ci, object data) => BuildManager.GetType(data.ToString(), true);

	public override object ConvertTo(ITypeDescriptorContext ctx, CultureInfo ci, object value, Type type) => value is null ? null : ((Type)value).AssemblyQualifiedName;
}

/// <summary>A helper class for the <see cref="WebRSizeSection" /> configuration.</summary>
public static class WebRSizeConfig
{
	/// <summary>Gets the current <see cref="WebRSizeSection" /> configuration.</summary>
	/// <value>The <see cref="WebRSizeSection" /> representing the current config.</value>
	public static WebRSizeSection Current { get; } = WebConfigurationManager.GetWebApplicationSection("webrsize") as WebRSizeSection ?? new WebRSizeSection();
}
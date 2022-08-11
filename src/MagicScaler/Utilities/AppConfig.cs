// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Globalization;

namespace PhotoSauce.MagicScaler;

internal class AppConfig
{
	private const string prefix = $"{nameof(PhotoSauce)}.{nameof(MagicScaler)}";

	public const string MaxPooledBufferSizeName = $"{prefix}.{nameof(MaxPooledBufferSize)}";
	public const string EnablePixelSourceStatsName = $"{prefix}.{nameof(EnablePixelSourceStats)}";
	public const string ThrowOnFinalizerName = $"{prefix}.{nameof(ThrowOnFinalizer)}";

	public static readonly int MaxPooledBufferSize = getAppContextInt(MaxPooledBufferSizeName);
	public static readonly bool EnablePixelSourceStats = AppContext.TryGetSwitch(EnablePixelSourceStatsName, out bool val) && val;
	public static readonly bool ThrowOnFinalizer = AppContext.TryGetSwitch(ThrowOnFinalizerName, out bool val) && val;

	private static int getAppContextInt(string name)
	{
#if NETFRAMEWORK
		var data = AppDomain.CurrentDomain.GetData(name);
#else
		var data = AppContext.GetData(name);
#endif

		if (data is int val || ((data is string s) && int.TryParse(s, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out val)))
			return val;

		return default;
	}
}

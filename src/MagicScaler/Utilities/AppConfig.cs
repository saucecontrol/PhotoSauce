// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Globalization;

namespace PhotoSauce.MagicScaler;

internal class AppConfig
{
	private const string prefix = $"{nameof(PhotoSauce)}.{nameof(MagicScaler)}";

	public static readonly int MaxPooledBufferSize = getAppContextInt($"{prefix}.{nameof(MaxPooledBufferSize)}");
	public static readonly bool EnablePixelSourceStats = AppContext.TryGetSwitch($"{prefix}.{nameof(EnablePixelSourceStats)}", out bool val) && val;
	public static readonly bool GdsMitigationsDisabled = AppContext.TryGetSwitch($"{prefix}.{nameof(GdsMitigationsDisabled)}", out bool val) && val;
	public static readonly bool EnableWindowsLcms = AppContext.TryGetSwitch($"{prefix}.{nameof(EnableWindowsLcms)}", out bool val) && val;
	public static readonly bool ThrowOnFinalizer = AppContext.TryGetSwitch($"{prefix}.{nameof(ThrowOnFinalizer)}", out bool val) && val;

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

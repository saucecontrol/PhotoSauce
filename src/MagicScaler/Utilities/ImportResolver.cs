// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

#if NET5_0_OR_GREATER
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace PhotoSauce.MagicScaler;

internal static class ImportResolver
{
	[ModuleInitializer]
	public static void ModuleInitializer() => NativeLibrary.SetDllImportResolver(typeof(ImportResolver).Assembly, ResolveLibrary);

	public static nint ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
	{
		if (OperatingSystem.IsLinux() && libraryName == "lcms2")
		{
			if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out nint handle))
				return handle;
			else
				libraryName = "lcms2.so.2";
		}

		return NativeLibrary.Load(libraryName, assembly, searchPath);
	}
}
#endif

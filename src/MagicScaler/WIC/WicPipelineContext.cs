// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

using TerraFX.Interop;

namespace PhotoSauce.MagicScaler
{
	internal sealed unsafe class WicPipelineContext : IDisposable
	{
		public IWICColorContext* SourceColorContext { get; set; }
		public IWICColorContext* DestColorContext { get; set; }
		public IWICPalette* DestPalette { get; set; }

		public void Dispose()
		{
			if (DestPalette is null)
				return;

			DestPalette->Release();
			DestPalette = null;
		}
	}
}

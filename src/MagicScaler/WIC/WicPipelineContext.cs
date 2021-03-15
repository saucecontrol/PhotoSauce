// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

using TerraFX.Interop;

namespace PhotoSauce.MagicScaler
{
	internal sealed unsafe class WicPipelineContext : IDisposable
	{
		//TODO The ownership model for the IWICColorContext references stored here is funky because they belong to either a shared
		//profile or to some other WIC component. Therefore we don't release them on Dispose. Shared ownership should be made explicit.
		public IWICColorContext* SourceColorContext { get; set; }
		public IWICColorContext* DestColorContext { get; set; }
		public IWICPalette* DestPalette { get; set; }

		public void Dispose()
		{
			dispose(true);
			GC.SuppressFinalize(this);
		}

		private void dispose(bool disposing)
		{
			if (DestPalette is null)
				return;

			DestPalette->Release();
			DestPalette = null;
		}

		~WicPipelineContext() => dispose(false);
	}
}

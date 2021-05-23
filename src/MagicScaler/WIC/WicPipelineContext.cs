// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;

using TerraFX.Interop;

namespace PhotoSauce.MagicScaler
{
	internal sealed unsafe class WicPipelineContext : IDisposable
	{
		public IWICColorContext* SourceColorContext { get; set; }
		public IWICColorContext* DestColorContext { get; set; }

		public void Dispose()
		{
			dispose(true);
			GC.SuppressFinalize(this);
		}

		private void dispose(bool disposing)
		{
			if (SourceColorContext is not null)
			{
				SourceColorContext->Release();
				SourceColorContext = null;
			}

			if (DestColorContext is not null)
			{
				DestColorContext->Release();
				DestColorContext = null;
			}
		}

		~WicPipelineContext() => dispose(false);
	}
}

// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;

using TerraFX.Interop.Windows;

namespace PhotoSauce.MagicScaler;

internal sealed unsafe class WicPipelineContext : IDisposable
{
	public IWICColorContext* SourceColorContext { get; set; }
	public IWICColorContext* DestColorContext { get; set; }

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

		if (disposing)
			GC.SuppressFinalize(this);
	}

	public void Dispose() => dispose(true);

	~WicPipelineContext()
	{
		ThrowHelper.ThrowIfFinalizerExceptionsEnabled(nameof(WicPipelineContext));

		dispose(false);
	}
}

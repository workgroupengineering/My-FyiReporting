using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Majorsilence.Reporting.Rdl;
using SkiaSharp;

namespace Majorsilence.Reporting.UI.RdlAvalonia.Viewer
{
    public sealed class ReportCanvas : Control
    {
        private Pages? _pages;
        private Report? _report;
        private int _pageIndex;
        private double _zoom = 1.0;
        private WriteableBitmap? _bitmap;
        private bool _needsRender = true;

        public void SetReport(Report? report, Pages? pages)
        {
            _report = report;
            _pages = pages;
            _pageIndex = 0;
            _needsRender = true;
            InvalidateVisual();
        }

        public void SetPage(int pageIndex)
        {
            _pageIndex = Math.Max(0, pageIndex);
            _needsRender = true;
            InvalidateVisual();
        }

        public void SetZoom(double zoom)
        {
            _zoom = Math.Max(0.1, zoom);
            _needsRender = true;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (_pages == null || _pages.PageCount == 0)
            {
                return;
            }

            if (_needsRender || _bitmap == null)
            {
                RenderPage();
            }

            if (_bitmap != null)
            {
                context.DrawImage(_bitmap, new Rect(0, 0, _bitmap.Size.Width, _bitmap.Size.Height));
            }
        }

        private void RenderPage()
        {
            if (_pages == null)
            {
                return;
            }

            var pageIndex = Math.Clamp(_pageIndex, 0, _pages.PageCount - 1);
            var scale = VisualRoot?.RenderScaling ?? 1.0;
            var dpi = 96.0 * scale;
            var pixelWidth = Math.Max(1, (int)Math.Ceiling(_pages.PageWidth * dpi / 72.0 * _zoom));
            var pixelHeight = Math.Max(1, (int)Math.Ceiling(_pages.PageHeight * dpi / 72.0 * _zoom));

            _bitmap = new WriteableBitmap(
                new PixelSize(pixelWidth, pixelHeight),
                new Vector(dpi, dpi),
                PixelFormats.Bgra8888,
                AlphaFormat.Premul);

            using var framebuffer = _bitmap.Lock();
            var info = new SKImageInfo(framebuffer.Size.Width, framebuffer.Size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, framebuffer.Address, framebuffer.RowBytes);
            if (surface == null)
            {
                return;
            }

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            using var g = new Majorsilence.Drawing.Graphics(canvas)
            {
                DpiX = (float)dpi,
                DpiY = (float)dpi,
                PageUnit = Majorsilence.Drawing.GraphicsUnit.Pixel
            };

            var renderer = new SkiaPageDrawing(_pages, (float)_zoom);
            renderer.Draw(g, pageIndex);

            surface.Flush();
            _needsRender = false;
        }
    }
}


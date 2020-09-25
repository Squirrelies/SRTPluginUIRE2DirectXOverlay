using GameOverlay.Drawing;
using GameOverlay.Windows;
using SRTPluginBase;
using SRTPluginProviderRE2;
using System;
using System.Runtime.InteropServices;

namespace SRTPluginUIRE2DirectXOverlay
{
    public class SRTPluginUIRE2DirectXOverlay : IPluginUI
    {
        internal static PluginInfo _Info = new PluginInfo();
        public IPluginInfo Info => _Info;
        public string RequiredProvider => "SRTPluginProviderRE2";
        private IPluginHostDelegates hostDelegates;
        private IGameMemoryRE2 gameMemory;

        // DirectX Overlay-specific.
        private OverlayWindow _window;
        private Graphics _graphics;
        private SharpDX.Direct2D1.WindowRenderTarget _device;
        private Font _font;
        private SolidBrush _black;
        private SolidBrush _red;
        private SolidBrush _green;
        private SolidBrush _blue;
        private SharpDX.Direct2D1.Bitmap _invItemSheet1;

        [STAThread]
        public int Startup(IPluginHostDelegates hostDelegates)
        {
            this.hostDelegates = hostDelegates;

            DEVMODE devMode = default;
            devMode.dmSize = (short)Marshal.SizeOf<DEVMODE>();
            PInvoke.EnumDisplaySettings(null, -1, ref devMode);

            _graphics = new Graphics()
            {
                MeasureFPS = true,
                PerPrimitiveAntiAliasing = false,
                TextAntiAliasing = true,
                UseMultiThreadedFactories = false,
                VSync = false
            };

            _window = new OverlayWindow(0, 0, devMode.dmPelsWidth, devMode.dmPelsHeight)
            {
                IsTopmost = true,
                IsVisible = true
            };

            _window.Create();

            _graphics.Width = _window.Width;
            _graphics.Height = _window.Height;
            _graphics.WindowHandle = _window.Handle; // set the target handle before calling Setup()
            _graphics.Setup();

            _device = (SharpDX.Direct2D1.WindowRenderTarget)typeof(Graphics).GetField("_device", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_graphics);

            // creates a simple font with no additional style
            _font = _graphics.CreateFont("Consolas", 12);

            // colors for brushes will be automatically normalized. 0.0f - 1.0f and 0.0f - 255.0f is accepted!
            _black = _graphics.CreateSolidBrush(0, 0, 0);
            _red = _graphics.CreateSolidBrush(Color.Red); // those are the only pre defined Colors
            _green = _graphics.CreateSolidBrush(Color.Green);
            _blue = _graphics.CreateSolidBrush(Color.Blue);

            //_invItemSheet1 = _graphics.CreateImage(Properties.Resources.ui0100_iam_texout).Bitmap;

            return 0;
        }

        public int Shutdown()
        {
            _graphics.Dispose();
            _window.Dispose();
            return 0;
        }

        public int ReceiveData(object gameMemory)
        {
            try
            {
                this.gameMemory = (IGameMemoryRE2)gameMemory;
                _graphics.BeginScene();
                _graphics.ClearScene();
                DrawOverlay();
            }
            finally
            {
                _graphics.EndScene();
            }
            return 0;
        }

        private void DrawOverlay()
        {
            _graphics.DrawText(_font, _red, 50, 50, string.Format("HP: {0}", this.gameMemory.PlayerCurrentHealth));
            //DrawInventoryIcon(0, 0, 0, 0);
            //DrawInventoryIcon(112, 0, 1, 0);
            //DrawInventoryIcon(0, 112, 0, 1);
            //DrawInventoryIcon(112, 112, 1, 1);
        }

        public void DrawInventoryIcon(float x, float y, int iconCol, int iconRow, bool dualSlot = false)
        {
            SharpDX.Mathematics.Interop.RawRectangleF[] invIconRects = GetInventoryIconRectangles(x, y, iconCol, iconRow, dualSlot);
            _device.DrawBitmap(_invItemSheet1, invIconRects[0], 1f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear, invIconRects[1]);
        }

        public SharpDX.Mathematics.Interop.RawRectangleF[] GetInventoryIconRectangles(float drawX, float drawY, float iconX, float iconY, float iconWidth = 112f, float iconHeight = 112f) =>
             new SharpDX.Mathematics.Interop.RawRectangleF[2]
             {
                 new SharpDX.Mathematics.Interop.RawRectangleF(drawX, drawY, drawX + iconWidth, drawY + iconHeight),
                 new SharpDX.Mathematics.Interop.RawRectangleF(iconX, iconY, iconX + iconWidth, iconY + iconHeight)
             };

        public SharpDX.Mathematics.Interop.RawRectangleF[] GetInventoryIconRectangles(float drawX, float drawY, int iconCol, int iconRow, bool dualSlot = false, float iconWidth = 112f, float iconHeight = 112f) =>
            new SharpDX.Mathematics.Interop.RawRectangleF[2]
            {
                 new SharpDX.Mathematics.Interop.RawRectangleF(drawX, drawY, drawX + (iconWidth * (dualSlot ? 2 : 1)), drawY + iconHeight),
                 new SharpDX.Mathematics.Interop.RawRectangleF(iconCol * (iconWidth * (dualSlot ? 2 : 1)), iconRow * iconHeight, (iconCol * (iconWidth * (dualSlot ? 2 : 1))) + (iconWidth * (dualSlot ? 2 : 1)), (iconRow * iconHeight) + iconHeight)
            };
    }
}

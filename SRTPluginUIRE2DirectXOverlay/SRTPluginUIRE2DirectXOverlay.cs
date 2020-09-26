﻿using GameOverlay.Drawing;
using GameOverlay.Windows;
using SRTPluginBase;
using SRTPluginProviderRE2;
using SRTPluginProviderRE2.Structures;
using System;
using System.Collections.Generic;
using System.Linq;
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

        private Font _consolasBold;

        private SolidBrush _black;
        private SolidBrush _white;
        private SolidBrush _grey;
        private SolidBrush _darkred;
        private SolidBrush _red;
        private SolidBrush _lawngreen;
        private SolidBrush _goldenrod;

        private IReadOnlyDictionary<ItemEnumeration, SharpDX.Mathematics.Interop.RawRectangleF> itemToImageTranslation;
        private IReadOnlyDictionary<Weapon, SharpDX.Mathematics.Interop.RawRectangleF> weaponToImageTranslation;
        private SharpDX.Direct2D1.Bitmap _invItemSheet1;
        private SharpDX.Direct2D1.Bitmap _invItemSheet2;
        private int INV_SLOT_WIDTH;
        private int INV_SLOT_HEIGHT;
        private Options options;

        [STAThread]
        public int Startup(IPluginHostDelegates hostDelegates)
        {
            this.hostDelegates = hostDelegates;

            DEVMODE devMode = default;
            devMode.dmSize = (short)Marshal.SizeOf<DEVMODE>();
            PInvoke.EnumDisplaySettings(null, -1, ref devMode);

            // Create and initialize the overlay window.
            _window = new OverlayWindow(0, 0, devMode.dmPelsWidth, devMode.dmPelsHeight)
            {
                IsTopmost = true,
                IsVisible = true
            };
            _window?.Create();

            // Create and initialize the graphics object.
            _graphics = new Graphics()
            {
                MeasureFPS = false,
                PerPrimitiveAntiAliasing = false,
                TextAntiAliasing = true,
                UseMultiThreadedFactories = false,
                VSync = false,
                Width = _window.Width,
                Height = _window.Height,
                WindowHandle = _window.Handle
            };
            _graphics?.Setup();

            // Get a refernence to the underlying RenderTarget from SharpDX. This'll be used to draw portions of images.
            _device = (SharpDX.Direct2D1.WindowRenderTarget)typeof(Graphics).GetField("_device", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_graphics);

            _consolasBold = _graphics?.CreateFont("Consolas", 12, true);

            _black = _graphics?.CreateSolidBrush(0, 0, 0);
            _white = _graphics?.CreateSolidBrush(255, 255, 255);
            _grey = _graphics?.CreateSolidBrush(128, 128, 128);
            _darkred = _graphics?.CreateSolidBrush(139, 0, 0);
            _red = _graphics?.CreateSolidBrush(255, 0, 0);
            _lawngreen = _graphics?.CreateSolidBrush(124, 252, 0);
            _goldenrod = _graphics?.CreateSolidBrush(218, 165, 32);

            options = new Options();
            options.GetOptions();
            //INV_SLOT_WIDTH = (int)Math.Round(112d * scalingFactor, MidpointRounding.AwayFromZero); // Individual inventory slot width.
            //INV_SLOT_HEIGHT = (int)Math.Round(112d * scalingFactor, MidpointRounding.AwayFromZero); // Individual inventory slot height.
            INV_SLOT_WIDTH = 112;
            INV_SLOT_HEIGHT = 112;

            _invItemSheet1 = ImageLoader.LoadBitmap(_device, Properties.Resources.ui0100_iam_texout);
            _invItemSheet2 = ImageLoader.LoadBitmap(_device, Properties.Resources._40d_texout);

            GenerateClipping();

            return 0;
        }

        public int Shutdown()
        {
            weaponToImageTranslation = null;
            itemToImageTranslation = null;

            _invItemSheet2?.Dispose();
            _invItemSheet1?.Dispose();

            _goldenrod?.Dispose();
            _lawngreen?.Dispose();
            _red?.Dispose();
            _darkred?.Dispose();
            _grey?.Dispose();
            _white?.Dispose();
            _black?.Dispose();

            _consolasBold?.Dispose();

            _device = null; // We didn't create this object so we probably shouldn't be the one to dispose of it. Just set the variable to null so the reference isn't held.
            _graphics?.Dispose(); // This should technically be the one to dispose of the _device object since it was pulled from this instance.
            _graphics = null;
            _window?.Dispose();
            _window = null;

            return 0;
        }

        public int ReceiveData(object gameMemory)
        {
            try
            {
                this.gameMemory = (IGameMemoryRE2)gameMemory;
                _graphics?.BeginScene();
                _graphics?.ClearScene();
                if (options.ScalingFactor != 1f)
                    _device.Transform = new SharpDX.Mathematics.Interop.RawMatrix3x2(options.ScalingFactor, 0f, 0f, options.ScalingFactor, 0f, 0f);
                DrawOverlay();
                if (options.ScalingFactor != 1f)
                    _device.Transform = new SharpDX.Mathematics.Interop.RawMatrix3x2(1f, 0f, 0f, 1f, 0f, 0f);
            }
            finally
            {
                _graphics?.EndScene();
            }
            return 0;
        }

        private void DrawOverlay()
        {
            float baseXOffset = 5f;
            float baseYOffset = 30f;

            // Player HP
            float playerHPXOffset = baseXOffset + 80f;
            float playerHPYOffset = baseYOffset + 0f;
            if (gameMemory.PlayerCurrentHealth > 1200 || this.gameMemory.PlayerCurrentHealth <= 0)
                _graphics?.DrawText(_consolasBold, 36f, _red, playerHPXOffset, playerHPYOffset, "DEAD");
            else if (gameMemory.PlayerCurrentHealth >= 801) // Fine (Green)
                _graphics?.DrawText(_consolasBold, 36f, _lawngreen, playerHPXOffset, playerHPYOffset, string.Format("{0}", gameMemory.PlayerCurrentHealth));
            else if (gameMemory.PlayerCurrentHealth <= 800 && this.gameMemory.PlayerCurrentHealth >= 361) // Caution (Yellow)
                _graphics?.DrawText(_consolasBold, 36f, _goldenrod, playerHPXOffset, playerHPYOffset, string.Format("{0}", gameMemory.PlayerCurrentHealth));
            else if (gameMemory.PlayerCurrentHealth <= 360) // Danger (Red)
                _graphics?.DrawText(_consolasBold, 36f, _red, playerHPXOffset, playerHPYOffset, string.Format("{0}", gameMemory.PlayerCurrentHealth));

            // Stats
            float statsXOffset = baseXOffset + 5f;
            float statsYOffset = baseYOffset + 40f;
            _graphics?.DrawText(_consolasBold, 36f, _white, statsXOffset, statsYOffset, gameMemory.IGTFormattedString);
            _graphics?.DrawText(_consolasBold, 20f, _grey, statsXOffset, statsYOffset += 42, "Raw IGT");
            _graphics?.DrawText(_consolasBold, 20f, _grey, statsXOffset, statsYOffset += 24, string.Format("A:{0}", gameMemory.IGTRunningTimer.ToString("00000000000000000000")));
            _graphics?.DrawText(_consolasBold, 20f, _grey, statsXOffset, statsYOffset += 24, string.Format("C:{0}", gameMemory.IGTCutsceneTimer.ToString("00000000000000000000")));
            _graphics?.DrawText(_consolasBold, 20f, _grey, statsXOffset, statsYOffset += 24, string.Format("M:{0}", gameMemory.IGTMenuTimer.ToString("00000000000000000000")));
            _graphics?.DrawText(_consolasBold, 20f, _grey, statsXOffset, statsYOffset += 24, string.Format("P:{0}", gameMemory.IGTPausedTimer.ToString("00000000000000000000")));
            _graphics?.DrawText(_consolasBold, 20f, _grey, statsXOffset, statsYOffset += 24, string.Format("DA Rank: {0}", gameMemory.Rank.ToString()));
            _graphics?.DrawText(_consolasBold, 20f, _grey, statsXOffset, statsYOffset += 24, string.Format("DA Score: {0}", gameMemory.RankScore.ToString()));
            // Enemy HP
            _graphics?.DrawText(_consolasBold, 20f, _red, statsXOffset, statsYOffset += 34f, "Enemy HP");
            foreach (EnemyHP enemyHP in gameMemory.EnemyHealth.Where(a => a.IsAlive).OrderBy(a => a.IsTrigger).ThenBy(a => a.Percentage).ThenByDescending(a => a.CurrentHP))
                DrawProgressBar(ref statsXOffset, ref statsYOffset, enemyHP.CurrentHP, enemyHP.Percentage);

            // Inventory
            float invXOffset = baseXOffset + 265f;
            float invYOffset = baseYOffset + 0f;
            if (itemToImageTranslation != null && weaponToImageTranslation != null)
            {
                for (int i = 0; i < gameMemory.PlayerInventory.Length; ++i)
                {
                    // Only do logic for non-blank and non-broken items.
                    if (gameMemory.PlayerInventory[i] != default && gameMemory.PlayerInventory[i].SlotPosition >= 0 && gameMemory.PlayerInventory[i].SlotPosition <= 19 && !gameMemory.PlayerInventory[i].IsEmptySlot)
                    {
                        int slotColumn = gameMemory.PlayerInventory[i].SlotPosition % 4;
                        int slotRow = gameMemory.PlayerInventory[i].SlotPosition / 4;
                        float imageX = invXOffset + (slotColumn * INV_SLOT_WIDTH);
                        float imageY = invYOffset + (slotRow * INV_SLOT_HEIGHT);
                        //float textX = imageX + (INV_SLOT_WIDTH * options.ScalingFactor);
                        //float textY = imageY + (INV_SLOT_HEIGHT * options.ScalingFactor);
                        float textX = (imageX + INV_SLOT_WIDTH) * 0.96f;
                        float textY = (imageY + INV_SLOT_HEIGHT) * 0.92f;
                        SolidBrush textBrush = _white;
                        if (gameMemory.PlayerInventory[i].Quantity == 0)
                            textBrush = _darkred;

                        // Get the region of the inventory sheet where this item's icon resides.
                        SharpDX.Mathematics.Interop.RawRectangleF imageRegion;
                        Weapon weapon;
                        if (gameMemory.PlayerInventory[i].IsItem && itemToImageTranslation.ContainsKey(gameMemory.PlayerInventory[i].ItemID))
                            imageRegion = itemToImageTranslation[gameMemory.PlayerInventory[i].ItemID];
                        else if (gameMemory.PlayerInventory[i].IsWeapon && weaponToImageTranslation.ContainsKey(weapon = new Weapon() { WeaponID = gameMemory.PlayerInventory[i].WeaponID, Attachments = gameMemory.PlayerInventory[i].Attachments }))
                            imageRegion = weaponToImageTranslation[weapon];
                        else
                            imageRegion = new SharpDX.Mathematics.Interop.RawRectangleF(0, 0, INV_SLOT_WIDTH, INV_SLOT_HEIGHT);
                        imageRegion.Right += imageRegion.Left;
                        imageRegion.Bottom += imageRegion.Top;

                        // Get the region to draw our item icon to.
                        SharpDX.Mathematics.Interop.RawRectangleF drawRegion;
                        if (imageRegion.Right - imageRegion.Left == INV_SLOT_WIDTH * 2f)
                        {
                            // Double-slot item, adjust the draw region width and text's X coordinate.
                            textX += INV_SLOT_WIDTH;
                            drawRegion = new SharpDX.Mathematics.Interop.RawRectangleF(imageX, imageY, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT);
                        }
                        else // Normal-sized icon.
                            drawRegion = new SharpDX.Mathematics.Interop.RawRectangleF(imageX, imageY, INV_SLOT_WIDTH, INV_SLOT_HEIGHT);
                        drawRegion.Right += drawRegion.Left;
                        drawRegion.Bottom += drawRegion.Top;

                        // If we're one of the DLC items, use a different sheet.
                        if (gameMemory.PlayerInventory[i].ItemID == ItemEnumeration.OldKey)
                            _device?.DrawBitmap(_invItemSheet2, drawRegion, 1f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear, imageRegion);
                        else // Otherwise, use the main sheet.
                            _device?.DrawBitmap(_invItemSheet1, drawRegion, 1f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear, imageRegion);

                        // Draw the quantity text.
                        _graphics?.DrawText(_consolasBold, 22f, textBrush, textX, textY, (gameMemory.PlayerInventory[i].Quantity != -1) ? gameMemory.PlayerInventory[i].Quantity.ToString() : "∞");
                    }
                }
            }
        }

        private void DrawProgressBar(ref float xOffset, ref float yOffset, int health, float percentage = 1f)
        {
            _graphics.DrawRectangle(_grey, xOffset, yOffset += 28f, xOffset + 250f, yOffset + 22f, 4f);
            _graphics.FillRectangle(_darkred, xOffset + 1f, yOffset + 1f, xOffset + (247f * percentage), yOffset + 20f);
            _graphics.DrawText(_consolasBold, 20f, _red, xOffset + 6f, yOffset - 2f, string.Format("{0} {1:P1}", health, percentage));
        }

        public void GenerateClipping()
        {
            int itemColumnInc = -1;
            int itemRowInc = -1;
            itemToImageTranslation = new Dictionary<ItemEnumeration, SharpDX.Mathematics.Interop.RawRectangleF>()
            {
                { ItemEnumeration.None, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * 0, INV_SLOT_HEIGHT * 3, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },

                // Row 0.
                { ItemEnumeration.FirstAidSpray, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 0), INV_SLOT_HEIGHT * ++itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Herb_Green2, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Herb_Red2, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Herb_Blue2, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Herb_Mixed_GG, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Herb_Mixed_GR, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Herb_Mixed_GB, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Herb_Mixed_GGB, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Herb_Mixed_GGG, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Herb_Mixed_GRB, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Herb_Mixed_RB, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Herb_Green1, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Herb_Red1, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Herb_Blue1, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },

                // Row 1.
                { ItemEnumeration.HandgunBullets, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 0), INV_SLOT_HEIGHT * ++itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.ShotgunShells, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.SubmachineGunAmmo, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.MAGAmmo, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.HandgunLargeCaliberAmmo, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.SLS60HighPoweredRounds, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.GrenadeAcidRounds, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.GrenadeFlameRounds, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.NeedleCartridges, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Fuel, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.InkRibbon, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.WoodenBoard, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Gunpowder, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.GunpowderLarge, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.GunpowderHighGradeYellow, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.GunpowderHighGradeWhite, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.HipPouch, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },

                // Row 2.
                { ItemEnumeration.MatildaHighCapacityMagazine, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 0), INV_SLOT_HEIGHT * ++itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.MatildaMuzzleBrake, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.MatildaGunStock, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.SLS60SpeedLoader, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.JMBHp3LaserSight, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.SLS60ReinforcedFrame, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.JMBHp3HighCapacityMagazine, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.W870ShotgunStock, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.W870LongBarrel, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.MQ11HighCapacityMagazine, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.MQ11Suppressor, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.LightningHawkRedDotSight, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.LightningHawkLongBarrel, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.GM79ShoulderStock, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.FlamethrowerRegulator, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.SparkShotHighVoltageCondenser, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },

                //Row 3.
                { ItemEnumeration.Film_HidingPlace, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 9), INV_SLOT_HEIGHT * ++itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Film_RisingRookie, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Film_Commemorative, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Film_3FLocker, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Film_LionStatue, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.PortableSafe, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.TinStorageBox1, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.TinStorageBox2, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },

                // Row 4.
                { ItemEnumeration.Detonator, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 0), INV_SLOT_HEIGHT * ++itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.ElectronicGadget, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Battery9Volt, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.KeyStorageRoom, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 4), INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.JackHandle, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 6), INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.SquareCrank, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.MedallionUnicorn, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.KeySpade, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.KeyCardParkingGarage, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.KeyCardWeaponsLocker, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.ValveHandle, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 13), INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.STARSBadge, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Scepter, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.RedJewel, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.BejeweledBox, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },

                // Row 5.
                { ItemEnumeration.PlugBishop, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 1), INV_SLOT_HEIGHT * ++itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.PlugRook, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.PlugKing, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.PictureBlock, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.USBDongleKey, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.SpareKey, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.RedBook, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 8), INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.StatuesLeftArm, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.StatuesLeftArmWithRedBook, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.MedallionLion, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.KeyDiamond, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.KeyCar, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.MedallionMaiden, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 15), INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.PowerPanelPart1, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.PowerPanelPart2, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },

                // Row 6.
                { ItemEnumeration.LoversRelief, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 0), INV_SLOT_HEIGHT * ++itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.KeyOrphanage, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.KeyClub, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.KeyHeart, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.USSDigitalVideoCassette, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.TBarValveHandle, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.SignalModulator, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.KeySewers, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 8), INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.IDWristbandVisitor1, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.IDWristbandGeneralStaff1, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.IDWristbandSeniorStaff1, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.UpgradeChipGeneralStaff, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.UpgradeChipSeniorStaff, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.FuseMainHall, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 15), INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Scissors, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.BoltCutter, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },

                // Row 7.
                { ItemEnumeration.StuffedDoll, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 0), INV_SLOT_HEIGHT * ++itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.IDWristbandVisitor2, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 2), INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.IDWristbandGeneralStaff2, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.IDWristbandSeniorStaff2, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.LabDigitalVideoCassette, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.DispersalCartridgeEmpty, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.DispersalCartridgeSolution, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.DispersalCartridgeHerbicide, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.JointPlug, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 10), INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Trophy1, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 12), INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.Trophy2, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.GearSmall, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.GearLarge, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 14), INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { ItemEnumeration.PlugKnight, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 16), INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.PlugPawn, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },

                // Row 8.
                { ItemEnumeration.PlugQueen, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 0), INV_SLOT_HEIGHT * ++itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.BoxedElectronicPart1, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 2), INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.BoxedElectronicPart2, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.UpgradeChipAdministrator, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 5), INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.IDWristbandAdministrator, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.KeyCourtyard, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.FuseBreakRoom, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.JointPlug2, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.GearLarge2, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },

                // Row 9.
                //     No items.

                // Row 10.
                //     No items.

                // Row 11.
                //     No items.

                // Row 12.
                //     No items.

                // Row 13.
                //     No items.

                // Row 14.
                { ItemEnumeration.WoodenBox1, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (itemColumnInc = 9), INV_SLOT_HEIGHT * ++itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { ItemEnumeration.WoodenBox2, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++itemColumnInc, INV_SLOT_HEIGHT * itemRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },

                // Patch Items.
                { ItemEnumeration.OldKey, new SharpDX.Mathematics.Interop.RawRectangleF(0, 0, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
            };

            int weaponColumnInc = -1;
            int weaponRowInc = 8;
            weaponToImageTranslation = new Dictionary<Weapon, SharpDX.Mathematics.Interop.RawRectangleF>()
            {
                { new Weapon() { WeaponID = WeaponEnumeration.None, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * 0, INV_SLOT_HEIGHT * 3, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },

                // Row 1.
                //     No weapons.

                // Row 2.
                //     No weapons.

                // Row 3.
                //     No weapons.

                // Row 4.
                //     No weapons.

                // Row 5.
                //     No weapons.

                // Row 6.
                //     No weapons.

                // Row 7.
                //     No weapons.

                // Row 8.
                //     No weapons.

                // Row 9.
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_Matilda, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 0), INV_SLOT_HEIGHT * ++weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_Matilda, Attachments = AttachmentsFlag.First }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_Matilda, Attachments = AttachmentsFlag.Second }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 3), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_Matilda, Attachments = AttachmentsFlag.Third }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_Matilda, Attachments = AttachmentsFlag.First | AttachmentsFlag.Second }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_Matilda, Attachments = AttachmentsFlag.First | AttachmentsFlag.Third }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 7), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_Matilda, Attachments = AttachmentsFlag.Second | AttachmentsFlag.Third }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 9), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_Matilda, Attachments = AttachmentsFlag.First | AttachmentsFlag.Second | AttachmentsFlag.Third }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_JMB_Hp3, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 12), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_JMB_Hp3, Attachments = AttachmentsFlag.Second }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_JMB_Hp3, Attachments = AttachmentsFlag.Third }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_JMB_Hp3, Attachments = AttachmentsFlag.Second | AttachmentsFlag.Third }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_MUP, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_BroomHc, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },

                // Row 10.
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_SLS60, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 0), INV_SLOT_HEIGHT * ++weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_SLS60, Attachments = AttachmentsFlag.Third }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH , INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_M19, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH , INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_SLS60, Attachments = AttachmentsFlag.Second }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH , INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_SLS60, Attachments = AttachmentsFlag.Second | AttachmentsFlag.Third }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Shotgun_W870, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 6), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Shotgun_W870, Attachments = AttachmentsFlag.First }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Shotgun_W870, Attachments = AttachmentsFlag.Second }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 9), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Shotgun_W870, Attachments = AttachmentsFlag.First | AttachmentsFlag.Second }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH* 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.SMG_MQ11, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 12), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.SMG_MQ11, Attachments = AttachmentsFlag.First }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH* 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.SMG_MQ11, Attachments = AttachmentsFlag.Second }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 15), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.SMG_MQ11, Attachments = AttachmentsFlag.First | AttachmentsFlag.Second }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH* 2, INV_SLOT_HEIGHT) },

                // Row 11.
                { new Weapon() { WeaponID = WeaponEnumeration.SMG_LE5_Infinite2, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 0), INV_SLOT_HEIGHT * ++weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_LightningHawk, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_LightningHawk, Attachments = AttachmentsFlag.First }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_LightningHawk, Attachments = AttachmentsFlag.Second }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 4), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_LightningHawk, Attachments = AttachmentsFlag.First | AttachmentsFlag.Second }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.GrenadeLauncher_GM79, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 7), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.GrenadeLauncher_GM79, Attachments = AttachmentsFlag.First }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.ChemicalFlamethrower, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 10), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.ChemicalFlamethrower, Attachments = AttachmentsFlag.Second }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 12), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.SparkShot, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 14), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.SparkShot, Attachments = AttachmentsFlag.Second }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 16), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },

                // Row 12.
                { new Weapon() { WeaponID = WeaponEnumeration.ATM4, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 0), INV_SLOT_HEIGHT * ++weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.ATM4_Infinite, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 0), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.AntiTankRocketLauncher, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 2), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.AntiTankRocketLauncher_Infinite, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 2), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Minigun, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 4), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Minigun_Infinite, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 4), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.EMF_Visualizer, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 6), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_Quickdraw_Army, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.SMG_LE5_Infinite, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 9), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_SamuraiEdge_Infinite, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 11), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_SamuraiEdge_AlbertWesker, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 11), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_SamuraiEdge_ChrisRedfield, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 11), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Handgun_SamuraiEdge_JillValentine, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 11), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.ATM42, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 14), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.AntiTankRocketLauncher2, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 16), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },


                // Row 13.
                { new Weapon() { WeaponID = WeaponEnumeration.CombatKnife, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 0), INV_SLOT_HEIGHT * ++weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.CombatKnife_Infinite, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH , INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Minigun2, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 2), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH * 2, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.ChemicalFlamethrower2, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 4), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.SparkShot2, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH , INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.ATM43, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH , INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.AntiTankRocketLauncher3, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH , INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Minigun3, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH , INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.HandGrenade, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH , INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.FlashGrenade, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH , INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.ATM44, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 16), INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.AntiTankRocketLauncher4, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * ++weaponColumnInc, INV_SLOT_HEIGHT * weaponRowInc, INV_SLOT_WIDTH , INV_SLOT_HEIGHT) },

                // Row 14.
                { new Weapon() { WeaponID = WeaponEnumeration.Minigun4, Attachments = AttachmentsFlag.None }, new SharpDX.Mathematics.Interop.RawRectangleF(INV_SLOT_WIDTH * (weaponColumnInc = 4), INV_SLOT_HEIGHT * ++weaponRowInc, INV_SLOT_WIDTH, INV_SLOT_HEIGHT) },
            };
        }
    }
}

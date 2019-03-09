// <copyright file="ILI9340.cs">
//
// Copyright (c) A. Schiffler
// All rights reserved.
//
// MIT License
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software 
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED// AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//
// </copyright>
//
// <author>Andreas Schiffler, aschiffler at ferzkopp dot net</author>
//
// <summary>
// Windows IoT Core access class for the Adafruit PiTFT 2.2 HAT LCD display based on the ILI9340 interface.
// </summary>
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.Spi;

namespace PiTFT
{
    /// <summary>
    /// Windows IoT Core access class for the Adafruit PiTFT 2.2 HAT LCD display based on the ILI9340 interface.
    /// </summary>
    /// <remarks>
    /// References:
    /// https://www.adafruit.com/product/2315
    /// http://www.adafruit.com/datasheets/ILI9340.pdf
    /// https://developer.microsoft.com/en-us/windows/iot/samples/spidisplay
    /// https://github.com/notro/fbtft/blob/master/fb_ili9340.c
    /// </remarks>
    public sealed class ILI9340
    {
        // Raspberry Pi 2
        private const string SPI_CONTROLLER_NAME = "SPI0";
        private const int SPI_CHIP_SELECT_LINE = 0;

        // PITFT_2_2
        private const int PITFT22_DATA_COMMAND_PIN = 25;
        private const int PITFT22_RESET_PIN = 23;

        // Pixel buffer
        private const int PITFT22_TFTWIDTH = 240;
        private const int PITFT22_TFTHEIGHT = 320;
        private byte[] pixelBuffer;

        // Display IO
        private GpioController gpio;
        private GpioPin dcPin;
        private GpioPin rstPin;
        private SpiDevice spi;

        // Rotation setting
        private bool rotation = false;

        // Initialized state
        private bool initialized = false;

        // Various command buffers
        private byte[] commandBuffer;
        private byte[] columnAddressBuffer;
        private byte[] rowAddressBuffer;

        /// <summary>
        /// Create instance of the ILI9340 interface class.
        /// </summary>
        /// <remarks>
        /// Typical initialization sequence:
        /// ILI9340 tft = new ILI9340();
        /// tft.Rotation = true;
        /// tft.InitAll();
        /// </remarks>
        public ILI9340()
        {
            pixelBuffer = new byte[PITFT22_TFTWIDTH * PITFT22_TFTHEIGHT * 2];
            commandBuffer = new byte[1];
            columnAddressBuffer = new byte[] { 0, 0, (PITFT22_TFTWIDTH - 1) >> 8, (PITFT22_TFTWIDTH - 1) & 0xff };
            rowAddressBuffer = new byte[] { 0, 0, (PITFT22_TFTHEIGHT - 1) >> 8, (PITFT22_TFTHEIGHT - 1) & 0xff };
        }

        /// <summary>
        /// Initialize interfaces and display
        /// </summary>
        public async void InitAll()
        {
            InitGpio();                   //// Initialize the GPIO controller and GPIO pins
            await InitSpi();              //// Initialize the SPI controller
            await ResetDisplay();         //// Hardware reset of display
            await InitializeDisplay();    //// Initialize the display
        }

        /// <summary>
        /// Returns the width of the display for current rotation settings.
        /// </summary>
        public int Width
        {
            get
            {
                if (rotation)
                {
                    return PITFT22_TFTHEIGHT;
                }
                else
                {
                    return PITFT22_TFTWIDTH;
                }
            }
        }

        /// <summary>
        /// Returns the height of the display for current rotation settings.
        /// </summary>
        public int Height
        {
            get
            {
                if (rotation)
                {
                    return PITFT22_TFTWIDTH;
                }
                else
                {
                    return PITFT22_TFTHEIGHT;
                }
            }
        }

        /// <summary>
        /// Gets or sets the rotation setting of the display.
        /// False (default) is portrait, True is landscape.
        /// </summary>
        public bool Rotation
        {
            get
            {
                return rotation;
            }

            set
            {
                rotation = value;
            }
        }

        /// <summary>
        /// Gets a flag indicating if the display has been initialized.
        /// </summary>
        public bool Initialized
        {
            get
            {
                return initialized;
            }
        }

        /// <summary>
        /// Initialize GPIO interface
        /// </summary>
        private void InitGpio()
        {
            try
            {
                // Get default controller
                gpio = GpioController.GetDefault();

                // GPIO pin number for the D/C pin
                dcPin = gpio.OpenPin(PITFT22_DATA_COMMAND_PIN);
                dcPin.Write(GpioPinValue.High);
                dcPin.SetDriveMode(GpioPinDriveMode.Output);

                // GPIO pin number for the RST pin
                rstPin = gpio.OpenPin(PITFT22_RESET_PIN);
                rstPin.SetDriveMode(GpioPinDriveMode.Output);
            }
            catch (Exception ex)
            {
                throw new Exception("GPIO initialization failed", ex);
            }
        }

        /// <summary>
        /// Initialize SPI interface
        /// </summary>
        private async Task InitSpi()
        {
            try
            {
                var spiSettings = new SpiConnectionSettings(SPI_CHIP_SELECT_LINE);
                spiSettings.ClockFrequency = 32000000; //// 64000000 was not reliable
                spiSettings.Mode = SpiMode.Mode0;

                string spiDeviceSelector = SpiDevice.GetDeviceSelector(SPI_CONTROLLER_NAME);
                IReadOnlyList<DeviceInformation> devices = await DeviceInformation.FindAllAsync(spiDeviceSelector);
                spi = await SpiDevice.FromIdAsync(devices[0].Id, spiSettings);
            }
            catch (Exception ex)
            {
                throw new Exception("SPI initialization Failed", ex);
            }
        }

        /// <summary>
        /// Send data or command bytes.
        /// </summary>
        /// <param name="data">Data to send</param>
        /// <param name="isData">Flag indicating if bytes to send are data; command otherwise</param>
        private void Send(byte[] data, bool isData)
        {
            // Set DC low for command, high for data.
            dcPin.Write(isData ? GpioPinValue.High : GpioPinValue.Low);

            // Transfer data        
            spi?.Write(data);
        }

        /// <summary>
        /// Send a command byte
        /// </summary>
        /// <param name="data">Command byte to send</param>
        private void SendCommand(byte data)
        {
            commandBuffer[0] = data;
            Send(commandBuffer, false);
        }

        /// <summary>
        /// Send data bytes
        /// </summary>
        /// <param name="data">Data bytes to send</param>
        private void SendData(byte[] data)
        {
            Send(data, true);
        }

        /// <summary>
        /// Perform hardware reset of the TFT display.
        /// </summary>
        private async Task ResetDisplay()
        {
            rstPin.Write(GpioPinValue.High);
            await Task.Delay(5);
            rstPin.Write(GpioPinValue.Low);
            await Task.Delay(20);
            rstPin.Write(GpioPinValue.High);
            await Task.Delay(150);
        }

        /// <summary>
        /// Send SPI commands to power up and initialize the TFT display.
        /// </summary>
        private async Task InitializeDisplay()
        {
            // initial reset sequence
            SendCommand(0xEF);
            SendData(new byte[] { 0x03, 0x80, 0x02 });
            SendCommand(0xCF);
            SendData(new byte[] { 0x00, 0xC1, 0x30 });
            SendCommand(0xED);
            SendData(new byte[] { 0x64, 0x03, 0x12, 0x81 });
            SendCommand(0xE8);
            SendData(new byte[] { 0x85, 0x00, 0x78 });
            SendCommand(0xCB);
            SendData(new byte[] { 0x39, 0x2C, 0x00, 0x34, 0x02 });
            SendCommand(0xF7);
            SendData(new byte[] { 0x20 });
            SendCommand(0xEA);
            SendData(new byte[] { 0x00, 0x00 });

            // power control 1
            SendCommand(0xc0);
            SendData(new byte[] { 0x23 });

            // power control 2
            SendCommand(0xc1);
            SendData(new byte[] { 0x10 });

            // vcom control 1
            SendCommand(0xc5);
            SendData(new byte[] { 0x3e, 0x28 });

            // vcom control 2
            SendCommand(0xc7);
            SendData(new byte[] { 0x86 });

            // pixel format set: 16 bits/pixel
            SendCommand(0x3a);
            SendData(new byte[] { 0x55 });

            // frame rate control
            SendCommand(0xb1);
            SendData(new byte[] { 0x00, 0x18 });

            // display function control
            SendCommand(0xb6);
            SendData(new byte[] { 0x08, 0x82, 0x27 });

            // gamma function disable 
            SendCommand(0xF2);
            SendData(new byte[] { 0x00 });

            // gamma curve selected 
            SendCommand(0x26);
            SendData(new byte[] { 0x01 });

            // positive gamma correction
            SendCommand(0xe0);
            SendData(new byte[] { 0x0F, 0x31, 0x2B, 0x0C, 0x0E, 0x08, 0x4E, 0xF1, 0x37, 0x07, 0x10, 0x03, 0x0E, 0x09, 0x00 });

            // negative gamma correction
            SendCommand(0xe1);
            SendData(new byte[] { 0x00, 0x0E, 0x14, 0x03, 0x11, 0x07, 0x31, 0xC1, 0x48, 0x08, 0x0F, 0x0C, 0x31, 0x36, 0x0F });

            // memory access control
            SendCommand(0x36);
            SendData(new byte[] { 0x40 });

            // inversion off
            SendCommand(0x20);

            // idle mode off
            SendCommand(0x38);

            // sleep out
            SendCommand(0x11);
            await Task.Delay(120);

            // display on
            SendCommand(0x29);
            await Task.Delay(20);

            initialized = true;
        }

        /// <summary>
        /// Transfer the internal pixel buffer to the TFT display.
        /// </summary>
        public void Display()
        {
            try
            {
                if (initialized)
                {
                    // column address
                    SendCommand(0x2a);
                    SendData(columnAddressBuffer);

                    // row address
                    SendCommand(0x2b);
                    SendData(rowAddressBuffer);

                    // memory write
                    SendCommand(0x2c);
                    SendData(pixelBuffer);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Display failed", ex);
            }
        }

        /// <summary>
        /// Clear the internal pixel buffer to a color.
        /// </summary>
        /// <param name="c">Color to clear to.</param>
        /// <remarks>
        /// Typical call sequence:
        /// if (tft.Initialized) {
        ///  tft.Clear(0);
        ///  tft.Display();
        /// }
        /// </remarks>
        public void Clear(int c)
        {
            if (c == 0)
            {
                Array.Clear(pixelBuffer, 0, pixelBuffer.Length);
            }
            else
            {
                byte ch = (byte)(c & 0xFF);
                byte cl = (byte)((c >> 8) & 0xff);
                for (int i = 0; i < pixelBuffer.Length; i += 2)
                {
                    pixelBuffer[i] = cl;
                    pixelBuffer[i + 1] = ch;
                }
            }
        }

        /// <summary>
        /// Transfer a BGRA bitmap to the internal pixel buffer.
        /// </summary>
        /// <param name="pixelsBGRA8">Array of BGRA8 pixels.</param>
        /// <param name="sourceWidth">Width of the source bitmap in pixels.</param>
        /// <remarks>
        /// Typical call sequence:
        /// if (tft.Initialized) {
        ///  var renderBitmap = new RenderTargetBitmap();
        ///  await renderBitmap.RenderAsync(this.parentGrid, tft.Width, tft.Height);
        ///  IBuffer pixelBuffer = await renderBitmap.GetPixelsAsync();
        ///  byte[] pixelsBGRA8 = pixelBuffer.ToArray();
        ///  tft.SetBitmap(pixelsBGRA8, renderBitmap.PixelWidth);
        ///  tft.Display();
        /// }
        /// </remarks>
        public void SetBitmap(byte[] pixelsBGRA8, int sourceWidth)
        {
            // Scan the display in pixel order
            int target = 0;
            for (int y = 0; y < PITFT22_TFTHEIGHT; y++)
            {
                for (int x = 0; x < PITFT22_TFTWIDTH; x++)
                {
                    // Calculate source pixel position
                    int source;
                    if (rotation)
                    {
                        source = 4 * ((PITFT22_TFTHEIGHT - y) + sourceWidth * x);
                    }
                    else
                    {
                        source = 4 * (x + sourceWidth * y);
                    }

                    // Get RGB8 color of the source pixel
                    byte r = pixelsBGRA8[source];
                    source++;
                    byte g = pixelsBGRA8[source];
                    source++;
                    byte b = pixelsBGRA8[source];

                    // Set RGB565 color of the target pixel
                    int c = Color565(r, g, b);
                    byte ch = (byte)((c & 0xFF));
                    byte cl = (byte)(((c >> 8) & 0xff));
                    pixelBuffer[target] = cl;
                    target++;
                    pixelBuffer[target] = ch;
                    target++;
                }
            }            
        }

        /// <summary>
        /// Convert red, green, blue components to a 16-bit 565 RGB value. Components should be values 0 to 255.
        /// </summary>
        /// <param name="r">Red component</param>
        /// <param name="g">Green component</param>
        /// <param name="b">Blue component</param>
        /// <returns>A 16-bit color</returns>
        private int Color565(int r, int g, int b)
        {
            return (((r & 0xF8) << 8) | ((g & 0xFC) << 3) | ((b & 0xF8) >> 3));
        }
    }
}

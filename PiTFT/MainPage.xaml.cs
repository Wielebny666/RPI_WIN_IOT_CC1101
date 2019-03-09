// <author>Andreas Schiffler, aschiffler at ferzkopp dot net</author>
//
// <summary>
// Sample page to demonstrate continous TFT refresh of a XAML page.
// </summary>

namespace PiTFT
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading.Tasks;
    using Windows.Devices.Gpio;
    using Windows.Storage.Streams;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media.Imaging;

    /// <summary>
    ///  Sample page to demonstrate continous TFT refresh of a XAML page.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Instance of the PiTFT interface.
        /// </summary>
        private ILI9340 tft = null;
        private CC1101 rf = null;
        private GpioPin txPin = null;
        /// <summary>
        /// XAML to PiTFT display refresh rate.
        /// </summary>
        private TimeSpan refreshRate = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Main display.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();

            // Initialize CC1101
            rf = new CC1101();

            GpioController gpio = GpioController.GetDefault();
            txPin = gpio.OpenPin(CCRegister.CC1101_GDO0);
            var tmp = txPin.PinNumber;
            txPin.SetDriveMode(GpioPinDriveMode.Output);

            //rf.SetBaudRate(1);
            //rf.SetCarrierFrequency(433);
            //rf.SetDeviationFrequencySetting(1);
            rf.InitAll();

            //rf.SetTxState();
            //rf.SetIdleState();

            //rf.Reset();
            //rf.FlushRx();
            //rf.FlushTx();
            //rf.Info();

            //var part = rf.PartNumber;
            //var ver = rf.Version;

            //rf.SetupPATABLE();
            //rf.SetIdleState();


            //rf.SetupPATABLE();
            //rf.GetCarrierFrequency();
            // Initialize the display
            //tft = new ILI9340();
            //tft.Rotation = true;
            //tft.InitAll();

            //while (true)
            //{
            //    txPin.Write(GpioPinValue.High);
            //    rf.ShortWait(100);
            //    txPin.Write(GpioPinValue.Low);
            //    rf.ShortWait(100);
            //}

            // Create timer to refresh display
            DispatcherTimer dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Interval = refreshRate;
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            //dispatcherTimer.Start();
        }

        /// <summary>
        /// Render parent grid of page to TFT display
        /// </summary>
        private async void DispatcherTimer_Tick(object sender, object e)
        {
            if (rf != null)
            {
                
                rf.SendData(new byte[] { 0 });
                //txPin.Write(GpioPinValue.High);
                rf.ShortWait(100);
                //txPin.Write(GpioPinValue.Low);
                rf.SendData(new byte[] { 0, 0, 0, 0 });
                rf.ShortWait(100);
            }

            if (tft != null && tft.Initialized)
            {
                // Render parent to bitmap
                var renderBitmap = new RenderTargetBitmap();
                await renderBitmap.RenderAsync(parentGrid, tft.Width, tft.Height);

                // Get the pixels
                IBuffer pixelBuffer = await renderBitmap.GetPixelsAsync();
                byte[] pixelsBGRA8 = pixelBuffer.ToArray();

                // Transfer to display
                tft.Clear(0);
                tft.SetBitmap(pixelsBGRA8, renderBitmap.PixelWidth);
                tft.Display();
            }
        }
    }
}

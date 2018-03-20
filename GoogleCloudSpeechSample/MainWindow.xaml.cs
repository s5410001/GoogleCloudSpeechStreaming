using System;
using System.Windows;
using System.Threading;
using Google.Cloud.Speech.V1;
using Google.Apis.Auth.OAuth2;
using Google.Protobuf;
using Grpc.Auth;
using Grpc.Core;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Media;
using System.Drawing.Imaging;




namespace GoogleCloudSpeechSample
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {

        private CancellationTokenSource cancellationToken;

        private GoogleCredential credential = GoogleCredential.FromJson(File.ReadAllText("StreamingTest-a4691d19df64.json"));
        private Channel channel;
    
        public MainWindow()
        {
            InitializeComponent();

            StartRecButton.IsEnabled = true;
            EndRecButton.IsEnabled = false;

            credential = credential.CreateScoped("https://www.googleapis.com/auth/cloud-platform");
            channel = new Channel("speech.googleapis.com:443", credential.ToChannelCredentials());
        }
                
        private void StartRecButton_Click(object sender, RoutedEventArgs e)
        { 
            SpeechTextBlock.Text = "録音開始ボタンが押された";
            StartRecButton.IsEnabled = false;
            EndRecButton.IsEnabled = true;

            StreamingSpeechToText(e);
        }

        private void EndRecButton_Click(object sender, RoutedEventArgs e)
        {
            SpeechTextBlock.Text = "録音終了ボタンが押された";
            EndRecButton.IsEnabled = false;
            StartRecButton.IsEnabled = true;

            if(cancellationToken != null)
            {
                cancellationToken.Cancel();
            }
        }

        private async void StreamingSpeechToText(RoutedEventArgs e)
        {
            if(NAudio.Wave.WaveIn.DeviceCount < 1)
            {
                SpeechTextBlock.Text = "No Microphone!";
                return;
            }

            cancellationToken = new CancellationTokenSource();

            SpeechClient speech = SpeechClient.Create(channel);
            SpeechClient.StreamingRecognizeStream streamingCall = speech.StreamingRecognize();

            /*
             * 設定情報の初期リクエスト
             */
            await streamingCall.WriteAsync(new StreamingRecognizeRequest()
            {
                StreamingConfig = new StreamingRecognitionConfig()
                {
                    Config = new RecognitionConfig()
                    {
                        Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                        SampleRateHertz = 16000,
                        LanguageCode = "ja-JP",
                    },
                    InterimResults = true,
                }
            });

           
            Task printResponse = Task.Run(async () =>
            {
                while (await streamingCall.ResponseStream.MoveNext())
                {
                    foreach (var result in streamingCall.ResponseStream.Current.Results)
                    {
                        foreach (var alternative in result.Alternatives)
                        {
                            this.Dispatcher.Invoke((Action)(() =>
                            {
                                SpeechTextBlock.Text = alternative.Transcript;
                            }));
                        }
                    }
                }
            });

            object writeLock = new object();
            bool writeMore = true;
            NAudio.Wave.WaveInEvent waveIn = new NAudio.Wave.WaveInEvent();
            waveIn.DeviceNumber = 0;
            waveIn.WaveFormat = new NAudio.Wave.WaveFormat(16000, 1);
            waveIn.DataAvailable += (object sender, NAudio.Wave.WaveInEventArgs args) =>
            {
                lock (writeLock)
                {
                    if (!writeMore)
                    {
                        return;
                    }
                    streamingCall.WriteAsync(new StreamingRecognizeRequest()
                    {
                        AudioContent = Google.Protobuf.ByteString.CopyFrom(args.Buffer, 0, args.BytesRecorded)
                    }).Wait();
                }
            };

            waveIn.StartRecording();
            Console.WriteLine("Speak now");
            for (int i = 0; i < 50; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("Cancel");
                    break;
                }
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            Console.WriteLine("End Recognition");
            waveIn.StopRecording();
            lock (writeLock)
            {
                writeMore = false;
            }
            await streamingCall.WriteCompleteAsync();
            await printResponse;
            return;
            
        }

        private void RadioSample1_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void RadioSample2_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}

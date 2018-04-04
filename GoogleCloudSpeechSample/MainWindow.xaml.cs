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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using NAudio.Wave;

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

        static HttpMethod apiMethod = HttpMethod.Post;
        static string apiEndPointURL = "https://api.apigw.smt.docomo.ne.jp/aiTalk/v1/textToSpeech?APIKEY=";
        static string apiKey = "xxx";

        static string apiContentType = "application/ssml+xml";
        static string apiAccept = "audio/L16";

        private int selectMicrophoneNubmer;

        public MainWindow()
        {
            InitializeComponent();
            List<string> deviceList = new List<string>();
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var deviceInfo = WaveIn.GetCapabilities(i);
                Console.WriteLine("device number = " + i + " : " + "product name = " + deviceInfo.ProductName);
                MicrophoneSelectList.Items.Add(deviceInfo.ProductName);
            }
            MicrophoneSelectList.SelectedIndex = 0;
            selectMicrophoneNubmer = 0;

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
            waveIn.DeviceNumber = selectMicrophoneNubmer;
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

        private void RequestTextToSpeech()
        {
            string textBySSML = @"<?xml version=""1.0"" encoding=""utf-8""?>
<speak version=""1.1"">
    <voice name=""koutarou"">
        <prosody pitch=""1.2"" volume=""1.1"">"
            + textBox.Text +
        @"</prosody>
    </voice>
</speak>";

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            byte[] bytesBySSML = Encoding.UTF8.GetBytes(textBySSML);

            string lpcmFilePath = $@"C:\VoiceData\AITalkR{DateTime.Now.ToString("yyyyMMddHHmmss")}.lpcm";
            var sbResponseHeader = new StringBuilder();
            var isSuccessResponse = false;

            using (HttpClient client = new HttpClient())
            {
                var request = new HttpRequestMessage(apiMethod, apiEndPointURL + apiKey);
                request.Content = new ByteArrayContent(bytesBySSML);

                request.Content.Headers.ContentType = new MediaTypeHeaderValue(apiContentType);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(apiAccept));

                try
                {
                    var asyncSendTask = client.SendAsync(request);
                    asyncSendTask.Wait();

                    sbResponseHeader.AppendLine($"HTTP-Status-Code:{asyncSendTask.Result.StatusCode}");
                    foreach(var headerItem in asyncSendTask.Result.Headers)
                    {
                        var headkey = headerItem.Key;
                        var headValue = String.Join(",", headerItem.Value);
                        sbResponseHeader.AppendLine($"{headkey}:{headValue}");
                    }
                    if(asyncSendTask.Result.StatusCode == HttpStatusCode.OK)
                    {
                        isSuccessResponse = true;
                        var msgResponseBody = asyncSendTask.Result.Content;
                        var stmResponseBody = msgResponseBody.ReadAsStreamAsync().GetAwaiter().GetResult();
                        using(BinaryReader reader = new BinaryReader(stmResponseBody))
                        {
                            Byte[] lnByte = reader.ReadBytes(1 * 1024 * 1024 * 10);
                            using (FileStream lxFS = new FileStream(lpcmFilePath, FileMode.Create))
                            {
                                lxFS.Write(lnByte, 0, lnByte.Length);
                            }
                        }
                    }
                }catch(Exception ex)
                {
                    Console.WriteLine("エラー発生\n" + ex.Message);
                }
                finally
                {
                    if (isSuccessResponse)
                    {
                        Console.WriteLine($"【レスポンスヘッダ】\n{sbResponseHeader.ToString()}\n" + $"【レスポンスボディ】\n{lpcmFilePath}\n");
                    }
                    else
                    {
                        Console.WriteLine($"【レスポンスヘッダ】\n{sbResponseHeader.ToString()}");
                    }
                }
                Console.Read();
            }
        }


        private void SpeechStartButton_Click(object sender, RoutedEventArgs e)
        {
            RequestTextToSpeech();
        }

        private void MicrophoneSelectList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SpeechTextBlock.Text = "使用するマイクは" + MicrophoneSelectList.SelectedIndex + "番";
            selectMicrophoneNubmer = MicrophoneSelectList.SelectedIndex;
        }
    }
}


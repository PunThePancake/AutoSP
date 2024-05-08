using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using SoundpadConnector;

class Program
{
    static async Task Main(string[] args)
    {
        // Connect to Soundpad
        Soundpad soundpad = new Soundpad();
        try
        {
            soundpad.ConnectAsync().Wait(); // Wait for connection
            Console.WriteLine("Connected to Soundpad.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to Soundpad: {ex.Message}");
            return;
        }

        // Set up the HTTP listener
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:4999/");
        listener.Start();
        Console.WriteLine("Listening for requests on http://localhost:4999/");

        // Handle incoming requests
        while (true)
        {
            HttpListenerContext context = listener.GetContext();
            HttpListenerRequest request = context.Request;

            // Check if the request is a POST
            if (request.HttpMethod == "POST")
            {
                // Read the request body
                string requestBody;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    requestBody = reader.ReadToEnd();
                }

                // Parse the JSON data
                dynamic requestData = JsonConvert.DeserializeObject(requestBody);

                // Extract the URL and video ID
                string videoUrl = requestData.url;
                string videoId = videoUrl.Substring(videoUrl.IndexOf("v=") + 2);

                // Log the video details
                Console.WriteLine($"Video URL: {videoUrl}");
                Console.WriteLine($"Video ID: {videoId}");

                // Send a response (optional)
                HttpListenerResponse response = context.Response;
                string responseString = "Video details logged successfully";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.Close();

                Console.WriteLine("Attempting download...");

                try
                {
                    var youtube = new YoutubeClient();

                    // Get video metadata to retrieve title
                    var video = await youtube.Videos.GetAsync(videoId);
                    string videoTitle = video.Title;

                    // Remove any characters not allowed in Windows file names
                    char[] invalidChars = Path.GetInvalidFileNameChars();
                    videoTitle = new string(videoTitle.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());

                    // Remove any characters other than letters and numbers
                    videoTitle = new string(videoTitle.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());


                    // Get available streams and choose the best muxed (audio + video) stream
                    var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoId);
                    var streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
                    if (streamInfo is null)
                    {
                        Console.Error.WriteLine("This video has no muxed streams.");
                        return;
                    }

                    // Download the stream to the subdirectory
                    var subDirectory = "soundpadFiles";
                    Directory.CreateDirectory(subDirectory); // Create the subdirectory if it doesn't exist
                    var fileName = Path.Combine(subDirectory, $"{videoTitle}.{streamInfo.Container.Name}");

                    Console.WriteLine($"Downloading {videoTitle}...");

                    await youtube.Videos.Streams.DownloadAsync(streamInfo, fileName);

                    Console.WriteLine("Video downloaded successfully!");
                    Console.WriteLine($"Video saved as: '{fileName}'");

                    // Add the downloaded file to Soundpad
                    AddToSoundpad(soundpad, fileName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }

                Console.WriteLine("-----------------------");
            }
        }
    }

    static void AddToSoundpad(Soundpad soundpad, string filePath)
    {
        try
        {
            // Get the full directory path of the file
            string fullPath = Path.GetFullPath(filePath);

            // Add the file to Soundpad
            soundpad.AddSound(fullPath);
            Console.WriteLine(filePath + " added to Soundpad successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while adding the file to Soundpad: {ex.Message}");
        }
    }
}

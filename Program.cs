using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PushSampleDataToSentinelLogs
{
    class ApiExample
    { 
        // Update customerId to your Log Analytics workspace ID
        static string customerId = "";

        // For sharedKey, use either the primary or the secondary Connected Sources client authentication key   
        static string sharedKey = "";

        // LogName is name of the event type that is being submitted to Azure Monitor
        static string LogName = "ForcepointDLP_sample";

        // You can use an optional field to specify the timestamp from the data. If the time field is not specified, Azure Monitor assumes the time is the message ingestion time
        static string TimeStampField = "";

        static void Main()
        {
            var githubClient = new Github();
            var task = githubClient.getRepo("Azure", "Azure-Sentinel");
            task.Wait();
            var dir = task.Result;

            Console.WriteLine("Repo: " + dir.name);
            WriteRepoToConsole(dir);
            Console.WriteLine("Are you sure to add (Yes(Y)/No(N))");
            var test = Console.ReadLine();


            //if (test.ToUpper()=="Y" || test.ToUpper() == "YES")
            //{
            //    // do whhe
            //}

            // Create a hash for the API signature
            var datestring = DateTime.UtcNow.ToString("r");
            string json = ReadFile();
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
            string hashedString = BuildSignature(stringToHash, sharedKey);
            string signature = "SharedKey " + customerId + ":" + hashedString;

            PostData(signature, datestring, json);
        }

        private static void WriteRepoToConsole(Directory directory)
        {
            foreach (var file in directory.files)
            {
                if (file.hasFileBeenModified)
                {
                    Console.WriteLine("  **" + file.name);
                }
                else if (file.isNewFile)
                {
                    Console.WriteLine("  ++" + file.name);
                }
                else
                {
                    Console.WriteLine("  " + file.name);
                }
            }

            foreach (var file in directory.subDirs)
            {
                Console.WriteLine("Sub Dir: " + file.name);
                WriteRepoToConsole(file);
            }
        }

        // Build the API signature
        public static string BuildSignature(string message, string secret)
        {
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = Convert.FromBase64String(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hash = hmacsha256.ComputeHash(messageBytes);
                return Convert.ToBase64String(hash);
            }
        }

        public static string ReadFile()
        {
            // An example JSON object, with key/value pairs
            //string json = @"[{""TenantId"":""1063531e-68b1-4ff2-9546-d8d2d8a584c4"",""SourceSystem"":""OpsManager""},{""TimeGenerated"":""2020-02-06T09:26:24.94Z"",""ReceiptTime
            //                "":""1580980000000""}]";
            try
            {
                var filePath = Environment.CurrentDirectory + @"\SampleData\CustomLog\";
                using (StreamReader streamReader = new StreamReader(filePath))
                {
                    var json = streamReader.ReadToEnd();
                    return json;
                }
            }
            catch (Exception excep)
            {
                Console.WriteLine("Read File exception: " + excep.Message);
                return "Read File error";
            }

        }

        // Send a request to the POST API endpoint
        public static void PostData(string signature, string date, string json)
        {
            try
            {
                string url = "https://" + customerId + ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";

                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Log-Type", LogName);
                client.DefaultRequestHeaders.Add("Authorization", signature);
                client.DefaultRequestHeaders.Add("x-ms-date", date);
                client.DefaultRequestHeaders.Add("time-generated-field", TimeStampField);

                HttpContent httpContent = new StringContent(json, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Task<HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);

                HttpContent responseContent = response.Result.Content;
                string result = responseContent.ReadAsStringAsync().Result;
                Console.WriteLine("Data successfully posted: " + result);
            }
            catch (Exception excep)
            {
                Console.WriteLine("API Post Exception: " + excep.Message);
            }
        }
    }
}
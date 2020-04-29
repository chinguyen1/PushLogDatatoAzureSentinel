using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;

namespace PushSampleDataToSentinelLogs
{
    public class Github
    {
        static readonly string access_token = "<enter personal Github access token here>";
        public Github()
        {
            
        }

        //Get all files from a repo
        public async Task<Directory> getRepo(string owner, string name)
        {
            HttpClient client = new HttpClient();
            Directory root = await readDirectory("root", client, String.Format("https://api.github.com/repos/{0}/{1}/contents/", owner, name));
            client.Dispose();
            return root;
        }

        private async Task<String> GetDirectory(String name, HttpClient client, string uri)
        {
            //get the directory contents
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("Authorization",
                "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", access_token, "x-oauth-basic"))));
            request.Headers.Add("User-Agent", "lk-github-client");

            //parse result
            HttpResponseMessage response = await client.SendAsync(request);
            String jsonStr = await response.Content.ReadAsStringAsync(); ;
            response.Dispose();
            return jsonStr;
            
        }

        private async Task<Directory> readDirectory(String name, HttpClient client, string uri)
        {
            var jsonStr = await GetDirectory(name, client, uri);

            //read in data
            FileInfo[] dirContents = JsonConvert.DeserializeObject<FileInfo[]>(jsonStr);

            var rootDirectoryName = "Sample Data";
            Directory result;
            result.name = rootDirectoryName;
            result.subDirs = new List<Directory>();
            result.files = new List<FileData>();
            foreach (FileInfo dir in dirContents)
            {
                if(dir.name == rootDirectoryName)
                {
                    if (dir.type == "dir")
                    {
                        var currentDirectory = System.IO.Directory.GetCurrentDirectory();
                        var basePath = currentDirectory.Split(new string[] { "\\bin" }, StringSplitOptions.None)[0];
                        var dirPath = basePath + "\\" + rootDirectoryName;

                        if (!System.IO.Directory.Exists(dirPath))
                        {
                            System.IO.Directory.CreateDirectory(dirPath);
                        }

                        await ProcessFiles(dir.name, client, dir._links.self, dirPath, result);
                    }
                }
            }
            return result;
        }

        private async Task<Directory> ProcessFiles(String directoryName, HttpClient client, string uri, string dirPath, Directory directory)
        {
            var jsonStr = await GetDirectory(directoryName, client, uri);
            FileInfo[] dirContents = JsonConvert.DeserializeObject<FileInfo[]>(jsonStr);

            if (!System.IO.Directory.Exists(dirPath))
            {
                System.IO.Directory.CreateDirectory(dirPath);
            }

            foreach (FileInfo file in dirContents)
            {
                var filePath = dirPath + "\\" + file.name;

                if (file.type == "dir")
                {
                    Directory subDir;
                    subDir.name = file.name;
                    subDir.subDirs = new List<Directory>();
                    subDir.files = new List<FileData>();

                    await ProcessFiles(file.name, client, file._links.self, filePath, subDir);
                    directory.subDirs.Add(subDir);
                }
                else
                {
                    FileData data;
                    data.name = file.name;
                    var content = await GetDirectory(file.name, client, file._links.self);
                    data.hasFileBeenModified = false;
                    data.isNewFile = false;

                    if (file.name.Contains(".csv"))
                    {
                        var fileName = file.name.Replace(".csv", ".json");
                        filePath = dirPath + "\\" + fileName;
                        data.name = fileName;
                        data.contents = JsonConvert.SerializeObject(content);
                    }
                    else
                    {
                        data.contents = content;
                    }

                    var response = AddFiles(filePath, data);
                    //data.hasFileBeenModified = response.;
                    directory.files.Add(response);
                }
            }

            return directory;
        }

        private FileData AddFiles(String fileName, FileData file)
        {
            if (File.Exists(fileName))
            {
                file.hasFileBeenModified = CheckIfFileModifiedSinceLastDownload(fileName, file);
                //if (fileName.Contains(".csv"))
                //{
                //    //var json = JsonConvert.SerializeObject(file.contents);
                //    //fileName = fileName.Replace(".csv", ".json");
                    File.WriteAllText(fileName, file.contents);
                //}
            }
            else
            {
                file.isNewFile = true;
                //var json = JsonConvert.SerializeObject(file.contents);
                //fileName = fileName.Replace(".csv", ".json");
                File.WriteAllLines(fileName, new string[] { file.contents });
            }

            return file;
        }

        private bool CheckIfFileModifiedSinceLastDownload(String filePath, FileData file)
        {
            var existingFileContent = File.ReadAllText(filePath).Trim();
            if (existingFileContent != file.contents)
            {
                return true;
            }

            return false;
        }
    }



    //JSON parsing methods
    struct LinkFields
    {
        public String self;
    }
    struct FileInfo
    {
        public String name;
        public String type;
        public String download_url;
        public LinkFields _links;
    }

    //Structs used to hold file data
    public struct FileData
    {
        public string name;
        public String contents;
        public Boolean hasFileBeenModified;
        public Boolean isNewFile;
    }
    public struct Directory
    {
        public String name;
        public List<Directory> subDirs;
        public List<FileData> files;
    }
}

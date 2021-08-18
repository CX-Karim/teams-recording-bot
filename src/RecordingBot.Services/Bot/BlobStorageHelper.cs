using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecordingBot.Services.Bot
{
    class BlobStorageHelper
    {
        private BlobServiceClient client;
        private BlobContainerClient containerClient;

        public BlobStorageHelper(string connectionString, string containerName)
        {
            this.client = new BlobServiceClient(connectionString);
            this.containerClient = client.GetBlobContainerClient(containerName);
        }

        public void CreateContainer(string connectionString, string containerName)
        {
            BlobServiceClient client = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = client.CreateBlobContainer(containerName);
        }

        public async Task<Azure.Response<BlobContentInfo>> AddFileAsync(string blobname, Stream stream)
        {
            BlobClient blobClient = containerClient.GetBlobClient(blobname);
            var response = await blobClient.UploadAsync(stream, true);
            stream.Close();

            return response;
        }
        public async Task AddFolderAsync(string blobname, string path)
        {
            foreach (string dirFile in Directory.GetFiles(path))
            {
                string[] temp = dirFile.Split('\\');
                Console.WriteLine(temp.Last());
                this.AddFileTask($"{blobname}/{temp.Last()}", dirFile).Wait();
            }
        }

        public async Task<Azure.Response<BlobContentInfo>> AddFileAsync(string blobname, string path)
        {
            BlobClient blobClient = containerClient.GetBlobClient(blobname);
            var response = await blobClient.UploadAsync(path);

            return response;
        }

        public async Task<Azure.Response<BlobContentInfo>> AddFileTask(string blobname, string path)
        {
            BlobClient blobClient = containerClient.GetBlobClient(blobname);
            var response = await blobClient.UploadAsync(path);

            return response;
        }


        public async Task AddFileTask1(string blobname, string path)
        {

        }

        public async Task AddFileTask2(string blobname, string path)
        {
            throw new NotImplementedException();
        }

        public async Task<Stream> GetFileAsync(string blobname)
        {
            var memoryStream = new MemoryStream();
            BlobClient blobClient = containerClient.GetBlobClient(blobname);
            var download = await blobClient.DownloadToAsync(memoryStream);

            return memoryStream;
        }
    }
}

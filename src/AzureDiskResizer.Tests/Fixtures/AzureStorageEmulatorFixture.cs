using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using System.Diagnostics;

namespace AzureDiskResizer.Tests.Fixtures
{
    public class AzureStorageEmulatorFixture : IDisposable
    {
        public static Uri ServiceUri => new("http://127.0.0.1:10000/devstoreaccount1");
        public static string AccountName => "devstoreaccount1";
        public static string AccountKey => "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

        private readonly List<Action> _cleanupActions = new();
        private Process _process = null!;

        public AzureStorageEmulatorFixture()
        {
            StartAzurite();
        }

        public void Dispose()
        {
            RunCleanup();

            _process?.Kill();

            GC.SuppressFinalize(this);
        }

        public void DeleteVhdFile(string vhdFilePath)
        {
            _cleanupActions.Add(() =>
            {
                if (File.Exists(vhdFilePath))
                {
                    File.Delete(vhdFilePath);
                }
            });
        }

        public void DeleteVhdFileInContainer(string containerName, Uri vhdFileUri)
        {
            _cleanupActions.Add(() => DeleteVhdFileAndContainer(containerName, vhdFileUri));
        }

        /// <summary>
        /// Uploads the VHD file specified as a page blob, using the parameters specified.
        /// </summary>
        /// <param name="containerName">The container in the Azure Storage account where the VHD file will be uploaded to.</param>
        /// <param name="filePath">The path of the VHD file.</param>
        /// <returns></returns>
        public static async Task<Uri> UploadVhdFileToContainerAsync(string containerName, string filePath)
        {
            var serviceClient = GetBlobServiceClient();
            var containerClient = serviceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();

            var fileName = Path.GetFileName(filePath);
            var blobClient = containerClient.GetPageBlobClient(fileName);

            using FileStream source = new(filePath, FileMode.Open, FileAccess.Read);

            await blobClient.CreateIfNotExistsAsync(source.Length);
            await blobClient.UploadPagesAsync(source, 0);
            return blobClient.Uri;
        }

        /// <summary>
        /// Returns the size of the VHD file specified, in bytes.
        /// </summary>
        /// <param name="vhdFileUri">The location of the VHD file in the storage account, as a <see cref="Uri"/> object.</param>
        /// <returns></returns>
        public static async Task<long> GetVhdSizeInContainerAsync(Uri vhdFileUri)
        {
            var credential = new StorageSharedKeyCredential(AccountName, AccountKey);
            var blobClient = new PageBlobClient(vhdFileUri, credential);

            if (await blobClient.ExistsAsync())
            {
                return (await blobClient.GetPropertiesAsync()).Value.ContentLength;
            }
            else
                return 0L;
        }

        /// <summary>
        /// Deletes the VHD file specified along with its container from the storage account.
        /// </summary>
        /// <param name="containerName">The container in the Azure Storage account where the VHD file is located.</param>
        /// <param name="vhdFileUri">The location of the VHD file in the storage account, as a <see cref="Uri"/> object.</param>
        private static void DeleteVhdFileAndContainer(string containerName, Uri vhdFileUri)
        {
            var serviceClient = GetBlobServiceClient();
            var containerClient = serviceClient.GetBlobContainerClient(containerName);
            var credential = new StorageSharedKeyCredential(AccountName, AccountKey);
            var blobClient = new PageBlobClient(vhdFileUri, credential);

            blobClient.DeleteIfExists();
            containerClient.DeleteIfExists();
        }

        /// <summary>
        /// Returns the <see cref="BlobServiceClient"/> object based on the parameters specified.
        /// </summary>
        /// <returns></returns>
        private static BlobServiceClient GetBlobServiceClient()
        {
            var credential = new StorageSharedKeyCredential(AccountName, AccountKey);
            return new BlobServiceClient(ServiceUri, credential);
        }

        private void StartAzurite()
        {
            var processes = Process.GetProcessesByName("Azurite");
            if (processes.Length != 0)
            {
                _process = processes[0];
                return;
            }

            var arguments = @"-l D:\Azurite -d D:\Azurite\debug.log";
            var azuritePath = @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\Azurite.exe";

            if (!File.Exists(azuritePath))
            {
                azuritePath = "azurite.exe";
                arguments = @"-l C:\Azurite -d C:\Azurite\debug.log";
            }

            _process = new Process
            {
                StartInfo = {
                    UseShellExecute = false,
                    FileName = azuritePath,
                    Arguments = arguments,
                }
            };
            _process.Start();
        }

        private void RunCleanup()
        {
            foreach (var item in _cleanupActions)
            {
                item();
            }
        }
    }
}

using AzureDiskResizer.Helpers;
using AzureDiskResizer.Tests.Fixtures;
using AzureDiskResizer.Tests.Helpers;
using ByteSizeLib;

namespace AzureDiskResizer.Tests.Tests;

/// <summary>
/// This unit test class uses Azure Storage Emulator for running unit tests. There is more documentation
/// about the emulator at: https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator
/// </summary>
public class ResizeVhdHelperTests : IClassFixture<AzureStorageEmulatorFixture>
{
    private const string TestDiskUri = "http://127.0.0.1:10000/devstoreaccount1/test-container/TestDisk0.vhd";
    private readonly AzureStorageEmulatorFixture _azureStorageEmulatorFixture;
    private readonly string _accountName;
    private readonly string _accountKey;

    public ResizeVhdHelperTests(AzureStorageEmulatorFixture azureStorageEmulatorFixture)
    {
        _azureStorageEmulatorFixture = azureStorageEmulatorFixture;
        _accountName = AzureStorageEmulatorFixture.AccountName;
        _accountKey = AzureStorageEmulatorFixture.AccountKey;
    }

    [Fact]
    public async Task Resize_Vhd_Blob_Not_Exists()
    {
        const int newSizeInGb = 1;
        var blobUri = new Uri(TestDiskUri);
        var resizeVhdHelper = new ResizeVhdHelper();
        var result = await resizeVhdHelper.ResizeVhdBlobAsync(newSizeInGb, blobUri, _accountName, _accountKey);

        Assert.Equal(ResizeResult.Error, result);
    }

    [Fact]
    public async Task Resize_Vhd_Blob_Not_Exists_And_Bytes_Are_The_Same()
    {
        const int newSizeInGb = 1;
        var blobUri = new Uri(TestDiskUri);
        var resizeVhdHelper = new ResizeVhdHelper();
        var result = await resizeVhdHelper.ResizeVhdBlobAsync(newSizeInGb, blobUri, _accountName, _accountKey);

        Assert.Equal(ResizeResult.Error, result);
        Assert.Equal(ByteSize.FromGigaBytes(newSizeInGb).Bytes, resizeVhdHelper.NewSize.Bytes);
    }

    [Fact]
    public async Task GetVhdSizeInContainer_Fail()
    {
        var blobUri = new Uri(TestDiskUri);
        var result = await AzureStorageEmulatorFixture.GetVhdSizeInContainerAsync(blobUri);
        Assert.Equal(0L, result);
    }

    [Fact]
    public async Task Resize_Vhd_Blob_Empty_Account_Details()
    {
        const int newSizeInGb = 1;
        var blobUri = new Uri(TestDiskUri);
        var resizeVhdHelper = new ResizeVhdHelper();
        var result = await resizeVhdHelper.ResizeVhdBlobAsync(newSizeInGb, blobUri, null, null);
        Assert.Equal(ResizeResult.Error, result);
    }

    [Fact]
    public async Task Resize_Vhd_Blob_Dynamic_Disk()
    {
        const int newSizeInGb = 1;
        const string vhdFilePath = "TestDisk-Dynamic.vhd";
        const string containerName = "test-container";

        // First create the dynamic VHD file
        VhdHelper.CreateVhdDisk(true, newSizeInGb, vhdFilePath, "Testing Disk");
        var vhdBlobUri = await AzureStorageEmulatorFixture.UploadVhdFileToContainerAsync(containerName, vhdFilePath);

        // Then resize the VHD file
        var resizeVhdHelper = new ResizeVhdHelper();
        var result = await resizeVhdHelper.ResizeVhdBlobAsync(newSizeInGb, vhdBlobUri, _accountName, _accountKey);
        var length = await AzureStorageEmulatorFixture.GetVhdSizeInContainerAsync(vhdBlobUri);

        // Clean the files in the container and the local file system
        _azureStorageEmulatorFixture.DeleteVhdFileInContainer(containerName, vhdBlobUri);
        _azureStorageEmulatorFixture.DeleteVhdFile(vhdFilePath);

        Assert.Equal(ResizeResult.Error, result);
        Assert.NotEqual(ByteSize.FromGigaBytes(newSizeInGb), ByteSize.FromBytes(length));
    }

    [Fact]
    public async Task Resize_Vhd_Blob_Shrink()
    {
        const int firstSizeInGb = 2;
        const int newSizeInGb = 1;
        const string vhdFilePath = "TestDisk-Shrink.vhd";
        const string containerName = "test-container";

        // First create the fixed VHD file
        VhdHelper.CreateVhdDisk(false, firstSizeInGb, vhdFilePath, "Testing Shrink Disk");
        var vhdBlobUri = await AzureStorageEmulatorFixture.UploadVhdFileToContainerAsync(containerName, vhdFilePath);

        // Then resize the VHD file
        var resizeVhdHelper = new ResizeVhdHelper();
        var firstResult = await resizeVhdHelper.ResizeVhdBlobAsync(newSizeInGb, vhdBlobUri, _accountName, _accountKey);
        var finalResult = ResizeResult.Error;
        if (firstResult == ResizeResult.Shrink)
        {
            resizeVhdHelper.IsExpand = false;
            finalResult = await resizeVhdHelper.DoResizeVhdBlobAsync();
        }
        var length = await AzureStorageEmulatorFixture.GetVhdSizeInContainerAsync(vhdBlobUri);

        // Clean the files in the container and the local file system
        _azureStorageEmulatorFixture.DeleteVhdFileInContainer(containerName, vhdBlobUri);
        _azureStorageEmulatorFixture.DeleteVhdFile(vhdFilePath);

        Assert.Equal(ResizeResult.Shrink, firstResult);
        Assert.Equal(ResizeResult.Success, finalResult);
        Assert.Equal(newSizeInGb, (int)ByteSize.FromBytes(length).GigaBytes);
    }

    [Fact]
    public async Task Resize_Vhd_Blob_Expand()
    {
        const int firstSizeInGb = 1;
        const int newSizeInGb = 2;
        const string vhdFilePath = "TestDisk-Expand.vhd";
        const string containerName = "test-container";

        // First create the fixed VHD file
        VhdHelper.CreateVhdDisk(false, firstSizeInGb, vhdFilePath, "Testing Expand Disk");
        var vhdBlobUri = await AzureStorageEmulatorFixture.UploadVhdFileToContainerAsync(containerName, vhdFilePath);

        // Then resize the VHD file
        var resizeVhdHelper = new ResizeVhdHelper();
        var result = await resizeVhdHelper.ResizeVhdBlobAsync(newSizeInGb, vhdBlobUri, _accountName, _accountKey);
        var length = await AzureStorageEmulatorFixture.GetVhdSizeInContainerAsync(vhdBlobUri);

        // Clean the files in the container and the local file system
        _azureStorageEmulatorFixture.DeleteVhdFileInContainer(containerName, vhdBlobUri);
        _azureStorageEmulatorFixture.DeleteVhdFile(vhdFilePath);

        Assert.Equal(ResizeResult.Success, result);
        Assert.Equal(newSizeInGb, (int)ByteSize.FromBytes(length).GigaBytes);
    }
}

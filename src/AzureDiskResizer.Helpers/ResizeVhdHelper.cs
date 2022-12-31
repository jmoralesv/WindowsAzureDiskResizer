using Azure;
using Azure.Storage;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using AzureDiskResizer.Helpers.DiscUtils;
using ByteSizeLib;

namespace AzureDiskResizer.Helpers;

/// <summary>
/// This helper class has methods for resizing a VHD file located in an Azure Storage account.
/// </summary>
public class ResizeVhdHelper
{
    private PageBlobClient _blob = null!; // The null-forgiving operator tells the compiler to ignore the warning
    private byte[] _footer = new byte[512];
    private Footer _footerInstance = null!; // The null-forgiving operator tells the compiler to ignore the warning
    private long _originalLength;

    /// <summary>
    /// Gets or sets whether the resize operation will expand the VHD file.
    /// </summary>
    public bool IsExpand { get; set; } = true;

    /// <summary>
    /// Gets or sets the new size for the VHD file.
    /// </summary>
    public ByteSize NewSize { get; private set; }

    /// <summary>
    /// Tries to resize the VHD file, using the parameters specified.
    /// </summary>
    /// <param name="newSizeInGb">The new size of the VHD file, in gigabytes.</param>
    /// <param name="blobUri">The <see cref="Uri"/> to locate the VHD in the Azure Storage account.</param>
    /// <param name="accountName">The name of the Azure Storage account.</param>
    /// <param name="accountKey">The key of the Azure Storage account.</param>
    /// <returns>Returns <see cref="ResizeResult.Error"/> if there were issues while trying to do the resize operation.
    /// Returns <see cref="ResizeResult.Shrink"/> if this is a shrink operation which needs user confirmation.
    /// Returns <see cref="ResizeResult.Success"/> if everything went fine.</returns>
    public async Task<ResizeResult> ResizeVhdBlobAsync(int newSizeInGb, Uri blobUri, string? accountName, string? accountKey)
    {
        NewSize = ByteSize.FromGigaBytes(newSizeInGb);

        // Check if blob exists
        _blob = new PageBlobClient(blobUri);
        if (!string.IsNullOrEmpty(accountName) && !string.IsNullOrEmpty(accountKey))
        {
            var credential = new StorageSharedKeyCredential(accountName, accountKey);
            _blob = new PageBlobClient(blobUri, credential);
        }
        try
        {
            if (!await _blob.ExistsAsync())
            {
                Console.WriteLine("The specified blob does not exist.");
                return ResizeResult.Error;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"The specified storage account credentials are invalid.\n{ex}");
            return ResizeResult.Error;
        }

        // Determine blob attributes
        Console.WriteLine($"[{DateTime.Now.ToShortTimeString()}] Determining blob size...");
        _originalLength = (await _blob.GetPropertiesAsync()).Value.ContentLength;

        // Read current footer
        Console.WriteLine($"[{DateTime.Now.ToShortTimeString()}] Reading VHD file format footer...");
        _footer = new byte[512];
        using (var stream = new MemoryStream())
        {
            var response = await _blob.DownloadStreamingAsync(new BlobDownloadOptions
            {
                Range = new HttpRange(_originalLength - 512, 512),
            });
            await response.Value.Content.CopyToAsync(stream);
            stream.Position = 0;
            stream.Read(_footer, 0, 512);
            stream.Close();
        }

        _footerInstance = Footer.FromBytes(_footer, 0);

        // Make sure this is a "fixed" disk
        if (_footerInstance.DiskType != FileType.Fixed)
        {
            Console.WriteLine("The specified VHD blob is not a fixed-size disk. WindowsAzureDiskResizer can only resize fixed-size VHD files.");
            return ResizeResult.Error;
        }
        if (_footerInstance.CurrentSize >= (long)NewSize.Bytes)
        {
            // The specified VHD blob is larger than the specified new size. Shrinking disks is a potentially dangerous operation
            // Ask the user for confirmation
            return ResizeResult.Shrink;
        }
        return await DoResizeVhdBlobAsync();
    }

    /// <summary>
    /// Does the resize operation, based on the parameters specified when method <see cref="ResizeVhdBlobAsync(int, Uri, string, string)"/> was called.
    /// Please first call method <see cref="ResizeVhdBlobAsync(int, Uri, string, string)"/> before calling this method.
    /// </summary>
    /// <returns><see cref="ResizeResult.Success"/> if the resize operation went fine.</returns>
    public async Task<ResizeResult> DoResizeVhdBlobAsync()
    {
        Console.WriteLine($"[{DateTime.Now.ToShortTimeString()}] VHD file format fixed, current size {_footerInstance.CurrentSize} bytes.");

        // Expand the blob
        Console.WriteLine($"[{DateTime.Now.ToShortTimeString()}] Resizing containing blob...");
        await _blob.ResizeAsync((long)NewSize.Bytes + 512L);

        // Change footer size values
        Console.WriteLine($"[{DateTime.Now.ToShortTimeString()}] Updating VHD file format footer...");
        _footerInstance.CurrentSize = (long)NewSize.Bytes;
        _footerInstance.OriginalSize = (long)NewSize.Bytes;
        _footerInstance.Geometry = Geometry.FromCapacity((long)NewSize.Bytes);
        _footerInstance.UpdateChecksum();

        _footer = new byte[512];
        _footerInstance.ToBytes(_footer, 0);

        Console.WriteLine($"[{DateTime.Now.ToShortTimeString()}] New VHD file size {_footerInstance.CurrentSize} bytes, checksum {_footerInstance.Checksum}.");

        // Write new footer
        Console.WriteLine($"[{DateTime.Now.ToShortTimeString()}] Writing VHD file format footer...");
        using (var stream = new MemoryStream(_footer))
        {
            await _blob.UploadPagesAsync(stream, (long)NewSize.Bytes);
        }

        // Write 0 values where the footer used to be
        if (IsExpand)
        {
            Console.WriteLine($"[{DateTime.Now.ToShortTimeString()}] Overwriting the old VHD file footer with zeroes...");
            await _blob.ClearPagesAsync(new HttpRange(_originalLength - 512, 512));
        }

        // Done!
        Console.WriteLine($"[{DateTime.Now.ToShortTimeString()}] Done!");
        return ResizeResult.Success;
    }
}

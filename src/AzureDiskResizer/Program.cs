using AzureDiskResizer.Helpers;

Console.Title = typeof(Program).Assembly.GetName().Name ?? "AzureDiskResizer";
Console.OutputEncoding = System.Text.Encoding.UTF8;

WriteHeader();

// Check argument count
if (args.Length < 2)
{
    WriteUsage();
    return -1;
}

// Parse arguments
if (!int.TryParse(args[0], out var newSizeInGb) || (newSizeInGb * 1024 * 1024 * 1024) % 512 != 0)
{
    Console.WriteLine("Argument size invalid. Please specify a valid disk size in GB (must be a whole number).");
    return -1;
}
if (!Uri.TryCreate(args[1], UriKind.Absolute, out var blobUri))
{
    Console.WriteLine("Argument bloburl invalid. Please specify a valid URL with an HTTP or HTTPS schema.");
    return -1;
}

var accountName = "";
var accountKey = "";
if (args.Length == 4)
{
    accountName = args[2];
    accountKey = args[3];
}
else if (!blobUri.Query.Contains("sig="))
{
    Console.WriteLine("Please specify either a blob URL with a shared access signature that allows write access or provide full storage credentials.");
    return -1;
}

// Verify size. Size for disk must be <= 1023 GB
if (newSizeInGb > 1023)
{
    Console.WriteLine("The given disk size exceeds 1023 GB. Microsoft Azure will not be able to start the virtual machine stored on this disk if you continue.");
    Console.WriteLine("See https://learn.microsoft.com/en-us/azure/cloud-services/cloud-services-sizes-specs for more information.");
    return -1;
}

// Start the resize process
var resizeVhdHelper = new ResizeVhdHelper();
var result = await resizeVhdHelper.ResizeVhdBlobAsync(newSizeInGb, blobUri, accountName, accountKey);
if (result != ResizeResult.Shrink)
    return (int)result;

Console.WriteLine("The specified VHD blob is larger than the specified new size. Shrinking disks is a potentially dangerous operation.");
Console.WriteLine("Do you want to continue with shrinking the disk? (y/n)");

while (true)
{
    var consoleKey = Console.ReadKey().KeyChar;
    switch (consoleKey)
    {
        case 'n':
            Console.WriteLine("Aborted.");
            return -1;
        case 'y':
        {
            resizeVhdHelper.IsExpand = false;
            var finalResult = await resizeVhdHelper.DoResizeVhdBlobAsync();
            return (int)finalResult;
        }
    }
}

static void WriteHeader()
{
    Console.WriteLine($"WindowsAzureDiskResizer v{typeof(Program).Assembly.GetName().Version}");
    Console.WriteLine($"Copyright \u00a9 {DateTime.Now.Year} Maarten Balliauw");
    Console.WriteLine();
}

static void WriteUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("   AzureDiskResizer.exe <size> <bloburl> <accountname> <accountkey>");
    Console.WriteLine();
    Console.WriteLine("     <size>         New disk size in GB");
    Console.WriteLine("     <bloburl>      Disk blob URL");
    Console.WriteLine("     <accountname>  Storage account (optional if bloburl contains SAS)");
    Console.WriteLine("     <accountkey>   Storage key (optional if bloburl contains SAS)");
    Console.WriteLine();
}

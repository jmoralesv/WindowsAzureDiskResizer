# WindowsAzureDiskResizer

Resizes a Microsoft Azure virtual disk directly in blob storage.

This project uses the latest version of .NET and not .NET Framework. It is meant to be run on Windows though.

See https://blog.maartenballiauw.be/post/2013/01/07/tales-from-the-trenches-resizing-a-windows-azure-virtual-disk-the-smooth-way.html for more info.

Binaries: https://blog.maartenballiauw.be/files/2013/1/WindowsAzureDiskResizer-1.0.0.0.zip

## Growing disks

The following steps should be taken for growing a disk:
* Shutdown the VM
* Delete the VM -or- detach the disk if it's not the OS disk
* In the Microsoft Azure portal, delete the disk (retain the data!) do that the lease Microsoft Azure has on it is removed
* Run AzureDiskResizer with the correct parameters
* In the Microsoft Azure portal, recreate the disk based on the existing blob
* Recreate the VM  -or- reattach the disk if it's not the OS disk
* Start the VM
* Use diskpart / disk management to resize the partition

## Shrinking disks

Note that shrinking a disk is a potentially dangerous operation and is currently unsupported by the tool. If this is required, the following *may* work, use at your own risk.

* Use diskpart / disk management to resize the partition to a size smaller than the current
  * For Windows, check https://learn.microsoft.com/en-us/previous-versions/technet-magazine/gg309169(v=msdn.10) on how to do this.
  * Some resources for Linux:
    * https://askubuntu.com/questions/390769/how-do-i-resize-partitions-using-command-line-without-using-a-gui-on-a-server
    * https://www.howtoforge.com/linux_resizing_ext3_partitions
    * http://positon.org/resize-an-ext3-ext4-partition
    * https://forums.justlinux.com/showthread.php?147711-Resizing-Ext2-Ext3-Partitions-resize2fs-and-fdisk
* Shutdown the VM
* Delete the VM -or- detach the disk if it's not the OS disk
* In the Microsoft Azure portal, delete the disk (retain the data!) do that the lease Microsoft Azure has on it is removed
* Run AzureDiskResizer with the correct parameters
* In the Microsoft Azure portal, recreate the disk based on the existing blob
* Recreate the VM -or- reattach the disk if it's not the OS disk
* Start the VM

## Disclaimer
Even though I have tested this on a couple of data disks without any problems, you are using the provided code and/or binaries at your own risk! I'm not responsible if something breaks! The provided code is as-is without warranty!

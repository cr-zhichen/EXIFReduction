using System.Security.Cryptography;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using Directory = System.IO.Directory;

namespace EXIFReduction;

/// <summary>
/// 通过EXIF信息创建文件夹结构
/// 上级文件夹实例为：2021年01月
/// </summary>
public static class CreateFolderStructure
{
    private static DateTime _startTime;
    private static int _totalFiles;

    public static void Start(string directory, int maxDegreeOfParallelismString)
    {
        Console.WriteLine("是否开始处理？(Y/N) 默认为 N");
        string? input = Console.ReadLine();
        if (input != "Y" && input != "y")
        {
            Console.WriteLine("按任意键退出");
            return;
        }

        CreateFolderStructureByExif(directory, maxDegreeOfParallelismString);

        DeleteEmptyDirectory(directory);

        Console.WriteLine($"处理完成，共处理 {_totalFiles} 个文件，耗时 {DateTime.Now - _startTime}");
    }

    private static void CreateFolderStructureByExif(string directory, int maxDegreeOfParallelismString)
    {
        // 获取所有文件(包括子文件夹)
        var allFiles = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).ToList();
        _startTime = DateTime.Now;
        _totalFiles = allFiles.Count;
        int currentFile = 0;

        // 获取所有文件的EXIF信息
        Parallel.ForEach(allFiles, new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelismString
        }, file =>
        {
            ProcessFile(directory, file);
            currentFile++;
            UpdateProgress(currentFile, _totalFiles);
        });
    }

    private static void ProcessFile(string directory, string file)
    {
        // 获取文件名
        string fileName = Path.GetFileName(file);
        DateTime dateTime = DateTime.Now;

        try
        {
            // 判断文件是否是jpg
            if (file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            {
                // 判断是否存在exif信息
                var directories = ImageMetadataReader.ReadMetadata(file);
                var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                // 如果存在exif信息，获取exif中的拍摄时间
                if (subIfdDirectory != null)
                {
                    var tempDateTime = subIfdDirectory.GetDateTime(ExifDirectoryBase.TagDateTimeOriginal);
                    if (tempDateTime < DateTime.Now && tempDateTime.Year > 2000)
                    {
                        dateTime = tempDateTime;
                    }
                }
            }

            // 判断文件是否是视频，如果是，则获取视频的创建媒体日期
            else if (file.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                     file.EndsWith(".mov", StringComparison.OrdinalIgnoreCase))
            {
                var directories = ImageMetadataReader.ReadMetadata(file);
                var quickTimeDirectory = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
                if (quickTimeDirectory != null)
                {
                    var tempDateTime = quickTimeDirectory.GetDateTime(QuickTimeMovieHeaderDirectory.TagCreated);
                    if (tempDateTime < DateTime.Now && tempDateTime.Year > 2000)
                    {
                        dateTime = tempDateTime;
                    }
                }
            }
        }
        catch (Exception)
        {
            // ignored
        }

        // 获取文件的创建日期
        DateTime fileCreationDateTime = File.GetCreationTime(file);

        //判断时间是否为未来时间和2000年以前的时间
        if (fileCreationDateTime > DateTime.Now || fileCreationDateTime.Year < 2000)
        {
            fileCreationDateTime = dateTime;
        }

        // 获取文件的修改日期
        DateTime fileModificationDateTime = File.GetLastWriteTime(file);

        //判断时间是否为未来时间和2000年以前的时间
        if (fileModificationDateTime > DateTime.Now || fileModificationDateTime.Year < 2000)
        {
            fileModificationDateTime = dateTime;
        }

        //判断 dateTime、fileCreationDateTime、fileModificationDateTime、fileNameTime 哪个是最早的时间，将最早的时间赋值给 dateTime
        if (fileCreationDateTime < dateTime)
        {
            dateTime = fileCreationDateTime;
        }

        if (fileModificationDateTime < dateTime)
        {
            dateTime = fileModificationDateTime;
        }

        // 获取年月
        string yearMonth = dateTime.ToString("yyyy年MM月");

        // 如果文件夹不存在，则创建文件夹
        string yearMonthDirectory = Path.Combine(directory, yearMonth);
        if (!Directory.Exists(yearMonthDirectory))
        {
            Directory.CreateDirectory(yearMonthDirectory);
        }

        try
        {
            // 将文件移动到文件夹中,如果文件已经存在，则计算哈希，如果哈希相同，则跳过，如果哈希不同，则在文件名后面加上hash值
            string newFileName = Path.Combine(yearMonthDirectory, fileName);
            if (File.Exists(newFileName))
            {
                // 判断哈希是否相同
                byte[] oldFileHash = ComputeFileHash(file);
                byte[] newFileHash = ComputeFileHash(newFileName);
                if (!oldFileHash.SequenceEqual(newFileHash))
                {
                    newFileName = Path.Combine(yearMonthDirectory,
                        $"{Path.GetFileNameWithoutExtension(fileName)}_{BitConverter.ToString(oldFileHash).Replace("-", "")}{Path.GetExtension(fileName)}");
                }
                else
                {
                    File.Delete(file);
                    return;
                }
            }

            File.Move(file, newFileName);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    // 删除文件夹中的空文件夹
    private static void DeleteEmptyDirectory(string directory)
    {
        foreach (string dir in Directory.GetDirectories(directory))
        {
            DeleteEmptyDirectory(dir);
            if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
            {
                Directory.Delete(dir, false);
            }
        }
    }

    private static void UpdateProgress(int currentFile, int totalFiles)
    {
        double percent = (double)currentFile / totalFiles * 100;
        TimeSpan elapsed = DateTime.Now - _startTime;

        // 估计总时间
        TimeSpan estimatedTotal = TimeSpan.FromTicks((long)(elapsed.Ticks / (percent / 100)));
        // 预计剩余时间
        TimeSpan remaining = estimatedTotal - elapsed;

        Console.Write(
            $"\rProgress: {currentFile}/{totalFiles} files processed ({percent:N2}%) - Estimated Time Remaining: {remaining:hh\\:mm\\:ss}");
    }

    private static byte[] ComputeFileHash(string file)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(file);
        return md5.ComputeHash(stream);
    }
}
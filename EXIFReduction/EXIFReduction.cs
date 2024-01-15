using ImageMagick;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using Directory = System.IO.Directory;

namespace EXIFReduction;

/// <summary>
/// 从EXIF信息或文件名中恢复文件的创建日期和修改日期
/// </summary>
public static class ExifReduction
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

        UpdateFiles(directory, maxDegreeOfParallelismString);

        Console.WriteLine($"处理完成，共处理 {_totalFiles} 个文件，耗时 {DateTime.Now - _startTime}");
    }

    private static void UpdateFiles(string directory, int maxDegreeOfParallelismString)
    {
        var allFiles = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).ToList();
        _startTime = DateTime.Now;
        _totalFiles = allFiles.Count;
        int currentFile = 0;

        var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelismString };
        Parallel.ForEach(allFiles, options, (file) =>
        {
            ProcessFile(file);
            currentFile++;
            UpdateProgress(currentFile, _totalFiles);
        });
    }

    private static void ProcessFile(string file)
    {
        // 获取文件名
        string fileName = Path.GetFileName(file);
        DateTime dateTime = DateTime.Now;

        // 获取上级目录名
        string directoryName = Path.GetFileName(Path.GetDirectoryName(file)) ?? "";
        // 获取到的目录是类似这样的2017年01月这样的格式，需要将年字替换为-，月字删除
        directoryName = directoryName.Replace("年", "-").Replace("月", "");
        // 将日期字符串转换为日期对象
        try
        {
            dateTime = DateTime.ParseExact(directoryName, "yyyy-MM", null);
            // 将dateTime设置为每月的最后一天最后一秒
            dateTime = new DateTime(dateTime.Year, dateTime.Month,
                DateTime.DaysInMonth(dateTime.Year, dateTime.Month), 23, 59, 59);
        }
        catch (Exception)
        {
            // ignored
        }

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

        // 获取文件名中的日期
        DateTime? fileNameTime = GetDateTime(fileName);

        //判断时间是否为未来时间和2000年以前的时间
        if (fileNameTime != null)
        {
            if (fileNameTime > DateTime.Now || fileNameTime.Value.Year < 2000)
            {
                fileNameTime = null;
            }
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

        if (fileNameTime != null && fileNameTime < dateTime)
        {
            dateTime = fileNameTime.Value;
        }


        if (file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
        {
            UpdateJpegFile(file, dateTime);
        }
        else
        {
            UpdateFileTimestamps(file, dateTime);
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


    private static void UpdateJpegFile(string filePath, DateTime dateTime)
    {
        try
        {
            // 更新 JPEG 文件的 EXIF 数据
            using var image = new MagickImage(filePath);
            image.SetAttribute("exif:DateTimeOriginal", DateTime.Now.ToString("yyyy:MM:dd HH:mm:ss"));
            image.Write(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing JPEG file {filePath}: {ex.Message}");
        }

        // 更新文件的修改日期
        UpdateFileTimestamps(filePath, dateTime);
        // Console.WriteLine($"Updated JPEG file: {filePath}");
    }

    private static void UpdateFileTimestamps(string filePath, DateTime dateTime)
    {
        try
        {
            // 更新文件的创建日期和修改日期
            File.SetCreationTime(filePath, dateTime);
            File.SetLastWriteTime(filePath, dateTime);

            // Console.WriteLine($"Updated timestamps for file: {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating timestamps for file {filePath}: {ex.Message}");
        }
    }

    //文件名匹配
    private static DateTime? GetDateTime(string fileName)
    {
        // 使用正则表达式匹配文件名，连续的8个数字加上-加上连续6个数字，如：20190101-132809
        if (System.Text.RegularExpressions.Regex.IsMatch(fileName, @"(?<!\d)\d{8}-\d{6}(?!\d)"))
        {
            // 获取文件名中的日期（日期可能存在任何位置）
            string dateString = System.Text.RegularExpressions.Regex.Match(fileName, @"(?<!\d)\d{8}-\d{6}(?!\d)")
                .Value;
            // 将日期字符串转换为日期对象
            try
            {
                return DateTime.ParseExact(dateString, "yyyyMMdd-HHmmss", null);
            }
            catch (Exception)
            {
                // ignored
            }
        }
        // 使用正则表达式匹配文件名，连续的8个数字加上-加上连续6个数字，如：20190101-132809
        else if (System.Text.RegularExpressions.Regex.IsMatch(fileName, @"(?<!\d)\d{8}_\d{6}(?!\d)"))
        {
            // 获取文件名中的日期（日期可能存在任何位置）
            string dateString = System.Text.RegularExpressions.Regex.Match(fileName, @"(?<!\d)\d{8}_\d{6}(?!\d)")
                .Value;
            // 将日期字符串转换为日期对象
            try
            {
                return DateTime.ParseExact(dateString, "yyyyMMdd_HHmmss", null);
            }
            catch (Exception)
            {
                // ignored
            }
        }
        // 使用正则表达式匹配文件名，连续的8个数字，如：20190101
        else if (System.Text.RegularExpressions.Regex.IsMatch(fileName, @"(?<!\d)\d{8}(?!\d)"))
        {
            // 获取文件名中的日期（日期可能存在任何位置）
            string dateString = System.Text.RegularExpressions.Regex.Match(fileName, @"(?<!\d)\d{8}(?!\d)").Value;
            // 将日期字符串转换为日期对象
            try
            {
                return DateTime.ParseExact(dateString, "yyyyMMdd", null);
            }
            catch (Exception)
            {
                // ignored
            }
        }
        // 使用正则表达式匹配文件名，连续的13个数字，是毫秒时间戳，如：1484224755681
        else if (System.Text.RegularExpressions.Regex.IsMatch(fileName, @"(?<!\d)\d{13}(?!\d)"))
        {
            // 获取文件名中的日期（日期可能存在任何位置）
            string dateString = System.Text.RegularExpressions.Regex.Match(fileName, @"(?<!\d)\d{13}(?!\d)").Value;
            // 将日期字符串转换为日期对象
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(dateString)).DateTime.ToLocalTime();
            }
            catch (Exception)
            {
                // ignored
            }
        }
        // 使用正则表达式匹配文件名，连续的14个数字，类似20150325100521
        else if (System.Text.RegularExpressions.Regex.IsMatch(fileName, @"(?<!\d)\d{14}(?!\d)"))
        {
            // 获取文件名中的日期（日期可能存在任何位置）
            string dateString = System.Text.RegularExpressions.Regex.Match(fileName, @"(?<!\d)\d{14}(?!\d)").Value;
            // 将日期字符串转换为日期对象
            try
            {
                return DateTime.ParseExact(dateString, "yyyyMMddHHmmss", null);
            }
            catch (Exception)
            {
                // ignored
            }
        }
        // 使用正则表达式匹配类似 2020-06-11-18-38-22-989 的文件名
        else if (System.Text.RegularExpressions.Regex.IsMatch(fileName,
                     @"(?<!\d)\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2}-\d{3}(?!\d)"))
        {
            // 获取文件名中的日期（日期可能存在任何位置）
            string dateString = System.Text.RegularExpressions.Regex
                .Match(fileName, @"(?<!\d)\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2}-\d{3}(?!\d)").Value;
            // 将日期字符串转换为日期对象
            try
            {
                return DateTime.ParseExact(dateString, "yyyy-MM-dd-HH-mm-ss-fff", null);
            }
            catch (Exception)
            {
                // ignored
            }
        }
        // 使用正则表达式匹配类似 2021-04-18-085821104 的文件名
        else if (System.Text.RegularExpressions.Regex.IsMatch(fileName,
                     @"(?<!\d)\d{4}-\d{2}-\d{2}-\d{9}(?!\d)"))
        {
            // 获取文件名中的日期（日期可能存在任何位置）
            string dateString = System.Text.RegularExpressions.Regex
                .Match(fileName, @"(?<!\d)\d{4}-\d{2}-\d{2}-\d{9}(?!\d)").Value;
            // 将日期字符串转换为日期对象
            try
            {
                return DateTime.ParseExact(dateString, "yyyy-MM-dd-HHmmssfff", null);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        return null;
    }
}
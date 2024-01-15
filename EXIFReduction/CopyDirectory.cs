using System.Security.Cryptography;

namespace EXIFReduction;

public class CopyDirectory
{
    private static DateTime _startTime;
    private static int _totalFiles;

    public static void Start(string directory, string targetDirectory, int maxDegreeOfParallelismString)
    {
        Console.WriteLine("是否开始处理？(Y/N) 默认为 N");
        string? input = Console.ReadLine();
        if (input != "Y" && input != "y")
        {
            Console.WriteLine("按任意键退出");
            return;
        }

        CopyFile(directory, targetDirectory, maxDegreeOfParallelismString);

        Console.WriteLine($"处理完成，共处理 {_totalFiles} 个文件，耗时 {DateTime.Now - _startTime}");
    }

    private static void CopyFile(string directory, string targetDirectory, int maxDegreeOfParallelismString)
    {
        // 获取所有文件(包括子文件夹)
        var allFiles = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).ToList();
        _startTime = DateTime.Now;
        _totalFiles = allFiles.Count;
        int currentFile = 0;

        // 将所有文件复制到目标目录（包括相对的目录结构）
        // 例如：源路径为：D:\test\ 目标路径为：E:\test\  获取到的文件为：D:\test\2022\1.jpg 则复制到：E:\test\2022\1.jpg
        Parallel.ForEach(allFiles, new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelismString
        }, file =>
        {
            string targetFile = file.Replace(directory, targetDirectory);
            string targetFileDirectory = Path.GetDirectoryName(targetFile) ?? "";
            if (!Directory.Exists(targetFileDirectory))
            {
                Directory.CreateDirectory(targetFileDirectory);
            }

            // 判断文件是否存在，存在则判断hash是否相同，如果哈希相同则跳过，如果哈希不同则在文件名后面加上hash值
            try
            {
                if (File.Exists(targetFile))
                {
                    byte[] sourceFileHash = ComputeFileHash(file);
                    byte[] targetFileHash = ComputeFileHash(targetFile);
                    if (sourceFileHash.SequenceEqual(targetFileHash))
                    {
                        currentFile++;
                        UpdateProgress(currentFile, _totalFiles);
                        return;
                    }

                    string fileName = Path.GetFileNameWithoutExtension(targetFile);
                    string extension = Path.GetExtension(targetFile);
                    targetFile = Path.Combine(targetFileDirectory,
                        $"{fileName}_{BitConverter.ToString(sourceFileHash).Replace("-", "")}{extension}");
                }

                File.Copy(file, targetFile);
            }
            catch (Exception)
            {
                // ignored
            }


            currentFile++;
            UpdateProgress(currentFile, _totalFiles);
        });
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
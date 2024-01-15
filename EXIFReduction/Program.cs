using EXIFReduction;

#region 从控制台获取参数

string directory = Directory.GetCurrentDirectory();
Console.WriteLine("当前选择目录为: " + directory);
Console.WriteLine("是否更改目录？(Y/N) 默认为 N");
string? changeDirectory = Console.ReadLine();
if (changeDirectory == "Y" || changeDirectory == "y")
{
    Console.WriteLine("请输入目录: ");
    directory = Console.ReadLine() ?? "";
    if (string.IsNullOrWhiteSpace(directory))
    {
        Console.WriteLine("目录不能为空");
        return;
    }

    Console.WriteLine("当前选择目录为: " + directory);
}

Console.WriteLine("请输入线程数(默认为5): ");
string? threadCount = Console.ReadLine();
int threadCountInt = !string.IsNullOrWhiteSpace(threadCount) ? int.Parse(threadCount) : 5;

// 1-EXIF信息恢复
// 2-文件夹结构创建
// 3-目录复制
Console.WriteLine("请输入操作类型0-全部执行\n1-EXIF信息恢复\n2-文件夹结构创建\n3-目录复制\n(默认为1): ");
string? operationType = Console.ReadLine();
int operationTypeInt = !string.IsNullOrWhiteSpace(operationType) ? int.Parse(operationType) : 1;

switch (operationTypeInt)
{
    case 1:
        // 进行EXIF信息的恢复
        ExifReduction.Start(directory, threadCountInt);
        break;
    case 2:
        // 进行文件夹结构的创建
        CreateFolderStructure.Start(directory, threadCountInt);
        break;
    case 3:
        Console.WriteLine("请输入目标目录: ");
        string targetDirectory = Console.ReadLine() ?? "";
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            Console.WriteLine("目标目录不能为空");
            return;
        }

        // 进行目录复制
        CopyDirectory.Start(directory, targetDirectory, threadCountInt);
        break;
    default:
        Console.WriteLine("操作类型错误");
        return;
}

Console.WriteLine("按下回车键退出");
Console.ReadLine();

#endregion
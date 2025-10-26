using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace DiskCleaner
{
    // Класс конфигурации
    public class AppConfig
    {
        public List<string> FoldersPaths { get; set; } = new List<string>();
        public int DiskUsageThreshold { get; set; } = 80;
        public int LogRetentionDays { get; set; } = 10;

        public static AppConfig LoadFromFile(string configPath)
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    // Создаем конфиг по умолчанию
                    var defaultConfig = new AppConfig
                    {
                        FoldersPaths = new List<string>
                        {
                            @"C:\Temp",
                            @"C:\Logs"
                        },
                        DiskUsageThreshold = 80,
                        LogRetentionDays = 10
                    };
                    defaultConfig.SaveToFile(configPath);
                    return defaultConfig;
                }

                var json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка загрузки конфигурации: {ex.Message}");
                return new AppConfig();
            }
        }

        public void SaveToFile(string configPath)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка сохранения конфигурации: {ex.Message}");
            }
        }
    }

    // Статический класс для логирования
    public static class Logger
    {
        private static readonly string LogsDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "logs");

        private static readonly string LogFileName = $"disk_cleaner_{DateTime.Now:yyyyMMdd}.log";
        private static readonly string LogFilePath = Path.Combine(LogsDirectory, LogFileName);

        static Logger()
        {
            // Создаем папку для логов при первом обращении
            EnsureLogsDirectoryExists();
        }

        private static void EnsureLogsDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(LogsDirectory))
                {
                    Directory.CreateDirectory(LogsDirectory);
                    Console.WriteLine($"{DateTime.Now:dd-MM-yyyyd HH:mm:ss} [INFO] Создана папка для логов: {LogsDirectory}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:dd-MM-yyyy HH:mm:ss} [ERROR] Не удалось создать папку для логов: {ex.Message}");
            }
        }

        public static void LogInfo(string message)
        {
            Log("INFO", message);
        }

        public static void LogError(string message)
        {
            Log("ERROR", message);
        }

        public static void LogDeletedFile(string filePath)
        {
            Log("DELETED", $"Удален файл: {filePath}");
        }

        public static void LogDeletedLog(string logFileName)
        {
            Log("CLEANUP", $"Удален старый лог: {logFileName}");
        }

        private static void Log(string level, string message)
        {
            try
            {
                var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
                Console.WriteLine(logMessage);

                // Убеждаемся, что папка существует перед записью
                EnsureLogsDirectoryExists();
                File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Если не удалось записать в лог, выводим только в консоль
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERROR] Не удалось записать в лог: {ex.Message}");
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}");
            }
        }

        // Метод для очистки старых логов
        public static void CleanOldLogs(int retentionDays)
        {
            try
            {
                if (!Directory.Exists(LogsDirectory))
                {
                    LogInfo("Папка логов не существует, очистка не требуется");
                    return;
                }

                string logFilePattern = "disk_cleaner_*.log";
                var logFiles = Directory.GetFiles(LogsDirectory, logFilePattern);
                int deletedCount = 0;

                foreach (var logFile in logFiles)
                {
                    // Пропускаем текущий лог-файл дня
                    if (logFile.Equals(LogFilePath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        var fileInfo = new FileInfo(logFile);
                        if (fileInfo.LastWriteTime < DateTime.Now.AddDays(-retentionDays))
                        {
                            fileInfo.Delete();
                            LogDeletedLog(Path.GetFileName(logFile));
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Ошибка удаления старого лога {Path.GetFileName(logFile)}: {ex.Message}");
                    }
                }

                if (deletedCount > 0)
                {
                    LogInfo($"Очистка логов завершена. Удалено файлов: {deletedCount}");
                }
                else
                {
                    LogInfo("Старые логи для удаления не найдены");
                }
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при очистке старых логов: {ex.Message}");
            }
        }

        // Метод для получения информации о лог-файлах
        public static void DisplayLogInfo()
        {
            try
            {
                if (!Directory.Exists(LogsDirectory))
                {
                    LogInfo("Папка логов не существует");
                    return;
                }

                string logFilePattern = "disk_cleaner_*.log";
                var logFiles = Directory.GetFiles(LogsDirectory, logFilePattern)
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.LastWriteTime)
                    .ToList();

                LogInfo($"Папка логов: {LogsDirectory}");
                LogInfo($"Текущий лог-файл: {Path.GetFileName(LogFilePath)}");
                LogInfo($"Всего лог-файлов: {logFiles.Count}");

                if (logFiles.Count > 0)
                {
                    LogInfo($"Самый старый лог: {logFiles.First().Name} ({logFiles.First().LastWriteTime:yyyy-MM-dd})");
                    LogInfo($"Самый новый лог: {logFiles.Last().Name} ({logFiles.Last().LastWriteTime:yyyy-MM-dd})");
                }
            }
            catch (Exception ex)
            {
                LogError($"Ошибка получения информации о логах: {ex.Message}");
            }
        }

        // Метод для получения пути к папке логов (может пригодиться)
        public static string GetLogsDirectory()
        {
            return LogsDirectory;
        }
    }

    // Основной класс для очистки диска
    public class DiskCleaner
    {
        private readonly AppConfig _config;
        private readonly string _driveRoot;

        public DiskCleaner(AppConfig config)
        {
            _config = config;
            _driveRoot = GetDriveRootFromFirstFolder();
        }

        private string GetDriveRootFromFirstFolder()
        {
            if (_config.FoldersPaths.Count == 0)
                return null;

            try
            {
                var firstFolder = _config.FoldersPaths[0];
                var directoryInfo = new DirectoryInfo(firstFolder);
                return Path.GetPathRoot(directoryInfo.FullName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void Clean()
        {
            Logger.LogInfo("=== ЗАПУСК ОЧИСТКИ ДИСКА ===");
            Logger.LogInfo($"Дата запуска: {DateTime.Now:dd-MM-yyyy HH:mm:ss}");

            try
            {
                // Показываем информацию о логах
                Logger.DisplayLogInfo();

                // Очищаем старые логи
                Logger.LogInfo($"Очистка логов старше {_config.LogRetentionDays} дней...");
                Logger.CleanOldLogs(_config.LogRetentionDays);

                // Проверяем наличие папок в конфигурации
                if (_config.FoldersPaths.Count == 0)
                {
                    Logger.LogError("В конфигурации не указаны папки для очистки");
                    return;
                }

                // Проверяем существование папок
                var existingFolders = _config.FoldersPaths.Where(Directory.Exists).ToList();
                if (existingFolders.Count == 0)
                {
                    Logger.LogError("Ни одна из указанных папок не существует");
                    return;
                }

                // Получаем информацию о диске
                var driveInfo = GetDriveInfo();

                if (driveInfo == null)
                {
                    Logger.LogError("Не удалось получить информацию о диске");
                    return;
                }

                double diskUsagePercent = (double)(driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / driveInfo.TotalSize * 100;
                Logger.LogInfo($"Заполненность диска: {diskUsagePercent:F2}% (порог: {_config.DiskUsageThreshold}%)");

                if (diskUsagePercent < _config.DiskUsageThreshold)
                {
                    Logger.LogInfo($"Заполненность диска ({diskUsagePercent:F2}%) ниже порога {_config.DiskUsageThreshold}%. Очистка не требуется.");
                    return;
                }

                Logger.LogInfo($"Заполненность диска превышает порог {_config.DiskUsageThreshold}%. Начинаем очистку...");

                // Запускаем процесс очистки
                CleanUntilThresholdReached();

                Logger.LogInfo("Очистка завершена");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка во время очистки: {ex.Message}");
            }
        }

        private DriveInfo GetDriveInfo()
        {
            try
            {
                if (string.IsNullOrEmpty(_driveRoot))
                    return null;

                return new DriveInfo(_driveRoot);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка получения информации о диске: {ex.Message}");
                return null;
            }
        }

        private double GetCurrentDiskUsagePercent()
        {
            var driveInfo = GetDriveInfo();
            if (driveInfo == null)
                return 100; // Если не удалось получить информацию, считаем что диск полный

            return (double)(driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / driveInfo.TotalSize * 100;
        }

        private void CleanUntilThresholdReached()
        {
            int iteration = 0;
            long totalFreedSpace = 0;

            while (true)
            {
                iteration++;
                Logger.LogInfo($"Итерация очистки #{iteration}");

                // Проверяем текущую заполненность диска
                double currentUsagePercent = GetCurrentDiskUsagePercent();

                if (currentUsagePercent <= _config.DiskUsageThreshold)
                {
                    Logger.LogInfo($"Достигнут целевой порог заполненности диска: {currentUsagePercent:F2}%");
                    Logger.LogInfo($"Всего освобождено: {FormatFileSize(totalFreedSpace)}");
                    break;
                }

                // Получаем самые старые файлы из каждой папки
                var oldestFiles = GetOldestFilesFromFolders();

                // Фильтруем null и несуществующие файлы
                var validFiles = oldestFiles.Where(f => f != null && f.Exists).ToList();

                if (validFiles.Count == 0)
                {
                    Logger.LogInfo("Больше нет файлов для удаления");
                    break;
                }

                // Сортируем файлы по дате создания (от самых старых)
                validFiles.Sort((f1, f2) => f1.CreationTime.CompareTo(f2.CreationTime));

                // Удаляем по одному самому старому файлу из всех папок
                bool deletedAny = false;
                foreach (var fileInfo in validFiles)
                {
                    if (fileInfo.Exists)
                    {
                        try
                        {
                            long fileSize = fileInfo.Length;
                            fileInfo.Delete();
                            Logger.LogDeletedFile(fileInfo.FullName);
                            Logger.LogInfo($"Освобождено {FormatFileSize(fileSize)}");
                            totalFreedSpace += fileSize;
                            deletedAny = true;

                            // Проверяем заполненность после каждого удаления
                            currentUsagePercent = GetCurrentDiskUsagePercent();

                            if (currentUsagePercent <= _config.DiskUsageThreshold)
                            {
                                Logger.LogInfo($"Достигнут целевой порог после удаления файла. Всего освобождено: {FormatFileSize(totalFreedSpace)}");
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Ошибка удаления файла {fileInfo.FullName}: {ex.Message}");
                        }
                    }
                }

                if (!deletedAny)
                {
                    Logger.LogInfo("Не удалось удалить ни одного файла в этой итерации");
                    break;
                }

                // Небольшая пауза между итерациями
                Thread.Sleep(500);
            }
        }

        private List<FileInfo> GetOldestFilesFromFolders()
        {
            var oldestFiles = new List<FileInfo>();

            foreach (var folderPath in _config.FoldersPaths)
            {
                try
                {
                    if (!Directory.Exists(folderPath))
                    {
                        Logger.LogError($"Папка не существует: {folderPath}");
                        oldestFiles.Add(null);
                        continue;
                    }

                    var directory = new DirectoryInfo(folderPath);
                    var files = directory.GetFiles();

                    if (files.Length == 0)
                    {
                        oldestFiles.Add(null);
                        continue;
                    }

                    var oldestFile = files.OrderBy(f => f.CreationTime).First();
                    oldestFiles.Add(oldestFile);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Ошибка доступа к папке {folderPath}: {ex.Message}");
                    oldestFiles.Add(null);
                }
            }

            return oldestFiles;
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n2} {suffixes[counter]}";
        }
    }

    // Главный класс программы
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Определяем путь к конфигурационному файлу
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "disk_cleaner_config.json");

                // Загружаем конфигурацию (файл создастся автоматически если его нет)
                Logger.LogInfo("Загрузка конфигурации...");
                var config = AppConfig.LoadFromFile(configPath);

                Logger.LogInfo($"Настроено папок для очистки: {config.FoldersPaths.Count}");
                Logger.LogInfo($"Порог заполненности диска: {config.DiskUsageThreshold}%");
                Logger.LogInfo($"Срок хранения логов: {config.LogRetentionDays} дней");

                // Проверяем, есть ли папки в конфигурации
                if (config.FoldersPaths.Count == 0)
                {
                    Logger.LogError("В конфигурационном файле не указаны папки для очистки.");
                    Logger.LogError($"Пожалуйста, отредактируйте файл: {configPath}");
                    WaitAndExit();
                    return;
                }

                // Запускаем очистку
                var cleaner = new DiskCleaner(config);
                cleaner.Clean();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Критическая ошибка: {ex.Message}");
            }

            Logger.LogInfo("Программа завершена");
            WaitAndExit();
        }

        static void WaitAndExit()
        {
            Logger.LogInfo("Консоль закроется автоматически через 5 секунд...");
            Thread.Sleep(5000); // Задержка 5 секунд
        }
    }
}
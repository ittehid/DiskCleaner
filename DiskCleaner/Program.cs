using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace DiskCleaner
{    
    public class AppConfig
    {
        public List<string> MonitorFolders { get; set; } = new List<string>();
        public string ReserveFolder { get; set; } = @"C:\#REZERV#";
        public int FileMinAgeMinutes { get; set; } = 30;
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
                        MonitorFolders = new List<string>
                        {
                            @"C:\Канал-1",
                            @"C:\Канал-2"
                        },
                        ReserveFolder = @"C:\#REZERV#",
                        FileMinAgeMinutes = 30,
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
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(configPath, json, System.Text.Encoding.UTF8);
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

        private static readonly string LogFileName = $"disk_cleaner_{DateTime.Now:dd-MM-yyyy}.log";
        private static readonly string LogFilePath = Path.Combine(LogsDirectory, LogFileName);

        static Logger()
        {
            EnsureLogsDirectoryExists();
        }

        private static void EnsureLogsDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(LogsDirectory))
                {
                    Directory.CreateDirectory(LogsDirectory);
                    Console.WriteLine($"{DateTime.Now:dd-MM-yyyy HH:mm:ss} [INFO] Создана папка для логов: {LogsDirectory}");
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

        public static void LogMovedFile(string sourcePath, string targetPath)
        {
            Log("MOVED", $"Перемещен: {sourcePath} -> {targetPath}");
        }

        public static void LogDeletedFile(string filePath)
        {
            Log("DELETED", $"Удален: {filePath}");
        }

        public static void LogDeletedLog(string logFileName)
        {
            Log("CLEANUP", $"Удален старый лог: {logFileName}");
        }

        private static void Log(string level, string message)
        {
            try
            {
                var logMessage = $"{DateTime.Now:dd-MM-yyyy HH:mm:ss} [{level}] {message}";
                Console.WriteLine(logMessage);

                EnsureLogsDirectoryExists();
                File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:dd-MM-yyyy HH:mm:ss} [ERROR] Не удалось записать в лог: {ex.Message}");
                Console.WriteLine($"{DateTime.Now:dd-MM-yyyy HH:mm:ss} [{level}] {message}");
            }
        }

        public static void CleanOldLogs(int retentionDays)
        {
            try
            {
                if (!Directory.Exists(LogsDirectory))
                    return;

                string logFilePattern = "disk_cleaner_*.log";
                var logFiles = Directory.GetFiles(LogsDirectory, logFilePattern);
                int deletedCount = 0;

                foreach (var logFile in logFiles)
                {
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
                        LogError($"Ошибка удаления лога {Path.GetFileName(logFile)}: {ex.Message}");
                    }
                }

                if (deletedCount > 0)
                {
                    LogInfo($"Очистка логов завершена. Удалено: {deletedCount}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при очистке логов: {ex.Message}");
            }
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
            _driveRoot = GetDriveRootFromReserveFolder();
        }

        private string GetDriveRootFromReserveFolder()
        {
            try
            {
                var directoryInfo = new DirectoryInfo(_config.ReserveFolder);
                return Path.GetPathRoot(directoryInfo.FullName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void Clean()
        {
            Logger.LogInfo("=== ЗАПУСК ДВУХЭТАПНОЙ ОЧИСТКИ ===");

            try
            {
                // Очищаем старые логи
                Logger.CleanOldLogs(_config.LogRetentionDays);

                // Этап 1: Перемещение старых файлов в резерв
                Logger.LogInfo($"=== ЭТАП 1: ПЕРЕМЕЩЕНИЕ ФАЙЛОВ СТАРШЕ {_config.FileMinAgeMinutes} МИНУТ ===");
                MoveOldFilesToReserve();

                // Этап 2: Очистка резерва при нехватке места
                Logger.LogInfo("=== ЭТАП 2: ПРОВЕРКА ЗАПОЛНЕННОСТИ ДИСКА ===");
                CleanReserveIfNeeded();

                Logger.LogInfo("Очистка завершена");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка во время очистки: {ex.Message}");
            }
        }

        private void MoveOldFilesToReserve()
        {
            int movedCount = 0;

            foreach (var monitorFolder in _config.MonitorFolders)
            {
                try
                {
                    if (!Directory.Exists(monitorFolder))
                    {
                        Logger.LogError($"Папка мониторинга не существует: {monitorFolder}");
                        continue;
                    }

                    // Создаем соответствующую папку в резерве
                    string folderName = new DirectoryInfo(monitorFolder).Name;
                    string reserveSubFolder = Path.Combine(_config.ReserveFolder, folderName);
                    EnsureDirectoryExists(reserveSubFolder);

                    // Ищем файлы старше указанного времени
                    var directory = new DirectoryInfo(monitorFolder);
                    var oldFiles = directory.GetFiles()
                        .Where(f => (DateTime.Now - f.CreationTime).TotalMinutes >= _config.FileMinAgeMinutes)
                        .OrderBy(f => f.CreationTime)
                        .ToList();

                    foreach (var file in oldFiles)
                    {
                        try
                        {
                            string targetPath = Path.Combine(reserveSubFolder, file.Name);

                            // Если файл с таким именем уже существует, добавляем суффикс
                            if (File.Exists(targetPath))
                            {
                                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.Name);
                                string extension = Path.GetExtension(file.Name);
                                int counter = 1;

                                do
                                {
                                    targetPath = Path.Combine(reserveSubFolder,
                                        $"{fileNameWithoutExt}_{counter}{extension}");
                                    counter++;
                                } while (File.Exists(targetPath));
                            }

                            File.Move(file.FullName, targetPath);
                            Logger.LogMovedFile(file.FullName, targetPath);
                            movedCount++;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Ошибка перемещения {file.FullName}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Ошибка доступа к папке {monitorFolder}: {ex.Message}");
                }
            }

            if (movedCount > 0)
            {
                Logger.LogInfo($"Перемещено файлов в резерв: {movedCount}");
            }
        }

        private void CleanReserveIfNeeded()
        {
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
                Logger.LogInfo($"Заполненность диска ({diskUsagePercent:F2}%) ниже порога. Очистка резерва не требуется.");
                return;
            }

            Logger.LogInfo($"Заполненность диска превышает порог. Начинаем очистку резерва...");
            CleanReserveUntilThresholdReached();
        }

        private void CleanReserveUntilThresholdReached()
        {
            int iteration = 0;
            long totalFreedSpace = 0;

            while (true)
            {
                iteration++;
                Logger.LogInfo($"Итерация очистки резерва #{iteration}");

                // Проверяем текущую заполненность диска
                double currentUsagePercent = GetCurrentDiskUsagePercent();

                if (currentUsagePercent <= _config.DiskUsageThreshold)
                {
                    Logger.LogInfo($"Достигнут целевой порог заполненности: {currentUsagePercent:F2}%");
                    Logger.LogInfo($"Всего освобождено из резерва: {FormatFileSize(totalFreedSpace)}");
                    break;
                }

                // Получаем самые старые файлы из всех подпапок резерва
                var oldestFiles = GetOldestFilesFromReserve();

                if (oldestFiles.Count == 0)
                {
                    Logger.LogInfo("В резерве больше нет файлов для удаления");
                    break;
                }

                // Удаляем самый старый файл
                var oldestFile = oldestFiles.OrderBy(f => f.CreationTime).First();
                try
                {
                    long fileSize = oldestFile.Length;
                    oldestFile.Delete();
                    Logger.LogDeletedFile(oldestFile.FullName);
                    Logger.LogInfo($"Освобождено: {FormatFileSize(fileSize)}");
                    totalFreedSpace += fileSize;

                    // Проверяем заполненность после удаления
                    currentUsagePercent = GetCurrentDiskUsagePercent();

                    if (currentUsagePercent <= _config.DiskUsageThreshold)
                    {
                        Logger.LogInfo($"Достигнут целевой порог. Всего освобождено: {FormatFileSize(totalFreedSpace)}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Ошибка удаления {oldestFile.FullName}: {ex.Message}");
                }

                // Небольшая пауза между итерациями
                Thread.Sleep(500);
            }
        }

        private List<FileInfo> GetOldestFilesFromReserve()
        {
            var oldestFiles = new List<FileInfo>();

            if (!Directory.Exists(_config.ReserveFolder))
                return oldestFiles;

            try
            {
                var reserveDir = new DirectoryInfo(_config.ReserveFolder);
                foreach (var subDir in reserveDir.GetDirectories())
                {
                    try
                    {
                        var files = subDir.GetFiles();
                        if (files.Length > 0)
                        {
                            var oldestFile = files.OrderBy(f => f.CreationTime).First();
                            oldestFiles.Add(oldestFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Ошибка доступа к папке резерва {subDir.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка доступа к резервной папке: {ex.Message}");
            }

            return oldestFiles;
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
                return 100;

            return (double)(driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / driveInfo.TotalSize * 100;
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
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

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "disk_cleaner_config.json");

                Logger.LogInfo("Загрузка конфигурации...");
                var config = AppConfig.LoadFromFile(configPath);

                Logger.LogInfo($"Папок для мониторинга: {config.MonitorFolders.Count}");
                Logger.LogInfo($"Резервная папка: {config.ReserveFolder}");
                Logger.LogInfo($"Минимальный возраст файла: {config.FileMinAgeMinutes} мин");
                Logger.LogInfo($"Порог заполненности диска: {config.DiskUsageThreshold}%");

                if (config.MonitorFolders.Count == 0)
                {
                    Logger.LogError("В конфигурации не указаны папки для мониторинга.");
                    WaitAndExit();
                    return;
                }

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
            Logger.LogInfo("Консоль закроется через 5 секунд...");
            Thread.Sleep(5000);
        }
    }
}
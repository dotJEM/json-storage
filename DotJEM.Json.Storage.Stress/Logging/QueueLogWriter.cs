using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace DotJEM.Json.Storage.Stress.Logging
{
    public interface ILogWriter : IDisposable
    {
        void Write(string message);
        void Close();
    }

    public static class QueueingLogWriterExt
    {
        public static void Write(this ILogWriter self, object message) => self.Write(message.ToString());
    }

    public class QueueingLogWriter : ILogWriter
    {
        private readonly string path;
        private readonly long maxSize;
        private readonly int maxFiles;
        private readonly bool compress;
        private readonly string name;
        private readonly string directory;
        private readonly string extention;

        private readonly object padLock = new object();
        private readonly Queue<string> logQueue = new Queue<string>();
        private readonly Thread thread;

        private bool disposed;
        private StreamWriter current;
        private FileInfo file;

        public QueueingLogWriter(string path, long maxSize, int maxFiles, bool compress)
        {
            this.file = new FileInfo(path);
            this.path = path;
            this.maxSize = maxSize;
            this.maxFiles = maxFiles;
            this.compress = compress;
            this.name = Path.GetFileNameWithoutExtension(path);
            this.extention = Path.GetExtension(path);
            this.directory = Path.GetDirectoryName(path);

            this.current = SafeOpen(path);

            thread = new Thread(WriteLoop);
            thread.Start();
        }

        public void Write(string message)
        {
            if (disposed)
                return;

            lock (padLock)
            {
                logQueue.Enqueue(message);
                if (logQueue.Count > 32)
                {
                    Monitor.PulseAll(padLock);
                }
            }
        }

        private static StreamWriter SafeOpen(string path)
        {
            int count = 0;
            while (true)
            {
                try
                {
                    return new StreamWriter(path, true);
                }
                catch (Exception)
                {
                    if (count > 100)
                        throw;

                    if (count > 10)
                        Thread.Sleep(count * 10);

                    path = MorphPath(path, count++);
                }

            }
        }

        private static string MorphPath(string path, int retry)
        {
            string dir = Path.GetDirectoryName(path);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            return Path.Combine(dir, $"{fileName}-{retry:x}{ext}");
        }

        private void WriteLoop()
        {
            try
            {
                lock (padLock)
                {
                    while (true)
                    {
                        if (logQueue.Count < 1)
                            Monitor.Wait(padLock);
                        Flush(32);
                    }
                }
            }
            catch (ThreadAbortException)
            {
                Flush(logQueue.Count);
            }
            catch (Exception)
            {
                //TODO: Ignore for now, but we need an idea of how to deal with this.
            }
        }

        private StreamWriter NextWriter()
        {
            file.Refresh();
            if (file.Length <= maxSize) return current;

            if (current != null)
                current.Close();

            Archive();

            return current = SafeOpen(path);
        }

        private void Archive()
        {
            file.MoveTo(Path.Combine(directory, GenerateUniqueLogName()));
            file = new FileInfo(path);

            DirectoryInfo dir = new DirectoryInfo(directory);
            FileInfo[] logFiles = dir.GetFiles(name + "-*" + extention);
            if (logFiles.Length < maxFiles)
                return;

            if (compress)
            {
                string zipname = Path.Combine(directory, GenerateUniqueArchiveName());
                using (ZipArchive archive =  ZipFile.Open(zipname, ZipArchiveMode.Create))
                {
                    foreach (FileInfo f in logFiles)
                    {
                        try
                        {
                            archive.CreateEntryFromFile(f.FullName, f.Name);
                            f.Delete();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                    }
                }

                FileInfo[] zipfiles = dir.GetFiles("*.zip");
                if (zipfiles.Length > maxFiles)
                    DeleteOldest(zipfiles);
            }
            else
            {
                DeleteOldest(logFiles);
            }
        }

        private void DeleteOldest(FileInfo[] files)
        {
            try
            {
                FileInfo oldest = files.OrderByDescending(f => f.CreationTime).First();
                oldest.Delete();
            }
            catch (Exception)
            {
                //TODO: Report
            }
        }

        private string GenerateUniqueArchiveName()
        {
            return name + "-" + Guid.NewGuid().ToString("N") + ".zip";
        }

        private string GenerateUniqueLogName()
        {
            return name + "-" + Guid.NewGuid().ToString("N") + extention;
        }

        private void Flush(int count)
        {
            StreamWriter writer = NextWriter();
            while (logQueue.Count > 0 && count-- > 0)
            {
                writer.WriteLine(logQueue.Dequeue());
            }

            if (logQueue.Count > 0)
            {
                Flush(32);
                return;
            }

            writer.Flush();
        }

        public void Dispose()
        {
            disposed = true;
            thread.Abort();
            thread.Join();
        }

        public void Close()
        {
            Dispose();
        }
    }
}
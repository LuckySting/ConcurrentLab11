using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConcurrentLab1
{
    class TaskDetails
    {
        public int thread;
        public int task;
        public DateTime dtStart;
        public DateTime dtEnd;

        public TaskDetails(int thread, int task, DateTime dtStart, DateTime dtEnd)
        {
            this.thread = thread;
            this.task = task;
            this.dtStart = dtStart;
            this.dtEnd = dtEnd;
        }
    }

    static class ImageWorker
    {
        public static Bitmap load(String filePath)
        {
            Image image = Image.FromFile(filePath);
            Bitmap bitmap = new Bitmap(image);
            return bitmap;
        }

        public static Bitmap clip(Bitmap bitmap, int newWidth, int newHeight)
        {
            Bitmap resizedBitmap = new Bitmap(newWidth, newHeight);
            for (int x = 0; x < newWidth; x++)
            {
                for (int y = 0; y < newHeight; y++)
                {
                    if (x < bitmap.Width && y < bitmap.Height)
                    {
                        Color color = bitmap.GetPixel(x, y);
                        resizedBitmap.SetPixel(x, y, color);
                    }
                    else
                    {
                        resizedBitmap.SetPixel(x, y, Color.Black);
                    }
                }
            }

            return resizedBitmap;
        }

        public static Bitmap gray(Bitmap bitmap)
        {
            Bitmap newBitmap = new Bitmap(bitmap.Width, bitmap.Height);
            for (int x = 0; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Color color = bitmap.GetPixel(x, y);
                    byte newColor = (byte) (0.3 * color.R + 0.59 * color.G + 0.11 * color.B);
                    newBitmap.SetPixel(x, y, Color.FromArgb(newColor, newColor, newColor));
                }
            }

            return newBitmap;
        }

        public static void process(String path)
        {
            Bitmap bitmap = ImageWorker.load(path);
            bitmap = ImageWorker.gray(bitmap);
            bitmap = ImageWorker.clip(bitmap, 350, 250);
            string[] pathParts = path.Split('/');
            string[] pathToFileParts = pathParts.Take(pathParts.Count() - 2).ToArray();
            String pathToFile = String.Join('/', pathToFileParts);
            string[] filenameParts = pathParts.Last().Split('.');
            String name = String.Join('.', filenameParts.Take(filenameParts.Count() - 1).ToArray());
            String ext = filenameParts.Last();
            String newPath = pathToFile + "/computed/" + name + "_computed." + ext;
            bitmap.Save(newPath);
        }
    }

    class Program
    {
        static List<string> get_n_imgs(string folder_path, int n)
        {
            List<string> paths = new List<string>();
            int i = 0;
            foreach (var filepath in Directory.GetFiles(folder_path))
            {
                paths.Add(filepath);
                i++;
                if (i == n)
                {
                    break;
                }
            }

            return paths;
        }

        static double serial_process(int n)
        {
            var start_dt = DateTime.Now;
            foreach (var filepath in get_n_imgs("../../../source/", n))
            {
                ImageWorker.process(filepath);
            }

            var stop_dt = DateTime.Now;
            return (stop_dt - start_dt).TotalMilliseconds;
        }

        static double parallel_process(int n, int par_max_degree = -1)
        {
            var cts = new CancellationTokenSource();
            var token = cts.Token;
            Thread thr = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(100);
                    if (Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.Escape)
                    {
                        cts.Cancel();
                        break;
                    }

                    if (cts.IsCancellationRequested)
                    {
                        break;
                    }
                }
            });
            thr.Start();
            var start_dt = DateTime.Now;
            try
            {
                Parallel.ForEach(get_n_imgs("../../../source/", n), new ParallelOptions()
                    {
                        CancellationToken = token,
                        MaxDegreeOfParallelism = par_max_degree
                    },
                    ImageWorker.process);
                cts.Cancel();
            }
            catch (OperationCanceledException o)
            {
                Console.WriteLine("\nОтмена обработки!");
            }

            var stop_dt = DateTime.Now;
            return (stop_dt - start_dt).TotalMilliseconds;
        }

        static void test_serial_process()
        {
            Console.WriteLine(serial_process(25));
            Console.WriteLine(serial_process(50));
            Console.WriteLine(serial_process(100));
        }

        static void test_parallel_process()
        {
            Console.WriteLine(parallel_process(25));
            Console.WriteLine(parallel_process(50));
            Console.WriteLine(parallel_process(100));
        }

        static void test_bounded_parallel_process(int bound)
        {
            Console.WriteLine(parallel_process(25, bound));
            Console.WriteLine(parallel_process(50, bound));
            Console.WriteLine(parallel_process(100, bound));
        }
        
        static ConcurrentBag<TaskDetails> bag = new ConcurrentBag<TaskDetails>();
        static TaskDetails currentThread;
        
        static void test_standard(int n)
        {
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
            Parallel.ForEach(get_n_imgs("../../../source/", n),
                new ParallelOptions() { },
                line =>
                {
                    var start_dt = DateTime.Now;
                    ImageWorker.process(line);
                    var stop_dt = DateTime.Now;
                    bag.Add(new TaskDetails(Thread.CurrentThread.ManagedThreadId, Task.CurrentId.Value, start_dt, stop_dt));
                });
            Dictionary<int, int> dict = new Dictionary<int, int>();
            foreach (var td in bag)
            {
                if (dict.ContainsKey(td.thread))
                {
                    dict[td.thread]++;
                }
                else
                {
                    dict.Add(td.thread, 1);
                    Console.WriteLine(td.thread + " - " + td.dtStart.ToString("hh:mm:ss") + ",");
                }
            }
            foreach (var td in dict)
            {
                Console.WriteLine(td.Key + " - " + td.Value + ",");
            }
        }
        
        static void test_standard_splitting_data(int n)
        {
            var total_start_dt = DateTime.Now;
            Parallel.ForEach(get_n_imgs("../../../source/", n),
                new ParallelOptions() { },
                line =>
                {
                    var start_dt = DateTime.Now;
                    ImageWorker.process(line);
                    var stop_dt = DateTime.Now;
                    bag.Add(new TaskDetails(Thread.CurrentThread.ManagedThreadId, Task.CurrentId.Value, start_dt, stop_dt));
                });
            var total_stop_dt = DateTime.Now;
            List<int> threads = new List<int>();
            List<int> tasks = new List<int>();
            foreach (var td in bag)
            {
                if (!threads.Contains(td.thread))
                {
                    threads.Add(td.thread);
                }
                if (!threads.Contains(td.task))
                {
                    tasks.Add(td.task);
                }
            }
            Console.WriteLine((total_stop_dt - total_start_dt).TotalMilliseconds);
            Console.WriteLine(threads.Count);
            Console.WriteLine(tasks.Count);
        }
        
        static void test_static_splitting_data(int n)
        {
            var files = get_n_imgs("../../../source/", n);
            var PartedData = Partitioner.Create(0, 100);
            var total_start_dt = DateTime.Now;
            Parallel.ForEach(PartedData,
                new ParallelOptions() { },
                range =>
                {
                    var start_dt = DateTime.Now;
                    for(int i=range.Item1; i < range.Item2; i++)
                        ImageWorker.process(files[i]);
                    var stop_dt = DateTime.Now;
                    bag.Add(new TaskDetails(Thread.CurrentThread.ManagedThreadId, Task.CurrentId.Value, start_dt, stop_dt));
                });
            var total_stop_dt = DateTime.Now;
            List<int> threads = new List<int>();
            List<int> tasks = new List<int>();
            foreach (var td in bag)
            {
                if (!threads.Contains(td.thread))
                {
                    threads.Add(td.thread);
                }
                if (!threads.Contains(td.task))
                {
                    tasks.Add(td.task);
                }
            }
            Console.WriteLine((total_stop_dt - total_start_dt).TotalMilliseconds);
            Console.WriteLine(threads.Count);
            Console.WriteLine(tasks.Count);
        }
        
        static void test_balance_splitting_data(int n)
        {
            var PartedData = Partitioner.Create(get_n_imgs("../../../source/", n), true);
            var total_start_dt = DateTime.Now;
            Parallel.ForEach(PartedData,
                new ParallelOptions() { },
                line =>
                {
                    var start_dt = DateTime.Now;
                    ImageWorker.process(line);
                    var stop_dt = DateTime.Now;
                    bag.Add(new TaskDetails(Thread.CurrentThread.ManagedThreadId, Task.CurrentId.Value, start_dt, stop_dt));
                });
            var total_stop_dt = DateTime.Now;
            List<int> threads = new List<int>();
            List<int> tasks = new List<int>();
            foreach (var td in bag)
            {
                if (!threads.Contains(td.thread))
                {
                    threads.Add(td.thread);
                }
                if (!threads.Contains(td.task))
                {
                    tasks.Add(td.task);
                }
            }
            Console.WriteLine((total_stop_dt - total_start_dt).TotalMilliseconds);
            Console.WriteLine(threads.Count);
            Console.WriteLine(tasks.Count);
        }
        
        static void test_static_fixed_splitting_data(int n)
        {
            var files = get_n_imgs("../../../source/", n);
            var PartedData = Partitioner.Create(0, 100, 25);
            var total_start_dt = DateTime.Now;
            Parallel.ForEach(PartedData,
                new ParallelOptions() { },
                range =>
                {
                    var start_dt = DateTime.Now;
                    for(int i=range.Item1; i < range.Item2; i++)
                        ImageWorker.process(files[i]);
                    var stop_dt = DateTime.Now;
                    bag.Add(new TaskDetails(Thread.CurrentThread.ManagedThreadId, Task.CurrentId.Value, start_dt, stop_dt));
                });
            var total_stop_dt = DateTime.Now;
            List<int> threads = new List<int>();
            List<int> tasks = new List<int>();
            foreach (var td in bag)
            {
                if (!threads.Contains(td.thread))
                {
                    threads.Add(td.thread);
                }
                if (!threads.Contains(td.task))
                {
                    tasks.Add(td.task);
                }
            }
            Console.WriteLine((total_stop_dt - total_start_dt).TotalMilliseconds);
            Console.WriteLine(threads.Count);
            Console.WriteLine(tasks.Count);
        }

        static void Main(string[] args)
        {
            test_standard_splitting_data(100);
        }
    }
}
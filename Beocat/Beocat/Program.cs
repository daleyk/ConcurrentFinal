using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace Beocat
{
    class Program
    {
        static Stack<Tuple<int, int>> ranges = new Stack<Tuple<int, int>>();
        static Stack<int> metadataVals = new Stack<int>();
        static StringBuilder sb = new StringBuilder();
        static string[] chunk;
        static int[] bins = new int[100];
        static int binTime;
        static Mutex outputMutex = new Mutex();
        static void Main(string[] args)
        {
            string sr;
            List<Thread> threads = new List<Thread>();
            Console.Write("Please give a proper file path (i.e. C:/Users/e10d1/Downloads/2019-07-18.txt): ");
            string filename = Console.ReadLine();

            // Reads in text from file and puts it in to string 
            using (FileStream f = File.OpenRead(filename))
            {
                byte[] array = new byte[12000];
                StringBuilder sb = new StringBuilder();
                UTF8Encoding temp = new UTF8Encoding(true);
                while (f.Read(array, 0, 12000) > 0)
                {
                    sb.Append(temp.GetString(array));
                }
                sr = sb.ToString();
            }

            // Divides the text into individual strings associated with each line break
            chunk = sr.Split('\n');

            // Find the start and stop indexes for the data as well as the index values for the metadata
            int start = -1;
            for (int i = 0; i < chunk.Length; i++)
            {
                switch (chunk[i])
                {
                    case "C:\r":
                        start = i + 1;
                        break;
                    case "F:\r":
                        ranges.Push(new Tuple<int, int>(start, i - 1));
                        break;
                    default:
                        break;
                }
                if (chunk[i].Contains("BOX:"))
                    metadataVals.Push(i);
            }

            Console.Write("Select a bin size in minutes (0 = no binning): ");
            binTime = Convert.ToInt32(Console.ReadLine());

            Thread thread;
            // While there is still work to do
            while (ranges.Count > 0)
            {
                thread = new Thread(Method);
                thread.Start(new Tuple<Tuple<int, int>, int>(ranges.Pop(), metadataVals.Pop()));
                threads.Add(thread);
            }

            // Wait for all threads to finish
            foreach (Thread t in threads)
            {
                t.Join();
            }
            
            // Output the data to console and to a test file
            using (System.IO.StreamWriter writer = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\results.csv"))
            {
                Console.WriteLine(sb.ToString());
                writer.WriteLine(sb.ToString());
            }

            using (System.IO.StreamWriter writer = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\results.txt"))
            {
                if (binTime > 0)
                {
                    for (int i = 0; i < bins.Length; i++)
                    {
                        if (bins[i] > 0)
                        {
                            Console.WriteLine("{0} minutes: {1} lever presses", (i + 1) * binTime, bins[i]);
                            writer.WriteLine("{0} minutes: {1} lever presses", (i + 1) * binTime, bins[i]);
                        }
                    }
                }
            }


            Console.WriteLine("Results were written to \"My Documents.\"\n\tPress Enter to exist.");
            Console.Read();
        }

        static void Method(object obj)
        {
            Tuple<Tuple<int, int>, int> tuple = (Tuple<Tuple<int, int>, int>)obj;
            Tuple<int, int> range = tuple.Item1;
            int metadataIndex = tuple.Item2;
            int index, start, end;
            char[] metadata;
            double[] data;
            data = new double[1000];
            index = 0;
            start = range.Item1;
            end = range.Item2;

            // Convert the text to a double and place it in a data array
            for (int i = start; i <= end; i++)
            {
                string[] tempString = chunk[i].Split(' ');
                for (int j = 6; j < tempString.Length; j++)
                {
                    if (tempString[j] != "")
                    {
                        data[index] = Convert.ToDouble(tempString[j]);
                        index++;
                    }
                }
            }

            // Convert from time-between form to elapsed-time form
            for (int i = 1; i < index; i++)
                data[i] += data[i - 1];

            // Fix the metadata by removing carriage return
            metadata = (chunk[metadataIndex] + " " + chunk[metadataIndex + 1]).ToCharArray();
            for (int i = 0; i < metadata.Length; i++)
            {
                if (metadata[i] == '\r')
                    metadata[i] = ' ';
            }

            // Build a string for one-time output to console and file
            outputMutex.WaitOne();
            lock (sb)
            {
                sb.Append(metadata);
                for (int k = 0; k < index; k++)
                    sb.Append("," + data[k]);
                sb.Append("\n");
            }

            if (binTime > 0)
            {
                int binIndex = 0;
                for (int i = 0; i < index; i++)
                {
                    if (!(data[i] <= (binIndex + 1) * binTime * 60))
                        binIndex++;
                    bins[binIndex]++;
                }
            }
            outputMutex.ReleaseMutex();
        }
    }
}

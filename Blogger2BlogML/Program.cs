using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Reflection;

namespace Blogger2BlogML
{
    class Program
    {
        static void Main(string[] args)
        {
            // print banner
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);
            Console.WriteLine(Properties.Resources.Program_Banner, fvi.ProductMajorPart, fvi.ProductMinorPart, fvi.LegalCopyright);
            Console.WriteLine();

            if (args.Length == 2)
            {
                try
                {
                    BlogConverter converter = new BlogConverter(args[0], args[1]);
                    converter.ConverterMessage += new EventHandler<ConverterMessageEventArgs>(converter_ConverterMessage);
                    converter.Convert();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(Properties.Resources.Program_Exception, ex);
                    Console.WriteLine();
                    Console.WriteLine(Properties.Resources.Program_ReportError);

                    Environment.ExitCode = 1;
                }
            }
            else
            {
                Console.WriteLine(Properties.Resources.Program_Usage);
                Environment.ExitCode = 2;
            }
#if DEBUG
            Console.ReadKey();
#endif
            Console.WriteLine();
        }

        static void converter_ConverterMessage(object sender, ConverterMessageEventArgs e)
        {
            Console.WriteLine(e.Message);
        }
    }
}

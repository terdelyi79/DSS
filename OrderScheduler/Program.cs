using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderScheduler
{
    class Program
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            logger.Info("Order scheduler is started");

            if (args.Length != 3)
                throw new ApplicationException("Invalid number of command line arguments");

            try
            {
                OrderScheduler orderScheduler = new OrderScheduler(args[0]);
                orderScheduler.Schedule();

                File.WriteAllText(args[1], orderScheduler.ExportOrderSchedule(), Encoding.UTF8);
                File.WriteAllText(args[2], orderScheduler.ExportWorkSchedule(), Encoding.UTF8);

                logger.Info("Order scheduler successfully finished");
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                Console.WriteLine(ex.Message);
                Console.WriteLine("Please read OrderScheduler.log for more details");
            }
        }
    }
}

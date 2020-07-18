using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderScheduler
{
    public class OrderScheduler
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(Program));

        private List<Order> orders;
        private List<Penalty> penalties = null;
        private List<Resource>[] schedule = null;

        public OrderScheduler(string inputPath)
        {
            try
            {
                logger.Info("Reading orders from the input file");
                this.orders = File.ReadAllLines(inputPath).Skip(1).Select(line => Order.Parse(line)).ToList();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Unable to read input file", ex);
            }
        }

        /// <summary>
        /// Schedule order by minimazing penalties
        /// </summary>
        public void Schedule()
        {
            try
            {
                logger.Info("Starting the scheduling algorithm");

                List<Order> orders = new List<Order>(this.orders);

                // Calculate the difference between deadline and date when order would be finished when it would be alone for each order, and sort them
                Dictionary<Order, TimeSpan> aloneScheduleDate = new Dictionary<Order, TimeSpan>();
                foreach (Order order in orders)
                    aloneScheduleDate.Add(order, order.Deadline - this.CalculatePenalties(new List<Order>() { order }, out _)[0].FinishDate);
                orders.Sort((order1, order2) => TimeSpan.Compare(aloneScheduleDate[order2], aloneScheduleDate[order1]));

                // Current schedule
                List<Resource>[] schedule = null;
                // Penalties for the current shcedule
                List<Penalty> penalties = null;
                // Total penalty for current schedule
                int currentPenalty = int.MaxValue;
                // Number of orders we decided to not move
                int fixedOrderCount = 0;

                // While it's possible to find better solution by changing two neighbour orders in the schedule
                while (fixedOrderCount < orders.Count - 1)
                {
                    // Calculate penalties for current schedule
                    penalties = this.CalculatePenalties(orders, out schedule);

                    // Claculate total penalty and find order with highest penalty
                    int index = 0, maxPenalty = 0, totalPenalty = 0;
                    foreach (Penalty penalty in penalties)
                    {
                        if (penalty.Amount > maxPenalty)
                            maxPenalty = penalty.Amount;
                        totalPenalty += penalty.Amount;
                        index++;
                    }

                    // If change leads to a better solution
                    if (totalPenalty < currentPenalty)
                    {
                        currentPenalty = totalPenalty;
                        fixedOrderCount = 0;
                        this.penalties = penalties;
                        this.schedule = schedule;
                        this.orders = new List<Order>(orders);
                    }
                    // If change can't provide a better solution
                    else
                    {
                        // Cobtinue with smaller penalties
                        fixedOrderCount++;
                    }

                    // Order penalties by descending order
                    penalties.Sort((p1, p2) => { if (p1.Amount < p2.Amount) return -1; if (p1.Amount == p2.Amount) return 0; else return 1; });
                    penalties.Reverse();

                    Penalty penaltyToChange = penalties[fixedOrderCount];

                    if (penaltyToChange.OrderIndex != 0)
                    {
                        Penalty previousPenalty = penalties.Where(p => p.OrderIndex == penaltyToChange.OrderIndex - 1).FirstOrDefault();
                        if (previousPenalty.Amount < penaltyToChange.Amount)
                        {
                            // Change penlty with previous one
                            Order order = orders[previousPenalty.OrderIndex];
                            orders[previousPenalty.OrderIndex] = orders[penaltyToChange.OrderIndex];
                            orders[penaltyToChange.OrderIndex] = order;
                        }
                    }
                }

                logger.Info("Scheduling successfully finished");
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error occured during the scheduling process", ex);
            }
        }

        /// <summary>
        /// Calculate penalties for current order
        /// </summary>
        /// <returns></returns>
        private List<Penalty> CalculatePenalties(List<Order> orders, out List<Resource>[] schedule)
        {
            List<Penalty> penalties = new List<Penalty>();

            // Initialize schedule
            schedule = new List<Resource>[6];
            for (int step = 0; step < 6; step++)
            {
                schedule[step] = new List<Resource>();
                for (int i = 0; i < Resources.AvailableResources[step]; i++)
                    schedule[step].Add(new Resource());
            }

            int orderIndex = 0;

            // For each order
            foreach (Order order in orders)
            {
                // End of the previous step in minutes
                int endOfPrevStep = 0;

                // For each piece for the order
                for (int piece = 0; piece < order.Quantity; piece++)
                {
                    endOfPrevStep = 0;

                    // For each step
                    for (int step = 0; step < 6; step++)
                    {
                        int nextStart = int.MaxValue;
                        Resource nextResource = null;

                        // Find the first available minute to process current step for current piece
                        foreach (Resource resource in schedule[step])
                        {
                            int lastEnd = 0;
                            if (resource.LastScheduledInterval != null)
                                lastEnd = resource.LastScheduledInterval.End;
                            if (lastEnd < nextStart)
                            {
                                nextStart = lastEnd;
                                nextResource = resource;
                            }
                        }

                        // We can't start the step while previous step for the same instance is not finished
                        if (nextStart < endOfPrevStep)
                            nextStart = endOfPrevStep;

                        // Add the time needed for the current step
                        endOfPrevStep = nextStart + Resources.NeededResources[order.Product][step];

                        // If the last interval for current resource is already for the same order, then extend the interval
                        if ((nextResource.LastScheduledInterval != null) && (nextResource.LastScheduledInterval.Order == order))
                            nextResource.LastScheduledInterval.End = endOfPrevStep;
                        // Add a new interval otherwise
                        else
                        {
                            ScheduledInterval newInterval = new ScheduledInterval() { Order = order, Start = nextStart, End = endOfPrevStep };
                            nextResource.ScheduledIntervals.Add(newInterval);
                            nextResource.LastScheduledInterval = newInterval;
                        }
                    }
                }

                // Calculate the finish date for current order
                DateTime finishDate = MinutesToDateTime(endOfPrevStep, false);

                // Add penalty of current order to the results
                Penalty penalty = new Penalty() { Amount = (int)Math.Ceiling((finishDate - order.Deadline).TotalDays) * order.PenaltyPerDay, FinishDate = finishDate, OrderIndex = orderIndex++ };
                if (penalty.Amount < 0)
                    penalty.Amount = 0;
                penalties.Add(penalty);
            }

            return penalties;
        }

        public string ExportOrderSchedule()
        {
            try
            {
                logger.Info("Exporting the order schedule");

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Megrendelésszám,Profit összesen,Levont kötbér,Munka megkezdése,Készre jelentés ideje,Megrendelés eredeti határideje");

                int orderIndex = 0;
                foreach (Order order in this.orders)
                {
                    int startMinute = int.MaxValue;
                    int endMinute = 0;
                    for (int step = 0; step < 6; step++)
                    {
                        foreach (Resource stepResource in schedule[step])
                        {
                            foreach (ScheduledInterval interval in stepResource.ScheduledIntervals)
                            {
                                if (interval.Order == order)
                                {
                                    if (interval.Start < startMinute)
                                        startMinute = interval.Start;
                                    if (interval.End > endMinute)
                                        endMinute = interval.End;
                                }
                            }
                        }
                    }

                    Penalty penalty = penalties.Where(p => p.OrderIndex == orderIndex).FirstOrDefault();

                    sb.Append($"{order.Id},");
                    sb.Append($"{order.Quantity * order.ProfitPerPiece - penalty.Amount} Ft,");
                    sb.Append($"{penalty.Amount} Ft,");
                    sb.Append($"{this.MinutesToDateTime(startMinute, true).ToString("MM.dd HH:mm")},");
                    sb.Append($"{this.MinutesToDateTime(endMinute, false).ToString("MM.dd HH:mm")},");
                    sb.Append($"{order.Deadline.ToString("MM.dd HH:mm")}");
                    sb.AppendLine();

                    orderIndex++;
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occured while exporting the order schedule", ex);
            }
        }

        public string ExportWorkSchedule()
        {
            try
            {
                logger.Info("Exporting the work schedule");

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Dátum,Gép,Kezdő időpont,Záró időpont,Megrendelésszám");

                int endOfDayMinutes = 0;
                bool intervalFound = true;
                while (intervalFound)
                {
                    intervalFound = false;
                    endOfDayMinutes += 16 * 60;
                    for (int step = 0; step < 6; step++)
                    {
                        List<Resource> stepResources = this.schedule[step];
                        for (int resourceIndex = 0; resourceIndex < stepResources.Count; resourceIndex++)
                        {
                            while (true)
                            {
                                ScheduledInterval interval = stepResources[resourceIndex].ScheduledIntervals.FirstOrDefault();
                                if (interval == null)
                                    break;
                                intervalFound = true;
                                if (interval.Start >= endOfDayMinutes)
                                    break;
                                if (interval.End <= endOfDayMinutes)
                                {
                                    AddInterval(sb, interval, step, resourceIndex);
                                    stepResources[resourceIndex].ScheduledIntervals.RemoveAt(0);
                                }
                                else
                                {
                                    ScheduledInterval dayInterval = new ScheduledInterval() { Order = interval.Order, Start = interval.Start, End = endOfDayMinutes };
                                    AddInterval(sb, dayInterval, step, resourceIndex);
                                    interval.Start = endOfDayMinutes;
                                }
                            }

                        }
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occured while exporting the work schedule", ex);
            }
        }

        private void AddInterval(StringBuilder sb, ScheduledInterval interval, int step, int resourceIndex)
        {
            DateTime startDate = MinutesToDateTime(interval.Start, true);
            DateTime endDate = MinutesToDateTime(interval.End, false);
            sb.AppendLine($"{startDate.ToString("yyyy.MM.dd.")},{Resources.Names[step]}-{resourceIndex + 1},{startDate.ToString("HH:mm")},{endDate.ToString("HH:mm")},{interval.Order.Id}");
        }

        /// <summary>
        /// Convert total number of minutes to a  date
        /// </summary>
        /// <param name="minutes"></param>
        /// <returns></returns>
        private DateTime MinutesToDateTime(int resourceminutes, bool isStartDate)
        {
            int h = resourceminutes / 60;
            int minutes = resourceminutes % 60;
            int days = h / 16;
            int hours = h % 16 + 6;
            if ((!isStartDate) && (resourceminutes != 0) && (hours == 6) && (minutes == 0))
            {
                days--;
                hours = 22;
            }
            return new DateTime(2020, 7, 20, 0, 0, 0).AddDays(days).AddHours(hours).AddMinutes(minutes);
        }

        private class Penalty
        {
            public int Amount { get; set; }

            public int OrderIndex { get; set; }

            public DateTime FinishDate { get; set; }
        }

        private class Resource
        {
            public Resource()
            {
                this.ScheduledIntervals = new List<ScheduledInterval>();
            }

            public List<ScheduledInterval> ScheduledIntervals { get; set; }

            /// <summary>
            /// Last interval for the resource (needed for performance tuning only)
            /// </summary>
            public ScheduledInterval LastScheduledInterval { get; set; }
        }

        private class ScheduledInterval
        {
            public int Start { get; set; }

            public int End { get; set; }

            public Order Order { get; set; }
        }
    }
}

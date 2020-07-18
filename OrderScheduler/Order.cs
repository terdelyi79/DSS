using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderScheduler
{
    public class Order
    {
        public string Id { get; set; }

        public Product Product { get; set; }

        public int Quantity { get; set; }

        public DateTime Deadline { get; set; }

        public int ProfitPerPiece { get; set; }

        public int PenaltyPerDay { get; set; }

        public static Order Parse(string s)
        {
            string[] tokens = s.Split(',').Select(token => token.Trim()).ToArray();
            return new Order()
            {
                Id = tokens[0],
                Product = (Product) Enum.Parse(typeof(Product), tokens[1]),
                Quantity = int.Parse(tokens[2].Replace(" ", "")),
                Deadline = DateTime.Parse($"2020.{tokens[3]}"),
                ProfitPerPiece = int.Parse(tokens[4].Replace(" ", "")),
                PenaltyPerDay = int.Parse(tokens[5].Replace(" ", ""))
            };
        }

        public List<int> TotalNeededResources
        {
            get
            {
                List<int> totalNeededResources = new List<int>();
                for (int i = 0; i < 6; i++)
                    totalNeededResources.Add(Resources.NeededResources[this.Product][i] * this.Quantity);
                return totalNeededResources;
            }
        }
    }

    public enum Product { GYB, FB, SB }
}

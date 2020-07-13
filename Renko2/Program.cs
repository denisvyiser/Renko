using System;
using System.Collections.Generic;

namespace Renko2
{

    class Program
    {
        public static int digits { get; set; } = 5;
        public static double points { get; set; } = 0.00001;

        public static double pip_size { get; set; } = 0.0001;

        public static double renko_size { get; set; } = 20.0;

        public static double tick_size { get; set; } = 0.00001;

        public static double brick_size { get; set; }

        public static long volumes { get; set; }

        public static double up_wick { get; set; }

        public static double down_wick { get; set; }

        public static long tick_volumes { get; set; }

        public static ENUM_RENKO_TYPE renko_type { get; set; }

        static List<MqlRates> renko_buffer = new List<MqlRates>();
        static void Main(string[] args)
        {

            var dados = MqlRates.GetDados();

            SetupType();

            for (int i = 0; i < dados.Count; i++)
            {

                int size = LoadPriceOHLC(dados[i]);
            }

            for(int i=0; i< renko_buffer.Count; i++)
            {
                Console.WriteLine("time: {0}, open: {1}, high: {2}, low: {3}, close: {4}, tick_volume: {5}, spread: {6}, real_volume: {7}, Direction: {8}",
                    renko_buffer[i].time, renko_buffer[i].open, renko_buffer[i].high, renko_buffer[i].low, renko_buffer[i].close, renko_buffer[i].tick_volume, renko_buffer[i].spread, renko_buffer[i].real_volume, (renko_buffer[i].open < renko_buffer[i].close ? "Up" : "Down"));
            }

            Console.Read();
        }

        public static int LoadPriceOHLC(MqlRates price)
        {
            LoadPrice(price.open, price.time, 0, 0, price.spread);
            if (price.close > price.open)
            {
                LoadPrice(price.low, price.time, 0, 0, price.spread);
                LoadPrice(price.high, price.time, 0, 0, price.spread);
            }
            else
            {
                LoadPrice(price.high, price.time, 0, 0, price.spread);
                LoadPrice(price.low, price.time, 0, 0, price.spread);
            }
            return LoadPrice(price.close, price.time, price.tick_volume, price.real_volume, price.spread);
        }

        public static int CloseUp(double points, DateTime time, int spread = 0)
        {
            int index = renko_buffer.Count - 1;
            //OHLC
            renko_buffer[index].open = renko_buffer[index - 1].close + points - brick_size;
            renko_buffer[index].high = renko_buffer[index - 1].close + points;
            renko_buffer[index].close = renko_buffer[index - 1].close + points;
            //Wicks
            renko_buffer[index].low = down_wick;
            //else renko_buffer[index].low = renko_buffer[index].open;
            up_wick = down_wick = renko_buffer[index].close;
            //Volumes
            renko_buffer[index].tick_volume = tick_volumes;
            renko_buffer[index].real_volume = volumes;
            renko_buffer[index].spread = spread;
            tick_volumes = volumes = 0;
            //Add one
            return AddOne(time);
        }

        public static int CloseDown(double points, DateTime time, int spread = 0)
        {
            int index = renko_buffer.Count - 1;
            //OHLC
            //if (asymetric_reversal) renko_buffer[index].open = renko_buffer[index - 1].close;
            renko_buffer[index].open = renko_buffer[index - 1].close - points + brick_size;
            renko_buffer[index].low = renko_buffer[index - 1].close - points;
            renko_buffer[index].close = renko_buffer[index - 1].close - points;
            //Wicks
            renko_buffer[index].high = up_wick;
            //else renko_buffer[index].high = renko_buffer[index].open;
            up_wick = down_wick = renko_buffer[index].close;
            //Volumes
            renko_buffer[index].tick_volume = tick_volumes;
            renko_buffer[index].real_volume = volumes;
            renko_buffer[index].spread = spread;
            tick_volumes = volumes = 0;
            //Add one
            return AddOne(time);
        }


        public static int AddOne(DateTime time)
        {
            DateTime timeRenko;
            //Resize buffers
            int index = renko_buffer.Count;
            if (index <= 0) return 0;
            //Time
            if (time == null) time = DateTime.Now;
            //if (!open_time) time = time - time % 86400;
            if (time <= renko_buffer[index - 1].time)
                timeRenko = renko_buffer[index - 1].time.AddSeconds(60);
            else
                timeRenko = time;
            //Defaults         
            renko_buffer.Add(new MqlRates(timeRenko, 
                renko_buffer[index - 1].close, 
                renko_buffer[index - 1].close, 
                renko_buffer[index - 1].close, 
                renko_buffer[index - 1].close,
                0,
                0,
                0));
           
            return renko_buffer.Count -1;
        }

        static DateTime last_time;
        static long last_tick_volume, last_volume;
        public static int LoadPrice(double price, DateTime time, long tick_volume = 0, long volume = 0, int spread = 0)
        {            
            //Time
            if (time == null) time = DateTime.Now;
            //Buffer size
            int size = renko_buffer.Count;
            int index = size - 1;
            //First bricks
            if (size == 0)
            {
                //1st Buffers

                var RenkoPrice = Math.Round(Math.Floor(price / brick_size) * brick_size, digits);

                renko_buffer.Add(new MqlRates(
                    time.AddSeconds(-120),
                    (RenkoPrice - brick_size) - brick_size,
                    RenkoPrice - brick_size,
                    (RenkoPrice - brick_size) - brick_size,
                    RenkoPrice - brick_size,
                    0,
                    0,
                    0
                    ));

                renko_buffer.Add(new MqlRates(
                   time.AddSeconds(-60),
                  RenkoPrice - brick_size,
                  RenkoPrice,
                  RenkoPrice - brick_size,
                  RenkoPrice,
                  0,
                  0,
                  0));
                                
                //Current Buffer
                index = AddOne(time);
            }
            //Time change
            if (time != last_time)
            {
                last_time = time;
                tick_volumes += last_tick_volume;
                volumes += last_volume;
            }
            //Volume change
            last_tick_volume = tick_volume;
            last_volume = volume;
            //Wicks
            up_wick = Math.Max(up_wick, price);
            down_wick = Math.Min(down_wick, price);
            if (down_wick <= 0) down_wick = price;
            //Price change
            if (renko_type == ENUM_RENKO_TYPE.RENKO_TYPE_R)
            {
                //Up
                if (renko_buffer[index - 1].close > renko_buffer[index - 2].close)
                {
                    if (price > renko_buffer[index - 1].close + brick_size)
                    {
                        for (; price > renko_buffer[index - 1].close + brick_size;)
                            index = CloseUp(brick_size, time, spread);
                    }
                    //Reversal
                    else if (price < renko_buffer[index - 1].close - brick_size * 2.0)
                    {
                        index = CloseDown(brick_size * 2.0, time, spread);
                        for (; price < renko_buffer[index - 1].close - brick_size;)
                            index = CloseDown(brick_size, time, spread);
                    }
                }
                //Down
                if (renko_buffer[index - 1].close < renko_buffer[index - 2].close)
                {
                    if (price < renko_buffer[index - 1].close - brick_size)
                    {
                        for (; price < renko_buffer[index - 1].close - brick_size;)
                            index = CloseDown(brick_size, time, spread);
                    }
                    //Reversal
                    else if (price > renko_buffer[index - 1].close + brick_size * 2.0)
                    {
                        index = CloseUp(brick_size * 2.0, time, spread);
                        for (; price > renko_buffer[index - 1].close + brick_size;)
                            index = CloseUp(brick_size, time, spread);
                    }
                }
            }
            else
            {
                //Up
                if (renko_buffer[index - 1].close >= renko_buffer[index - 2].close)
                {
                    if (price >= renko_buffer[index - 1].close + brick_size)
                    {
                        for (; price >= renko_buffer[index - 1].close + brick_size;)
                            index = CloseUp(brick_size, time, spread);
                    }
                    //Reversal
                    else if (price <= renko_buffer[index - 1].close - brick_size * 2.0)
                    {
                        index = CloseDown(brick_size * 2.0, time, spread);
                        for (; price <= renko_buffer[index - 1].close - brick_size;)
                            index = CloseDown(brick_size, time, spread);
                    }
                }
                //Down
                if (renko_buffer[index - 1].close <= renko_buffer[index - 2].close)
                {
                    if (price <= renko_buffer[index - 1].close - brick_size)
                    {
                        for (; price <= renko_buffer[index - 1].close - brick_size;)
                            index = CloseDown(brick_size, time, spread);
                    }
                    //Reversal
                    else if (price >= renko_buffer[index - 1].close + brick_size * 2.0)
                    {
                        index = CloseUp(brick_size * 2.0, time, spread);
                        for (; price >= renko_buffer[index - 1].close + brick_size;)
                            index = CloseUp(brick_size, time, spread);
                    }
                }
            }
            //Current buffer
            renko_buffer[index].open = renko_buffer[index - 1].close;
            renko_buffer[index].high = up_wick;
            renko_buffer[index].low = down_wick;
            renko_buffer[index].close = price;
            renko_buffer[index].tick_volume = tick_volumes + tick_volume;
            renko_buffer[index].real_volume = volumes + volume;
            renko_buffer[index].spread = spread;

            return index + 1;
        }

        public static bool SetupType(ENUM_RENKO_TYPE type = ENUM_RENKO_TYPE.RENKO_TYPE_TICKS)
        {
            if (type == ENUM_RENKO_TYPE.RENKO_TYPE_TICKS) brick_size = renko_size * tick_size;
            else if (type == ENUM_RENKO_TYPE.RENKO_TYPE_PIPS) brick_size = renko_size * pip_size;
            else if (type == ENUM_RENKO_TYPE.RENKO_TYPE_R) brick_size = renko_size * tick_size - tick_size;
            else brick_size = renko_size;

            renko_type = type;

            brick_size = Math.Round(brick_size, 4);

            return true;
        }

    }

    enum ENUM_RENKO_TYPE
    {
        RENKO_TYPE_TICKS, //Ticks
        RENKO_TYPE_PIPS,  //Pips
        RENKO_TYPE_POINTS,//Points
        RENKO_TYPE_R      //R Type (-1 Tick)
    };
    public class MqlRates
    {
        public MqlRates(DateTime time, double open, double high, double low, double close, long tick_volume, int spread, long real_volume)
        {
            this.time = time;
            this.open = open;
            this.high = high;
            this.low = low;
            this.close = close;
            this.tick_volume = tick_volume;
            this.spread = spread;
            this.real_volume = real_volume;
        }

        public DateTime time { get; set; }
        public double open { get; set; }

        public double high { get; set; }
        public double low { get; set; }
        public double close { get; set; }
        public long tick_volume { get; set; }
        public int spread { get; set; }
        public long real_volume { get; set; }

        public static List<MqlRates> GetDados()
        {
            List<MqlRates> list = new List<MqlRates>();

            list.Add(new MqlRates(DateTime.Parse("2020.04.06 11:59"), 1.0791299999999999, 1.07934, 1.07894, 1.0792999999999999, 159, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:00"), 1.0792999999999999, 1.0796300000000001, 1.0792600000000001, 1.0794699999999999, 126, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:01"), 1.0794999999999999, 1.07962, 1.0794299999999999, 1.0794600000000001, 114, 3, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:02"), 1.0794600000000001, 1.07969, 1.079, 1.0792999999999999, 144, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:03"), 1.0792999999999999, 1.07955, 1.0790500000000001, 1.0792900000000001, 136, 2, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:04"), 1.0792900000000001, 1.0797699999999999, 1.0792900000000001, 1.07972, 88, 3, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:05"), 1.07972, 1.0800000000000001, 1.0796000000000001, 1.0799300000000001, 123, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:06"), 1.0799300000000001, 1.07999, 1.07955, 1.0797300000000001, 120, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:07"), 1.0797300000000001, 1.07978, 1.07941, 1.0796300000000001, 101, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:08"), 1.07961, 1.07999, 1.07961, 1.0799099999999999, 134, 2, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:09"), 1.0799099999999999, 1.0801099999999999, 1.07968, 1.0797099999999999, 120, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:10"), 1.0797099999999999, 1.0799099999999999, 1.0796600000000001, 1.07969, 83, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:11"), 1.07969, 1.0797399999999999, 1.0795600000000001, 1.07959, 90, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:12"), 1.07958, 1.07988, 1.0795600000000001, 1.0797399999999999, 80, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:13"), 1.0797600000000001, 1.0799099999999999, 1.0796600000000001, 1.0798399999999999, 79, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:14"), 1.0798399999999999, 1.07995, 1.07975, 1.0799000000000001, 73, 3, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:15"), 1.0799000000000001, 1.08003, 1.07982, 1.0800000000000001, 82, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:16"), 1.0800000000000001, 1.0801099999999999, 1.0799300000000001, 1.0800799999999999, 69, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:17"), 1.0800799999999999, 1.08013, 1.0799700000000001, 1.08009, 57, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:18"), 1.08009, 1.08023, 1.0799799999999999, 1.08013, 78, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:19"), 1.08013, 1.0801400000000001, 1.0799700000000001, 1.0799700000000001, 83, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:20"), 1.0799700000000001, 1.0800099999999999, 1.0799000000000001, 1.0799300000000001, 69, 2, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:21"), 1.0799300000000001, 1.07999, 1.0799000000000001, 1.0799099999999999, 46, 2, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:22"), 1.0799099999999999, 1.0799300000000001, 1.07965, 1.0796999999999999, 87, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:23"), 1.0797600000000001, 1.08003, 1.0796000000000001, 1.0799799999999999, 71, 2, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:24"), 1.0799799999999999, 1.08022, 1.0799300000000001, 1.0801000000000001, 69, 2, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:25"), 1.0801099999999999, 1.08036, 1.08005, 1.0802700000000001, 60, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:26"), 1.0802799999999999, 1.0803199999999999, 1.08009, 1.0801099999999999, 90, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:27"), 1.0801099999999999, 1.0801099999999999, 1.07985, 1.0799099999999999, 88, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:28"), 1.0799099999999999, 1.0799099999999999, 1.0796699999999999, 1.07968, 71, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:29"), 1.0796999999999999, 1.0798700000000001, 1.0796699999999999, 1.0798300000000001, 66, 3, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:30"), 1.0798300000000001, 1.0799799999999999, 1.07979, 1.0799099999999999, 71, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:31"), 1.0799099999999999, 1.0800799999999999, 1.07982, 1.07992, 66, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:32"), 1.07992, 1.0800799999999999, 1.07979, 1.07986, 93, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:33"), 1.07986, 1.0801799999999999, 1.0797300000000001, 1.0799799999999999, 117, 2, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:34"), 1.0799799999999999, 1.08023, 1.0799099999999999, 1.0801000000000001, 69, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:35"), 1.0801099999999999, 1.08013, 1.0797600000000001, 1.07992, 93, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:36"), 1.0799099999999999, 1.0802400000000001, 1.07989, 1.08016, 72, 2, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:37"), 1.08016, 1.0804, 1.0800700000000001, 1.0800700000000001, 72, 2, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:38"), 1.0800700000000001, 1.0801799999999999, 1.0799300000000001, 1.0799700000000001, 72, 2, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:39"), 1.0799799999999999, 1.0805400000000001, 1.0799700000000001, 1.08036, 127, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:40"), 1.0803499999999999, 1.08049, 1.0801099999999999, 1.0802499999999999, 114, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:41"), 1.0802499999999999, 1.0806899999999999, 1.08022, 1.0805800000000001, 88, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:42"), 1.0805800000000001, 1.08081, 1.0805800000000001, 1.08066, 92, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:43"), 1.08066, 1.0807800000000001, 1.0805499999999999, 1.08077, 87, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:44"), 1.08077, 1.0807899999999999, 1.0806100000000001, 1.0807899999999999, 52, 3, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:45"), 1.0807899999999999, 1.0808599999999999, 1.08066, 1.0807, 100, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:46"), 1.0807100000000001, 1.0807899999999999, 1.0805100000000001, 1.0805100000000001, 52, 4, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:47"), 1.0805100000000001, 1.0806800000000001, 1.0804, 1.0804499999999999, 86, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:48"), 1.0804199999999999, 1.0805800000000001, 1.0804199999999999, 1.08057, 77, 2, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:49"), 1.0805800000000001, 1.0805800000000001, 1.0804499999999999, 1.0805199999999999, 40, 3, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:50"), 1.0805199999999999, 1.08083, 1.0805199999999999, 1.0808200000000001, 69, 3, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:51"), 1.0808200000000001, 1.0809, 1.0808, 1.0808599999999999, 53, 2, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:52"), 1.08087, 1.0809, 1.08066, 1.0808899999999999, 49, 3, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:53"), 1.0808899999999999, 1.0810999999999999, 1.08087, 1.08097, 83, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:54"), 1.08097, 1.0810200000000001, 1.0807599999999999, 1.08101, 86, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:55"), 1.0809800000000001, 1.0810200000000001, 1.0807100000000001, 1.08074, 70, 2, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:56"), 1.08074, 1.0807800000000001, 1.0806499999999999, 1.0807800000000001, 44, 3, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:57"), 1.08077, 1.08107, 1.08073, 1.081, 99, 0, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:58"), 1.08101, 1.0810999999999999, 1.08094, 1.0810999999999999, 74, 3, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 12:59"), 1.0810999999999999, 1.08128, 1.08104, 1.0811599999999999, 85, 1, 0));
            list.Add(new MqlRates(DateTime.Parse("2020.04.06 13:00"), 1.0811500000000001, 1.08142, 1.0811500000000001, 1.08142, 68, 1, 0));

            return list;
        }

    }
}

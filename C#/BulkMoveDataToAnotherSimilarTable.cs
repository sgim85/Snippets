using System;
using System.Configuration;
using System.Data.SqlClient;

namespace DBMigration
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
                stopWatch.Start();

                var qa = ConfigurationManager.ConnectionStrings["P1"].ConnectionString;
                var preprod = ConfigurationManager.ConnectionStrings["P2"].ConnectionString;
                using (SqlConnection conn1 = new SqlConnection(qa))
                {
                    conn1.Open();

                    using (SqlCommand command = new SqlCommand("select * from Table1", conn1))
                    {
                        command.CommandTimeout = 7200;

                        using (SqlDataReader rdr = command.ExecuteReader())
                        {
                            using (SqlBulkCopy sbc = new SqlBulkCopy(preprod))
                            {
                                sbc.BulkCopyTimeout = 7200;
                                sbc.BatchSize = 500;

                                sbc.DestinationTableName = "Table2";
                                sbc.WriteToServer(rdr);
                            }
                        }
                    }
                }

                stopWatch.Stop();
                TimeSpan t = TimeSpan.FromMilliseconds(stopWatch.ElapsedMilliseconds);
                string elapsedTime = string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms", t.Hours, t.Minutes, t.Seconds, t.Milliseconds);
                Console.WriteLine(elapsedTime);
            }
            catch(Exception ex)
            {

            }
           
        }
    }
}
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

                var dbSource = ConfigurationManager.ConnectionStrings["DBSource"].ConnectionString;
                var dbDestination = ConfigurationManager.ConnectionStrings["DBDestination"].ConnectionString;

                var sourceTable = ConfigurationManager.AppSettings["SourceTable"];
                var destinationTable = ConfigurationManager.AppSettings["DestinationTable"];

                using (SqlConnection conn1 = new SqlConnection(dbSource))
                {
                    conn1.Open();

                    using (SqlCommand command = new SqlCommand($"select * from {sourceTable}", conn1))
                    {
                        command.CommandTimeout = 7200;

                        using (SqlDataReader rdr = command.ExecuteReader())
                        {
                            using (SqlBulkCopy sbc = new SqlBulkCopy(dbDestination))
                            {
                                sbc.BulkCopyTimeout = 0; // unlimited
                                sbc.BatchSize = 500;

                                sbc.DestinationTableName = $"{destinationTable}";
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